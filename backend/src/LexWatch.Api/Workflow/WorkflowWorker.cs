using System.Diagnostics;
using System.Text.Json;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Observability;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Workflow;

public class WorkflowWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<WorkflowWorker> log)
    : BackgroundService
{
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await RunOnceAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                log.LogError(e, "워크플로 루프 오류");
            }
            try { await Task.Delay(IdleDelay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>한 단계를 처리한다. 집을 단계가 없으면 false.</summary>
    public async Task<bool> RunOnceAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexWatchDb>();
        var queue = new StepQueue(db, time);
        var step = await queue.ClaimNextAsync(Lease, ct);
        if (step is null) return false;

        var run = await db.WorkflowRuns.SingleAsync(r => r.Id == step.RunId, ct);
        if (run.Status == RunStatus.Queued) run.Status = RunStatus.Running;

        var handler = scope.ServiceProvider.GetServices<IStepHandler>().FirstOrDefault(h => h.Step == step.Step);
        var claimedAttempt = step.Attempt;
        var sw = Stopwatch.StartNew();
        // 실행의 TraceId에서 이어지는 span과 run_id/step/attempt 로그 범위. 나가는 HTTP 호출이 traceparent로 같은 trace를 전파한다.
        using var activity = Telemetry.StartStep(run.TraceId, step.Step, claimedAttempt);
        using var logScope = log.BeginScope(new Dictionary<string, object>
        {
            ["run_id"] = run.Id, ["step"] = step.Step, ["attempt"] = claimedAttempt,
        });
        StepResult result;
        if (handler is null)
        {
            result = new StepResult.Failed("NO_HANDLER", $"핸들러 없음: {step.Step}");
        }
        else
        {
            try
            {
                result = await handler.ExecuteAsync(new StepContext(step, run, db), ct);
                var kind = result switch
                {
                    StepResult.Succeeded sr => sr.ProviderKind,
                    StepResult.NeedsReview nr => nr.ProviderKind,
                    _ => null,
                };
                if (kind is not null && !ProviderKinds.Contains(kind))
                    throw new ArgumentException($"알 수 없는 ProviderKind: {kind}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                log.LogError(e, "핸들러 예외 step={Step} attempt={Attempt}", step.Step, step.Attempt);
                result = new StepResult.Retry("HANDLER_EXCEPTION", e.Message, null);
            }
        }

        // 결과 반영은 한 트랜잭션. 행 잠금 + (Running, 집은 시도 번호) 확인으로 소유권을 검증한다.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var running = nameof(StepStatus.Running);
        var owned = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "StepRuns" SET "Id" = "Id"
            WHERE "Id" = {step.Id} AND "Status" = {running} AND "Attempt" = {claimedAttempt}
            """, ct);
        if (owned == 0)
        {
            log.LogWarning("stale result discarded step={Step} id={Id} attempt={Attempt}", step.Step, step.Id, claimedAttempt);
            await tx.RollbackAsync(ct);
            return true;
        }

        var now = time.GetUtcNow();
        await ApplyAsync(db, step, run, result, now, ct);
        step.DurationMs = (int)sw.ElapsedMilliseconds;
        await db.SaveChangesAsync(ct);

        await UpdateRunStatusAsync(db, run, now, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        log.LogInformation("단계 처리 완료 status={Status} duration_ms={DurationMs}", step.Status, step.DurationMs);
        return true;
    }

    private static readonly HashSet<string> ProviderKinds = ["nvidia", "cloudflare", "groq", "fallback", "replay", "none"];

    private async Task ApplyAsync(LexWatchDb db, StepRun step, WorkflowRun run, StepResult result,
        DateTimeOffset now, CancellationToken ct)
    {
        step.LeaseUntil = null;
        step.FinishedAt = now;
        switch (result)
        {
            case StepResult.Succeeded s:
                step.Status = StepStatus.Succeeded;
                step.ErrorCode = null;
                step.ErrorMessage = null;
                step.OutputJson = s.Output is null ? null : JsonSerializer.Serialize(s.Output, StepQueue.Json);
                step.ProviderKind = s.ProviderKind;
                step.ProviderName = s.ProviderName;
                foreach (var n in s.Next) db.StepRuns.Add(StepQueue.Create(step.RunId, n, now));
                break;

            case StepResult.Retry r:
                var (retry, notBefore) = RetryPolicy.Decide(step.Step, step.Attempt, r, now);
                step.ErrorCode = r.ErrorCode;
                step.ErrorMessage = r.Message;
                if (retry)
                {
                    step.Status = StepStatus.RetryScheduled;
                    step.NotBefore = notBefore;
                    step.FinishedAt = null;
                }
                else
                {
                    await FailAsync(db, step, run, now, ct);
                }
                break;

            case StepResult.NeedsReview nr:
                step.OutputJson = nr.Output is null ? null : JsonSerializer.Serialize(nr.Output, StepQueue.Json);
                step.ProviderKind = nr.ProviderKind;
                step.ProviderName = nr.ProviderName;
                if (step.GroupReviewId is null)
                {
                    // 실행 단위 단계(분류)에는 "검토 필요"로 표시할 그룹이 없다. 그대로 두면 그룹 0개로
                    // 실행이 completed가 되어 개정 한 건이 조용히 사라진다. 실패로 남겨 이력에 보이고 다시 실행할 수 있게 한다.
                    step.ErrorCode = IsErrorCode(nr.Reason) ? nr.Reason : "NEEDS_REVIEW_WITHOUT_GROUP";
                    step.ErrorMessage = $"{StepLabel(step.Step)} 결과를 검증하지 못해 그룹 검토를 만들지 못했다({nr.Reason}). 관리자가 다시 실행할 수 있다";
                    await FailAsync(db, step, run, now, ct);
                    break;
                }
                step.Status = StepStatus.NeedsReview;
                step.ErrorCode = null;
                step.ErrorMessage = nr.Reason;
                await SetGroupStatusAsync(db, step.GroupReviewId, ReviewStatus.NeedsReview, ct);
                break;

            case StepResult.Failed f:
                step.ErrorCode = f.ErrorCode;
                step.ErrorMessage = f.Message;
                await FailAsync(db, step, run, now, ct);
                break;
        }
    }

    private static async Task FailAsync(LexWatchDb db, StepRun step, WorkflowRun run, DateTimeOffset now,
        CancellationToken ct)
    {
        step.Status = StepStatus.Failed;
        if (step.Step == "notify" && step.GroupReviewId is { } notifyGroup)
        {
            // 발송 실패가 승인을 되돌리지 않는다(설계 §6 10단계). 그룹은 Approved로 두고 관리자 알림을 남긴다.
            // 실패한 알림 단계는 기존 재실행 규칙(Approved 그룹의 notify)으로 다시 실행할 수 있다.
            var groupCode = await db.GroupReviews.Where(g => g.Id == notifyGroup).Select(g => g.GroupCode)
                .FirstOrDefaultAsync(ct) ?? "?";
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(), GroupReviewId = notifyGroup, Channel = "admin", Status = "pending", SentAt = now,
                Body = $"승인된 메모의 알림 발송이 실패했습니다(그룹 {groupCode}, {step.ErrorCode}). 승인은 유지되며 실행 이력에서 다시 실행할 수 있습니다.",
            });
        }
        else if (step.GroupReviewId is not null)
        {
            await SetGroupStatusAsync(db, step.GroupReviewId, ReviewStatus.NeedsReview, ct);
        }
        else
        {
            run.Status = RunStatus.Failed;
            run.FinishedAt = now;
        }
    }

    private static async Task SetGroupStatusAsync(LexWatchDb db, Guid? id, ReviewStatus status, CancellationToken ct)
    {
        if (id is null) return;
        var g = await db.GroupReviews.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (g is not null) g.Status = status;
    }

    private static readonly StepStatus[] ActiveSteps = [StepStatus.Pending, StepStatus.RetryScheduled, StepStatus.Running];

    private static readonly ReviewStatus[] TerminalReviews =
        [ReviewStatus.Approved, ReviewStatus.Rejected, ReviewStatus.Notified, ReviewStatus.ClosedIrrelevant];

    /// <summary>남은 단계가 없고 모든 그룹이 종결이면 Completed(알림이 실패해 Approved로 남은 그룹이 있으면 Failed).</summary>
    public static async Task UpdateRunStatusAsync(LexWatchDb db, WorkflowRun run, DateTimeOffset now,
        CancellationToken ct)
    {
        if (run.Status is RunStatus.Failed or RunStatus.Completed or RunStatus.ClosedIrrelevant) return;
        var hasActive = await db.StepRuns.AnyAsync(s => s.RunId == run.Id && ActiveSteps.Contains(s.Status), ct);
        if (hasActive) return;
        var openGroup = await db.GroupReviews.AnyAsync(g => g.RunId == run.Id && !TerminalReviews.Contains(g.Status), ct);
        if (openGroup)
        {
            // 남은 단계가 없고 열린 그룹이 전부 결정 대기면 변호사를 기다리는 중이다(재실행 뒤 Running으로 남지 않게).
            var allDecidable = !await db.GroupReviews.AnyAsync(g => g.RunId == run.Id && !TerminalReviews.Contains(g.Status)
                && g.Status != ReviewStatus.WaitingApproval && g.Status != ReviewStatus.NeedsReview, ct);
            if (allDecidable && run.Status == RunStatus.Running) run.Status = RunStatus.InReview;
            return;
        }
        // 남은 단계가 없는데 Approved로 남은 그룹은 알림 단계가 끝내 실패한 그룹이다. 완료로 보이지 않게 한다.
        var undelivered = await db.GroupReviews.AnyAsync(g => g.RunId == run.Id && g.Status == ReviewStatus.Approved, ct);
        run.Status = undelivered ? RunStatus.Failed : RunStatus.Completed;
        run.FinishedAt = now;
    }

    private static bool IsErrorCode(string s) =>
        s.Length is > 0 and <= 64 && s.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_');

    private static string StepLabel(string step) => step == "classify" ? "분류" : step;
}
