using System.Text.Json;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Radar;

public record BillFailure(string Target, string Code);

public record BillCollectResult(int NewBills, int StageChanges, int Refreshed, IReadOnlyList<BillFailure> Failures);

/// <summary>조회를 1건 이상 시도했는데 성공한 호출이 없다(BR-05). 엔드포인트가 502로 바꾼다.</summary>
public class BillCollectUnavailableException(IReadOnlyList<BillFailure> failures)
    : Exception("국회 API 조회가 모두 실패했다")
{
    public IReadOnlyList<BillFailure> Failures { get; } = failures;
}

/// <summary>
/// 공포 전 법안 수집(bill-radar). 법령 수집처럼 잡 단위로 돈다(POST /jobs/collect-bills).
/// 외부 조회는 트랜잭션 밖에서 상한 안에 끝내고, 병합과 저장만 짧은 트랜잭션과 자문 잠금 안에서 한다(설계 5절).
/// </summary>
public class BillRadarService(
    LexWatchDb db,
    AssemblyApiClient api,
    AssemblyApiOptions options,
    TimeProvider time,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    public const string Source = "assembly";
    public const int KeylessRefreshLimit = 60;
    public const int KeyedRefreshLimit = 200;
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(250);
    private const long LockKey = 0x4C57_4249_4C4C; // "LWBILL"
    private const int MaxLoggedFailures = 50;
    private static readonly TimeZoneInfo Kst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    public int RefreshLimit => options.ApiKey == "" ? KeylessRefreshLimit : KeyedRefreshLimit;

    /// <summary>발의일 00:00 KST(BR-02). 옛 법안이 한꺼번에 들어와도 "새 발의"로 보이지 않게 사건 시각으로 쓴다.</summary>
    public static DateTimeOffset KstMidnight(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), Kst.GetUtcOffset(day.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();

    private sealed record Fetched(AssemblyBill Row, string LawName, bool FromList);

    /// <summary>budget을 주지 않으면 120초. 요청 취소와는 엮지 않는다(curl이 먼저 끊겨도 받은 만큼 저장한다).</summary>
    public async Task<BillCollectResult> CollectAsync(TimeSpan? budget = null)
    {
        var started = time.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(started, Kst).DateTime);
        using var cts = new CancellationTokenSource(budget ?? Budget, time);
        var ct = cts.Token;

        // ① 조회
        var watched = (await db.PracticeGroups.AsNoTracking().Select(g => g.WatchLawNames).ToListAsync())
            .SelectMany(n => n).Select(n => n.Trim()).Where(n => n != "").Distinct().Order(StringComparer.Ordinal).ToList();
        var candidates = (await db.Bills.AsNoTracking()
                .Where(b => b.Source == Source && watched.Contains(b.LawName) && b.Stage != BillStages.Closed)
                .OrderBy(b => b.LastCheckedAt).ThenBy(b => b.BillNo)
                .Select(b => new { b.Id, b.ExternalId, b.BillNo, b.LawName, b.Stage, b.ProcOn })
                .ToListAsync())
            .Where(b => BillStages.NeedsRefresh(b.Stage, b.ProcOn, today))
            .ToList();

        var fetched = new Dictionary<string, Fetched>();
        var failures = new List<BillFailure>();
        var attempted = 0;
        var succeeded = 0;
        var failedRefresh = new List<Guid>();
        var budgetHit = false;
        var first = true;

        async Task<AssemblyResult<IReadOnlyList<AssemblyBill>>?> Call(Func<CancellationToken, Task<AssemblyResult<IReadOnlyList<AssemblyBill>>>> fn)
        {
            if (ct.IsCancellationRequested) { budgetHit = true; return null; }
            try
            {
                if (!first) await (delay ?? Task.Delay)(Pause, ct);
                first = false;
                attempted++;
                return await fn(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                budgetHit = true;
                return null;
            }
        }

        foreach (var law in watched)
        {
            var r = await Call(c => api.ListByLawNameAsync(law, c));
            if (r is null) break;
            switch (r)
            {
                case AssemblyResult<IReadOnlyList<AssemblyBill>>.Error e:
                    failures.Add(new($"law:{law}", e.Code));
                    break;
                case AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok ok:
                    succeeded++;
                    failures.AddRange(ok.BadRows.Select(no => new BillFailure($"row:{no}", "ASSEMBLY_API_BAD_ROW")));
                    foreach (var row in ok.Value.Where(x => BillNames.Matches(x.BillName, law)))
                        fetched.TryAdd(row.BillId, new(row, law, true));
                    break;
            }
        }

        var refreshed = 0;
        if (!budgetHit)
            foreach (var c in candidates.Where(c => !fetched.ContainsKey(c.ExternalId)).Take(RefreshLimit))
            {
                var r = await Call(t => api.GetByBillNoAsync(c.BillNo, t));
                if (r is null) break;
                switch (r)
                {
                    case AssemblyResult<IReadOnlyList<AssemblyBill>>.Error e:
                        failures.Add(new($"bill:{c.BillNo}", e.Code));
                        failedRefresh.Add(c.Id);
                        break;
                    case AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok ok when ok.Value.Count == 0:
                        // 결과 없음은 실패 목록에만 넣고 502 판정에는 넣지 않는다(BR-05).
                        failures.Add(new($"bill:{c.BillNo}", "ASSEMBLY_API_NOT_FOUND"));
                        failedRefresh.Add(c.Id);
                        break;
                    case AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok ok:
                        succeeded++;
                        if (ok.Value.FirstOrDefault(x => x.BillId == c.ExternalId) is { } row)
                        {
                            fetched[row.BillId] = new(row, c.LawName, false);
                            refreshed++;
                        }
                        else
                        {
                            failures.Add(new($"bill:{c.BillNo}", "ASSEMBLY_API_BAD_ROW"));
                            failedRefresh.Add(c.Id);
                        }
                        break;
                }
            }
        if (budgetHit) failures.Add(new("*", "ASSEMBLY_BUDGET_EXCEEDED"));

        // NOT_FOUND만 난 호출은 시도로도 성공으로도 세지 않는다.
        var notFound = failures.Count(f => f.Code == "ASSEMBLY_API_NOT_FOUND");
        if (attempted - notFound > 0 && succeeded == 0)
        {
            await LogAsync(started, false, 0, 0, 0, failures);
            throw new BillCollectUnavailableException(failures);
        }

        // ② 저장
        var now = time.GetUtcNow();
        int newBills = 0, stageChanges = 0;
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({LockKey})");

        var ids = fetched.Keys.ToList();
        var existing = await db.Bills.Where(b => b.Source == Source && ids.Contains(b.ExternalId))
            .ToDictionaryAsync(b => b.ExternalId);
        foreach (var f in fetched.Values)
        {
            var row = f.Row;
            var stage = BillStages.From(row);
            if (!existing.TryGetValue(row.BillId, out var bill))
            {
                if (!f.FromList) continue;
                bill = new Bill { Id = Guid.NewGuid(), Source = Source, ExternalId = row.BillId, FirstSeenAt = now };
                Apply(bill, row, f.LawName, stage, now);
                var at = KstMidnight(row.ProposedOn);
                bill.LastEventAt = at;
                db.Bills.Add(bill);
                db.BillEvents.Add(new BillEvent { Id = Guid.NewGuid(), BillId = bill.Id, At = at, Kind = "proposed", ToStage = stage });
                newBills++;
                continue;
            }
            var before = bill.Stage;
            Apply(bill, row, bill.LawName, stage, now);
            if (before != stage)
            {
                db.BillEvents.Add(new BillEvent
                {
                    Id = Guid.NewGuid(), BillId = bill.Id, At = now, Kind = "stage_changed", FromStage = before, ToStage = stage,
                });
                if (now > bill.LastEventAt) bill.LastEventAt = now;
                stageChanges++;
            }
        }
        // 실패한 재조회도 뒤로 미룬다(BR-04).
        if (failedRefresh.Count > 0)
            await db.Bills.Where(b => failedRefresh.Contains(b.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.LastCheckedAt, now));

        await db.SaveChangesAsync();
        await LogAsync(started, succeeded > 0 || (attempted == 0 && !budgetHit), newBills, stageChanges, refreshed, failures, save: false);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new BillCollectResult(newBills, stageChanges, refreshed, failures);
    }

    private static void Apply(Bill bill, AssemblyBill row, string lawName, string stage, DateTimeOffset now)
    {
        bill.BillNo = row.BillNo;
        bill.BillName = row.BillName;
        bill.LawName = lawName;
        bill.Stage = stage;
        bill.ProposedOn = row.ProposedOn;
        bill.Proposer = row.Proposer;
        bill.LeadProposer = row.LeadProposer;
        bill.Committee = row.Committee;
        bill.ProcResult = row.ProcResult ?? (stage == BillStages.Merged ? row.CommitteeResult : null);
        bill.ProcOn = row.ProcOn;
        bill.DetailUrl = row.DetailUrl;
        bill.RawJson = row.RawJson;
        bill.LastCheckedAt = now;
    }

    private async Task LogAsync(DateTimeOffset started, bool ok, int newBills, int changes, int refreshed,
        IReadOnlyList<BillFailure> failures, bool save = true)
    {
        db.BillCollectLogs.Add(new BillCollectLog
        {
            Id = Guid.NewGuid(), StartedAt = started, FinishedAt = time.GetUtcNow(), Ok = ok, NewBills = newBills,
            StageChanges = changes, Refreshed = refreshed, FailureCount = failures.Count,
            FailuresJson = JsonSerializer.Serialize(failures.Take(MaxLoggedFailures)
                .Select(f => new { target = f.Target, code = f.Code })),
        });
        if (save) await db.SaveChangesAsync();
    }
}
