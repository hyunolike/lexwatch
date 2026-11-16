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
                [new NextStep("fetch_articles", null, new { a = 1 }), new NextStep("classify", null, null)], "nvidia", "law")),
            new LambdaHandler("fetch_articles", _ => new StepResult.Succeeded(null, [])),
            new LambdaHandler("classify", _ => new StepResult.Succeeded(null, [])));

        await worker.RunOnceAsync();

        await using (var db = pg.CreateDb())
        {
            var f = await db.StepRuns.SingleAsync(s => s.Id == first.Id);
            Assert.Equal(StepStatus.Succeeded, f.Status);
            Assert.Equal("nvidia", f.ProviderKind);
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

    [Fact]
    public async Task Needs_review_persists_provider_and_rejects_unknown_kind()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var gid = (await AiSeed.GroupAsync(pg, run.Id)).Id;
        var ok = await EnqueueAsync(run, "analyze", time, gid);
        var bad = await EnqueueAsync(run, "critic_judge", time);
        var worker = Seed.Worker(pg, time,
            new LambdaHandler("analyze", _ => new StepResult.NeedsReview("x", null, "nvidia", "nim")),
            new LambdaHandler("critic_judge", _ => new StepResult.NeedsReview("x", null, "bogus", null)));

        await worker.RunOnceAsync();
        await worker.RunOnceAsync();

        var a = await LoadAsync(ok.Id);
        Assert.Equal((StepStatus.NeedsReview, "nvidia", "nim"), (a.Status, a.ProviderKind, a.ProviderName));
        var b = await LoadAsync(bad.Id);
        Assert.NotEqual("bogus", b.ProviderKind);
    }

    [Fact]
    public async Task Stale_worker_result_is_discarded_after_reclaim()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var step = await EnqueueAsync(run, "collect", time);
        var worker = Seed.Worker(pg, time, new LambdaHandler("collect", _ =>
        {
            // 리스가 만료되고 다른 작업자(B)가 재선점한다.
            time.Now = T0.AddMinutes(6);
            using var dbB = pg.CreateDb();
            var b = new StepQueue(dbB, time).ClaimNextAsync(TimeSpan.FromMinutes(5)).GetAwaiter().GetResult();
            Assert.Equal(2, b!.Attempt);
            return new StepResult.Succeeded(null, [new NextStep("classify", null, null)]);
        }));

        Assert.True(await worker.RunOnceAsync());

        await using var db = pg.CreateDb();
        var s = await db.StepRuns.SingleAsync();
        Assert.Equal(step.Id, s.Id);
        Assert.Equal(StepStatus.Running, s.Status);
        Assert.Equal(2, s.Attempt);
        Assert.Equal(1, await db.StepRuns.CountAsync());
    }

    [Fact]
    public async Task Unknown_provider_kind_is_rejected_and_retried()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var step = await EnqueueAsync(run, "collect", time);
        var worker = Seed.Worker(pg, time,
            new LambdaHandler("collect", _ => new StepResult.Succeeded(null, [new NextStep("classify", null, null)], "api")));

        await worker.RunOnceAsync();

        var s = await LoadAsync(step.Id);
        Assert.Equal(StepStatus.RetryScheduled, s.Status);
        Assert.Equal("HANDLER_EXCEPTION", s.ErrorCode);
        await using var db = pg.CreateDb();
        Assert.Equal(1, await db.StepRuns.CountAsync());
    }

    [Fact]
    public async Task Notify_failure_keeps_group_approved_alerts_admin_and_blocks_second_approval()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var g = await AiSeed.GroupAsync(pg, run.Id, "labor", ReviewStatus.Approved);
        var step = await EnqueueAsync(run, "notify", time, g.Id);
        var worker = Seed.Worker(pg, time, new LambdaHandler("notify", _ => new StepResult.Failed("NO_DRAFT", "알릴 초안이 없다")));

        await worker.RunOnceAsync();

        await using var db = pg.CreateDb();
        Assert.Equal(ReviewStatus.Approved, (await db.GroupReviews.SingleAsync()).Status);
        var s = await db.StepRuns.SingleAsync(x => x.Id == step.Id);
        Assert.Equal((StepStatus.Failed, "NO_DRAFT"), (s.Status, s.ErrorCode));
        var alert = await db.Notifications.SingleAsync();
        Assert.Equal(("admin", g.Id), (alert.Channel, alert.GroupReviewId));
        Assert.Equal(RunStatus.Failed, (await db.WorkflowRuns.SingleAsync()).Status); // 완료로 보이지 않는다
    }

    [Fact]
    public async Task Notify_exhausting_retries_keeps_group_approved()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var g = await AiSeed.GroupAsync(pg, run.Id, "labor", ReviewStatus.Approved);
        var step = await EnqueueAsync(run, "notify", time, g.Id);
        var worker = Seed.Worker(pg, time, new LambdaHandler("notify", _ => throw new InvalidOperationException("smtp down")));

        for (var i = 0; i < 10 && (await LoadAsync(step.Id)).Status != StepStatus.Failed; i++)
        {
            time.Now = time.Now.AddHours(1);
            await worker.RunOnceAsync();
        }

        Assert.Equal(StepStatus.Failed, (await LoadAsync(step.Id)).Status);
        await using var db = pg.CreateDb();
        Assert.Equal(ReviewStatus.Approved, (await db.GroupReviews.SingleAsync()).Status);
    }

    [Fact]
    public async Task Run_level_needs_review_fails_the_run_instead_of_completing_it()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var step = await EnqueueAsync(run, "classify", time);
        var worker = Seed.Worker(pg, time,
            new LambdaHandler("classify", _ => new StepResult.NeedsReview("LLM_OUTPUT_INVALID", null, "nvidia", "nim")));

        await worker.RunOnceAsync();

        var s = await LoadAsync(step.Id);
        Assert.Equal((StepStatus.Failed, "LLM_OUTPUT_INVALID", "nvidia"), (s.Status, s.ErrorCode, s.ProviderKind));
        await using var db = pg.CreateDb();
        var r = await db.WorkflowRuns.SingleAsync();
        Assert.Equal(RunStatus.Failed, r.Status);
        Assert.NotNull(r.FinishedAt);
        Assert.Empty(await db.GroupReviews.ToListAsync());
    }

    [Fact]
    public async Task Run_waiting_only_on_lawyer_becomes_in_review()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        var g = await AiSeed.GroupAsync(pg, run.Id, "labor", ReviewStatus.Analyzing);
        await EnqueueAsync(run, "critic_judge", time, g.Id);
        var worker = Seed.Worker(pg, time, new LambdaHandler("critic_judge", ctx =>
        {
            ctx.Db.GroupReviews.Single(x => x.Id == g.Id).Status = ReviewStatus.WaitingApproval;
            return new StepResult.Succeeded(null, []);
        }));

        await worker.RunOnceAsync();

        await using var db = pg.CreateDb();
        Assert.Equal(RunStatus.InReview, (await db.WorkflowRuns.SingleAsync()).Status);
    }
}
