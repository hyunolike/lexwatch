using System.Diagnostics;
using System.Text.Json;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
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
        var sw = Stopwatch.StartNew();
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

        var now = time.GetUtcNow();
        await ApplyAsync(db, step, run, result, now, ct);
        step.DurationMs = (int)sw.ElapsedMilliseconds;
        await db.SaveChangesAsync(ct);

        await UpdateRunStatusAsync(db, run, now, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

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
                step.Status = StepStatus.NeedsReview;
                step.ErrorCode = null;
                step.ErrorMessage = nr.Reason;
                step.OutputJson = nr.Output is null ? null : JsonSerializer.Serialize(nr.Output, StepQueue.Json);
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
        if (step.GroupReviewId is not null)
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

    /// <summary>남은 단계가 없고 모든 그룹이 종결이면 Completed. 이후 작업이 확장할 수 있도록 한 곳에 둔다.</summary>
    public static async Task UpdateRunStatusAsync(LexWatchDb db, WorkflowRun run, DateTimeOffset now,
        CancellationToken ct)
    {
        if (run.Status is RunStatus.Failed or RunStatus.Completed or RunStatus.ClosedIrrelevant) return;
        var hasActive = await db.StepRuns.AnyAsync(s => s.RunId == run.Id && ActiveSteps.Contains(s.Status), ct);
        if (hasActive) return;
        var openGroup = await db.GroupReviews.AnyAsync(g => g.RunId == run.Id && !TerminalReviews.Contains(g.Status), ct);
        if (openGroup) return;
        run.Status = RunStatus.Completed;
        run.FinishedAt = now;
    }
}
