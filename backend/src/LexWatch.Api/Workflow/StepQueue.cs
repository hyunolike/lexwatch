using System.Text.Json;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Workflow;

public class StepQueue(LexWatchDb db, TimeProvider time)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static StepRun Create(Guid runId, NextStep next, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        GroupReviewId = next.GroupReviewId,
        Step = next.Step,
        Attempt = 0,
        Status = StepStatus.Pending,
        NotBefore = now,
        InputJson = next.Input is null ? null : JsonSerializer.Serialize(next.Input, Json),
        ProviderKind = "none",
    };

    public async Task<StepRun> EnqueueAsync(Guid runId, NextStep next, CancellationToken ct = default)
    {
        var step = Create(runId, next, time.GetUtcNow());
        db.StepRuns.Add(step);
        await db.SaveChangesAsync(ct);
        return step;
    }

    /// <summary>
    /// 한 문장(원자적)으로 행 하나를 집어 Running으로 바꾼다. 시계는 TimeProvider에서 받아 파라미터로 넘긴다.
    /// </summary>
    public async Task<StepRun?> ClaimNextAsync(TimeSpan lease, CancellationToken ct = default)
    {
        var now = time.GetUtcNow().ToUniversalTime();
        var leaseUntil = now + lease;
        var pending = nameof(StepStatus.Pending);
        var retry = nameof(StepStatus.RetryScheduled);
        var running = nameof(StepStatus.Running);

        var rows = await db.StepRuns.FromSqlInterpolated($"""
            UPDATE "StepRuns" SET
                "Status" = {running},
                "LeaseUntil" = {leaseUntil},
                "Attempt" = "Attempt" + 1,
                "StartedAt" = {now},
                "FinishedAt" = NULL
            WHERE "Id" = (
                SELECT "Id" FROM "StepRuns"
                WHERE (("Status" IN ({pending}, {retry}) AND "NotBefore" <= {now})
                    OR ("Status" = {running} AND "LeaseUntil" < {now}))
                ORDER BY "NotBefore"
                FOR UPDATE SKIP LOCKED
                LIMIT 1)
            RETURNING *
            """).ToListAsync(ct);
        return rows.SingleOrDefault();
    }
}
