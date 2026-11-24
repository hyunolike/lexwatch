using System.Text.Json;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Governance;

/// <summary>데모 역할(로그인 없음). <c>/api</c>와 <c>/api/runs</c> 필터가 같은 집합을 쓴다.</summary>
public static class DemoRoles
{
    public const string Lawyer = "lawyer";
    public const string Partner = "partner";
    public const string Admin = "admin";
    public static bool IsKnown(string role) => role is Lawyer or Partner or Admin;
}

/// <summary>설정 <c>Governance:ClientNames</c>(쉼표 구분). 민감정보 가리기의 고객사 이름(GV-02).</summary>
public sealed record GovernanceOptions(IReadOnlyList<string> ClientNames)
{
    public static GovernanceOptions From(IConfiguration cfg) =>
        new((cfg["Governance:ClientNames"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

public static class AiUsage
{
    public const string Answered = "answered";
    public const string Refused = "refused";
    public const string Unavailable = "unavailable";
    public const string Failed = "failed";
    public static readonly string[] Outcomes = [Answered, Refused, Unavailable, Failed];
    public const int RetentionDays = 90;

    /// <summary>
    /// 기록 한 행을 저장한다. 실패해도 예외를 밖으로 내지 않는다(채팅 응답이 우선, GV-05).
    /// 요청이 취소돼도 남기도록 취소 토큰을 받지 않는다.
    /// </summary>
    public static async Task TryLogAsync(LexWatchDb db, AiUsageLog row, ILogger log)
    {
        try
        {
            db.AiUsageLogs.Add(row);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception e)
        {
            db.Entry(row).State = EntityState.Detached;
            log.LogWarning(e, "AI 사용 기록 저장 실패 outcome={Outcome} masked={Masked}", row.Outcome, row.MaskedJson);
        }
    }

    public static string MaskedJson(IEnumerable<MaskHit> hits) =>
        JsonSerializer.Serialize(hits.ToDictionary(h => h.Kind, h => h.Count));

    private static Dictionary<string, int> ParseMasked(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static void MapGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/governance").AddEndpointFilter(async (ctx, next) =>
        {
            if (ctx.HttpContext.Request.Headers["X-Demo-Role"].ToString() != DemoRoles.Admin)
                return Results.Json(new { code = "FORBIDDEN", message = "AI 사용 기록은 관리자만 볼 수 있다" }, statusCode: 403);
            return await next(ctx);
        });

        api.MapGet("/ai-usage", async (LexWatchDb db, TimeProvider time, CancellationToken ct) =>
        {
            const int days = 7;
            var since = time.GetUtcNow().AddHours(-24 * days);
            var week = await db.AiUsageLogs.AsNoTracking().Where(l => l.At >= since).ToListAsync(ct);
            var recent = await db.AiUsageLogs.AsNoTracking().OrderByDescending(l => l.At).Take(100).ToListAsync(ct);

            var masked = new Dictionary<string, int>();
            foreach (var (k, v) in week.SelectMany(l => ParseMasked(l.MaskedJson)))
                masked[k] = masked.GetValueOrDefault(k) + v;
            return Results.Ok(new
            {
                summary = new
                {
                    days,
                    total = week.Count,
                    byOutcome = Outcomes.ToDictionary(o => o, o => week.Count(l => l.Outcome == o)),
                    masked,
                    byProvider = week.Where(l => l.ProviderKind != null).GroupBy(l => l.ProviderKind!)
                        .ToDictionary(g => g.Key, g => g.Count()),
                },
                recent = recent.Select(l => new
                {
                    at = l.At, purpose = l.Purpose, groupReviewId = l.GroupReviewId, actorRole = l.ActorRole,
                    outcome = l.Outcome, providerKind = l.ProviderKind, providerName = l.ProviderName,
                    durationMs = l.DurationMs, masked = ParseMasked(l.MaskedJson),
                }),
                labels = SensitiveDataMasker.Labels,
            });
        });
    }
}
