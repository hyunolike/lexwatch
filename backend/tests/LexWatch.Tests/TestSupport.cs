using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LexWatch.Tests;

public sealed class FakeTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class LambdaHandler(string step, Func<StepContext, StepResult> fn) : IStepHandler
{
    public string Step => step;
    public Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct) => Task.FromResult(fn(ctx));
}

public static class Seed
{
    public static async Task<WorkflowRun> RunAsync(PostgresFixture pg)
    {
        await using var db = pg.CreateDb();
        var lc = new LawChange
        {
            Id = Guid.NewGuid(), LawId = "L1", LawName = "근로기준법", PromulgationDate = new DateOnly(2026, 11, 1),
            RawJson = "{}", RawSha256 = "x", FetchedAt = DateTimeOffset.UtcNow, IdempotencyKey = Guid.NewGuid().ToString(),
        };
        var run = new WorkflowRun { Id = Guid.NewGuid(), LawChangeId = lc.Id, Status = RunStatus.Queued, TraceId = "t", CreatedAt = DateTimeOffset.UtcNow };
        db.AddRange(lc, run);
        await db.SaveChangesAsync();
        return run;
    }

    public static WorkflowWorker Worker(PostgresFixture pg, TimeProvider time, params IStepHandler[] handlers)
    {
        var sc = new ServiceCollection();
        sc.AddScoped(_ => pg.CreateDb());
        foreach (var h in handlers) sc.AddSingleton(h);
        var sp = sc.BuildServiceProvider();
        return new WorkflowWorker(sp.GetRequiredService<IServiceScopeFactory>(), time, NullLogger<WorkflowWorker>.Instance);
    }
}
