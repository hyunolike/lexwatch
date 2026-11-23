using System.Security.Cryptography;
using System.Text;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.LawApi;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Workflow.Steps;

public record CollectResult(int Created, int Skipped, IReadOnlyList<Guid> RunIds);

public class LawApiException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// 공포 법령 수집. 단계(IStepHandler)가 아니라 잡 단위로 돈다(POST /jobs/collect).
/// 잡이므로 SaveChanges·트랜잭션을 직접 쓴다 (단계 핸들러 규칙의 예외).
/// </summary>
public class CollectService(
    LexWatchDb db,
    LawApiClient api,
    TimeProvider time,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    public const int MaxAttempts = 3;
    public const int AlertStreakDays = 3;
    private static readonly TimeZoneInfo Kst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    public static (DateOnly Yesterday, DateOnly Today) KstWindow(TimeProvider time)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Kst).DateTime);
        return (today.AddDays(-1), today);
    }

    public async Task<CollectResult> CollectAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var items = await ListWithRetryAsync(from, to, ct);
        var runIds = new List<Guid>();
        var skipped = 0;
        foreach (var item in items)
        {
            if (await TryInsertAsync(item, ct) is { } runId) runIds.Add(runId); else skipped++;
        }
        await RecordAndAlertAsync(items.Count, ct);
        return new CollectResult(runIds.Count, skipped, runIds);
    }

    private async Task<IReadOnlyList<LawListItem>> ListWithRetryAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        LawApiResult<IReadOnlyList<LawListItem>>.Error? last = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            switch (await api.ListPromulgatedAsync(from, to, ct))
            {
                case LawApiResult<IReadOnlyList<LawListItem>>.Ok ok:
                    return ok.Value;
                case LawApiResult<IReadOnlyList<LawListItem>>.Error err:
                    last = err;
                    break;
            }
            if (attempt < MaxAttempts)
                await (delay ?? Task.Delay)(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }
        throw new LawApiException(last!.Code, last.Message);
    }

    /// <summary>새 법령이면 실행을 만들고 그 ID를, 이미 있으면 null을 돌려준다.</summary>
    private async Task<Guid?> TryInsertAsync(LawListItem item, CancellationToken ct)
    {
        var raw = item.RawJson ?? "{}";
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var key = $"{item.LawId}:{item.PromulgationDate:yyyyMMdd}:{item.EffectiveDate:yyyyMMdd}";
        var id = Guid.NewGuid();
        var now = time.GetUtcNow().ToUniversalTime();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LawChanges"
                ("Id","LawId","LawName","Mst","PromulgationDate","EffectiveDate","RawJson","RawSha256","FetchedAt","IdempotencyKey")
            VALUES ({id},{item.LawId},{item.LawName},{item.Mst},{item.PromulgationDate},{item.EffectiveDate}::date,
                    {raw}::jsonb,{sha},{now},{key})
            ON CONFLICT ("IdempotencyKey") DO NOTHING
            """, ct);
        if (inserted == 0)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(), LawChangeId = id, Status = RunStatus.Queued,
            TraceId = LexWatch.Api.Observability.Telemetry.NewTraceId(), CreatedAt = now,
        };
        db.WorkflowRuns.Add(run);
        db.StepRuns.Add(StepQueue.Create(run.Id, new NextStep("fetch_articles", null, null), now));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return run.Id;
    }

    /// <summary>오늘 결과(항목 수)를 기록하고, 0건이 사흘 연속이면 관리자 알림을 한 번만 만든다.</summary>
    private async Task RecordAndAlertAsync(int count, CancellationToken ct)
    {
        var (_, today) = KstWindow(time);
        var now = time.GetUtcNow().ToUniversalTime();
        // 같은 날 여러 번 돌아도 0이 실제 결과를 덮지 않도록 큰 값을 남긴다.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CollectLogs" ("Day","ItemCount","At") VALUES ({today},{count},{now})
            ON CONFLICT ("Day") DO UPDATE SET "ItemCount" = GREATEST("CollectLogs"."ItemCount", EXCLUDED."ItemCount"),
                                              "At" = EXCLUDED."At"
            """, ct);

        var days = Enumerable.Range(0, AlertStreakDays).Select(i => today.AddDays(-i)).ToList();
        var zeroDays = await db.CollectLogs.CountAsync(l => days.Contains(l.Day) && l.ItemCount == 0, ct);
        if (zeroDays < AlertStreakDays) return;

        var body = $"법령 API 수집 결과가 {AlertStreakDays}일 연속 0건입니다 ({today:yyyy-MM-dd} 기준). API 키·등록 IP를 확인하세요.";
        if (await db.Notifications.AnyAsync(n => n.Channel == "admin" && n.Body == body, ct)) return;
        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), Channel = "admin", Status = "pending", Body = body });
        await db.SaveChangesAsync(ct);
    }
}
