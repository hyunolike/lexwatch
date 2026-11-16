using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

[Collection("pg")]
public class WorkflowWorkerTests(PostgresFixture pg) : IAsyncLifetime
{
    public async Task InitializeAsync() => await pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private async Task<StepRun> EnqueueAsync(WorkflowRun run, string step, FakeTime time, Guid? group = null)
    {
        await using var db = pg.CreateDb();
        return await new StepQueue(db, time).EnqueueAsync(run.Id, new NextStep(step, group, null));
    }

    private async Task<StepRun> LoadAsync(Guid id)
    {
        await using var db = pg.CreateDb();
        return await db.StepRuns.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    [Fact]
    public async Task Retry_with_after_sets_not_before_from_retry_after()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var step = await EnqueueAsync(run, "classify", time);
        var worker = Seed.Worker(pg, time,
            new LambdaHandler("classify", _ => new StepResult.Retry("LLM_RATE_LIMITED", "429", TimeSpan.FromSeconds(30))));

        Assert.True(await worker.RunOnceAsync());

        var s = await LoadAsync(step.Id);
        Assert.Equal(StepStatus.RetryScheduled, s.Status);
        Assert.Equal(T0.AddSeconds(30), s.NotBefore);
        Assert.Equal("LLM_RATE_LIMITED", s.ErrorCode);
        Assert.Null(s.LeaseUntil);
        Assert.False(await worker.RunOnceAsync()); // 아직 not_before 전
    }

    [Fact]
    public async Task Retry_without_after_uses_exponential_backoff()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var step = await EnqueueAsync(run, "analyze", time);
        var worker = Seed.Worker(pg, time, new LambdaHandler("analyze", _ => new StepResult.Retry("X", "x", null)));

        await worker.RunOnceAsync();
        Assert.Equal(T0.AddSeconds(2), (await LoadAsync(step.Id)).NotBefore); // attempt 1 → 2^1
    }

    [Fact]
    public async Task Exceeding_max_attempts_marks_failed()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var step = await EnqueueAsync(run, "classify", time);
        var worker = Seed.Worker(pg, time, new LambdaHandler("classify", _ => new StepResult.Retry("LLM_RATE_LIMITED", "429", TimeSpan.FromSeconds(1))));

        for (var i = 0; i < 5; i++)
        {
            time.Now = time.Now.AddMinutes(1);
            Assert.True(await worker.RunOnceAsync());
        }

        var s = await LoadAsync(step.Id);
        Assert.Equal(5, s.Attempt);
        Assert.Equal(StepStatus.Failed, s.Status);
        await using var db = pg.CreateDb();
        Assert.Equal(RunStatus.Failed, (await db.WorkflowRuns.SingleAsync()).Status);
    }

    [Fact]
    public async Task Succeeded_enqueues_next_steps_and_completes_run_when_done()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var first = await EnqueueAsync(run, "collect", time);
        var worker = Seed.Worker(pg, time,
            new LambdaHandler("collect", _ => new StepResult.Succeeded(new { n = 2 },
                [new NextStep("fetch_articles", null, new { a = 1 }), new NextStep("classify", null, null)], "api", "law")),
            new LambdaHandler("fetch_articles", _ => new StepResult.Succeeded(null, [])),
            new LambdaHandler("classify", _ => new StepResult.Succeeded(null, [])));

        await worker.RunOnceAsync();

        await using (var db = pg.CreateDb())
        {
            var f = await db.StepRuns.SingleAsync(s => s.Id == first.Id);
            Assert.Equal(StepStatus.Succeeded, f.Status);
            Assert.Equal("api", f.ProviderKind);
            Assert.Equal("law", f.ProviderName);
            Assert.NotNull(f.DurationMs);
            var next = await db.StepRuns.Where(s => s.Id != first.Id).ToListAsync();
            Assert.Equal(2, next.Count);
            Assert.All(next, s => Assert.Equal(StepStatus.Pending, s.Status));
            Assert.Contains(next, s => s.Step == "fetch_articles" && System.Text.Json.JsonDocument.Parse(s.InputJson!).RootElement.GetProperty("a").GetInt32() == 1);
            Assert.Equal(RunStatus.Running, (await db.WorkflowRuns.SingleAsync()).Status);
        }

        await worker.RunOnceAsync();
        await worker.RunOnceAsync();
        await using var db2 = pg.CreateDb();
        var r = await db2.WorkflowRuns.SingleAsync();
        Assert.Equal(RunStatus.Completed, r.Status);
        Assert.NotNull(r.FinishedAt);
    }

    [Fact]
    public async Task Unknown_step_fails_with_no_handler_and_exception_becomes_retry()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var a = await EnqueueAsync(run, "mystery", time);
        var b = await EnqueueAsync(run, "notify", time);
        var worker = Seed.Worker(pg, time, new LambdaHandler("notify", _ => throw new InvalidOperationException("boom")));

        await worker.RunOnceAsync();
        await worker.RunOnceAsync();

        var sa = await LoadAsync(a.Id);
        var sb = await LoadAsync(b.Id);
        Assert.Equal(StepStatus.Failed, sa.Status);
        Assert.Equal("NO_HANDLER", sa.ErrorCode);
        Assert.Equal(StepStatus.RetryScheduled, sb.Status);
        Assert.Equal("HANDLER_EXCEPTION", sb.ErrorCode);
        Assert.Equal("boom", sb.ErrorMessage);
    }

    [Fact]
    public async Task Group_step_failure_and_needs_review_only_touch_the_group()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        Guid g1, g2;
        await using (var db = pg.CreateDb())
        {
            var a = new GroupReview { Id = Guid.NewGuid(), RunId = run.Id, GroupCode = "labor", Status = ReviewStatus.Analyzing };
            var b = new GroupReview { Id = Guid.NewGuid(), RunId = run.Id, GroupCode = "privacy", Status = ReviewStatus.Analyzing };
            db.AddRange(a, b);
            await db.SaveChangesAsync();
            g1 = a.Id; g2 = b.Id;
        }
        await EnqueueAsync(run, "analyze", time, g1);
        await EnqueueAsync(run, "critic_judge", time, g2);
        var worker = Seed.Worker(pg, time,
            new LambdaHandler("analyze", _ => new StepResult.Failed("BAD", "bad")),
            new LambdaHandler("critic_judge", _ => new StepResult.NeedsReview("low confidence", new { c = 0.2 })));

        await worker.RunOnceAsync();
        await worker.RunOnceAsync();

        await using var db2 = pg.CreateDb();
        Assert.Equal(ReviewStatus.NeedsReview, (await db2.GroupReviews.SingleAsync(g => g.Id == g1)).Status);
        Assert.Equal(ReviewStatus.NeedsReview, (await db2.GroupReviews.SingleAsync(g => g.Id == g2)).Status);
        Assert.Contains(await db2.StepRuns.ToListAsync(), s => s.Status == StepStatus.NeedsReview);
        Assert.NotEqual(RunStatus.Failed, (await db2.WorkflowRuns.SingleAsync()).Status);
        Assert.NotEqual(RunStatus.Completed, (await db2.WorkflowRuns.SingleAsync()).Status);
    }
}
