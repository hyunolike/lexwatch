using System.Text.Json;
using LexWatch.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Radar;

/// <summary>GET /api/radar (bill-radar BR-06, BR-07). 모든 역할이 보고 역할 헤더가 없어도 된다.</summary>
public static class RadarEndpoints
{
    public const int MaxBills = 200;
    public const int MaxBillsLimit = 1000;
    public const int PromulgationWindowDays = 40;
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(48);

    public static void MapRadarEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/radar", async (string? group, int? limit, LexWatchDb db, TimeProvider time, CancellationToken ct) =>
        {
            var groups = await db.PracticeGroups.AsNoTracking().OrderBy(g => g.Code)
                .Select(g => new { g.Code, g.Name, g.WatchLawNames }).ToListAsync(ct);
            if (group is not null && groups.All(g => g.Code != group))
                return Results.Json(new { code = "UNKNOWN_GROUP", message = "알 수 없는 그룹이다" }, statusCode: 400);

            var groupsOf = groups
                .SelectMany(g => g.WatchLawNames.Select(n => (Law: n.Trim(), g.Code)))
                .GroupBy(x => x.Law)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Code).Distinct().Order(StringComparer.Ordinal).ToArray());
            var laws = group is null
                ? groupsOf.Keys.ToList()
                : groupsOf.Where(p => p.Value.Contains(group)).Select(p => p.Key).ToList();

            if (limit is < 1 or > MaxBillsLimit)
                return Results.Json(new { code = "INVALID_LIMIT", message = $"limit은 1~{MaxBillsLimit}이어야 한다" }, statusCode: 400);
            var take = limit ?? MaxBills;
            var bills = await db.Bills.AsNoTracking()
                .Where(b => laws.Contains(b.LawName))
                .OrderByDescending(b => b.LastEventAt).ThenByDescending(b => b.BillNo)
                .Take(take).ToListAsync(ct);
            // 화면의 탭 숫자는 잘린 목록이 아니라 전체 건수를 보여야 한다. 법률명별로 세고 그룹에 나눠 더한다.
            var perLaw = await db.Bills.AsNoTracking().Where(b => laws.Contains(b.LawName))
                .GroupBy(b => b.LawName).Select(x => new { Law = x.Key, N = x.Count() }).ToListAsync(ct);
            var byGroup = groups.ToDictionary(g => g.Code,
                g => perLaw.Where(p => groupsOf.GetValueOrDefault(p.Law, []).Contains(g.Code)).Sum(p => p.N));
            var billIds = bills.Select(b => b.Id).ToList();
            var lastEvents = (await db.BillEvents.AsNoTracking().Where(e => billIds.Contains(e.BillId)).ToListAsync(ct))
                .GroupBy(e => e.BillId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.At).ThenByDescending(e => e.Kind == "stage_changed").First());

            var promulgated = await FindPromulgatedAsync(db, bills, groupsOf, ct);

            var logs = await db.BillCollectLogs.AsNoTracking().OrderByDescending(l => l.StartedAt).Take(1).ToListAsync(ct);
            var lastOk = await db.BillCollectLogs.AsNoTracking().Where(l => l.Ok)
                .OrderByDescending(l => l.FinishedAt).Select(l => (DateTimeOffset?)l.FinishedAt).FirstOrDefaultAsync(ct);
            var assembly = logs.Count == 0 ? "never"
                : !logs[0].Ok || lastOk is null || time.GetUtcNow() - lastOk.Value > StaleAfter ? "failing"
                : "ok";

            return Results.Ok(new
            {
                lastCollectedAt = lastOk,
                totals = new { all = perLaw.Sum(p => p.N), shown = bills.Count, limit = take, byGroup },
                sources = new { assembly, lawmaking = "not_connected" },
                watch = groups.Select(g => new { groupCode = g.Code, groupName = g.Name, lawNames = g.WatchLawNames }),
                bills = bills.Select(b => new
                {
                    id = b.Id, billNo = b.BillNo, billName = b.BillName, lawName = b.LawName, stage = b.Stage,
                    proposedOn = b.ProposedOn, proposer = b.Proposer, leadProposer = b.LeadProposer,
                    committee = b.Committee, procResult = b.ProcResult, procOn = b.ProcOn,
                    groupCodes = groupsOf.GetValueOrDefault(b.LawName, []),
                    detailUrl = b.DetailUrl,
                    lastEvent = lastEvents.TryGetValue(b.Id, out var e)
                        ? new { kind = e.Kind, at = e.At, fromStage = e.FromStage, toStage = e.ToStage }
                        : null,
                    promulgated = promulgated.GetValueOrDefault(b.Id),
                }),
            });
        });
    }

    public sealed record Promulgated(Guid LawChangeId, DateOnly PromulgationDate, Guid? ReviewId);

    /// <summary>
    /// BR-07 공포 후보. 같은 법률명, 처리일부터 40일 안, 타법개정이 아니고 재현 예시가 아닌 공포 법령 중 가장 이른 것.
    /// </summary>
    private static async Task<Dictionary<Guid, Promulgated>> FindPromulgatedAsync(
        LexWatchDb db, List<Data.Entities.Bill> bills, Dictionary<string, string[]> groupsOf, CancellationToken ct)
    {
        var decided = bills.Where(b => b.ProcOn is not null && b.Stage is BillStages.Passed or BillStages.Merged).ToList();
        if (decided.Count == 0) return [];
        var names = decided.Select(b => b.LawName).Distinct().ToList();
        var earliest = decided.Min(b => b.ProcOn!.Value);
        var changes = await (
                from l in db.LawChanges.AsNoTracking()
                where names.Contains(l.LawName) && l.PromulgationDate >= earliest
                      && !db.WorkflowRuns.Any(r => r.LawChangeId == l.Id && r.IsReplay)
                select new { l.Id, l.LawName, l.PromulgationDate, l.RawJson })
            .ToListAsync(ct);
        changes = changes.Where(c => !IsOtherLawAmendment(c.RawJson)).ToList();
        if (changes.Count == 0) return [];

        var changeIds = changes.Select(c => c.Id).ToList();
        var reviews = await (
                from g in db.GroupReviews.AsNoTracking()
                join r in db.WorkflowRuns.AsNoTracking() on g.RunId equals r.Id
                where changeIds.Contains(r.LawChangeId)
                select new { r.LawChangeId, g.Id, g.GroupCode })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, Promulgated>();
        foreach (var b in decided)
        {
            var proc = b.ProcOn!.Value;
            var hit = changes
                .Where(c => c.LawName == b.LawName && c.PromulgationDate >= proc
                            && c.PromulgationDate <= proc.AddDays(PromulgationWindowDays))
                .OrderBy(c => c.PromulgationDate).FirstOrDefault();
            if (hit is null) continue;
            var codes = groupsOf.GetValueOrDefault(b.LawName, []);
            var review = reviews.Where(r => r.LawChangeId == hit.Id && codes.Contains(r.GroupCode))
                .OrderBy(r => r.GroupCode, StringComparer.Ordinal).FirstOrDefault();
            result[b.Id] = new Promulgated(hit.Id, hit.PromulgationDate, review?.Id);
        }
        return result;
    }

    /// <summary>법령 목록 응답의 제개정구분명이 타법개정인가. 형식을 모르면 아니라고 본다.</summary>
    public static bool IsOtherLawAmendment(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("제개정구분명", out var v)
                   && v.ValueKind == JsonValueKind.String
                   && v.GetString()!.Trim() == "타법개정";
        }
        catch (JsonException) { return false; }
    }
}
