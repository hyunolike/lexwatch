using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

[Collection("pg")]
public class StepQueueTests(PostgresFixture pg) : IAsyncLifetime
{
    public async Task InitializeAsync() => await pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Two_concurrent_claimers_never_get_the_same_step()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        await using (var db = pg.CreateDb())
        {
            var q = new StepQueue(db, time);
            for (var i = 0; i < 20; i++) await q.EnqueueAsync(run.Id, new NextStep("collect", null, null));
        }

        var claimed = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        var workers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await using var db = pg.CreateDb();
            var q = new StepQueue(db, time);
            while (await q.ClaimNextAsync(TimeSpan.FromMinutes(5)) is { } s) claimed.Add(s.Id);
        }));
        await Task.WhenAll(workers);

        Assert.Equal(20, claimed.Count);
        Assert.Equal(20, claimed.Distinct().Count());
    }

    [Fact]
    public async Task Expired_lease_running_step_is_reclaimed()
    {
        var run = await Seed.RunAsync(pg);
        var time = new FakeTime(T0);
        Guid id;
        await using (var db = pg.CreateDb())
        {
            var s = StepQueue.Create(run.Id, new NextStep("collect", null, null), T0);
            s.Status = StepStatus.Running;
            s.Attempt = 1;
            s.LeaseUntil = T0.AddSeconds(-1);
            db.StepRuns.Add(s);
            await db.SaveChangesAsync();
            id = s.Id;
        }

        await using var db2 = pg.CreateDb();
        var claimed = await new StepQueue(db2, time).ClaimNextAsync(TimeSpan.FromMinutes(5));

        Assert.NotNull(claimed);
        Assert.Equal(id, claimed!.Id);
        Assert.Equal(2, claimed.Attempt);
        Assert.Equal(StepStatus.Running, claimed.Status);
        Assert.Equal(T0.AddMinutes(5), claimed.LeaseUntil);
    }

    [Fact]
    public async Task Unexpired_lease_running_step_is_not_reclaimed()
    {
        var run = await Seed.RunAsync(pg);
        await using (var db = pg.CreateDb())
        {
            var s = StepQueue.Create(run.Id, new NextStep("collect", null, null), T0);
            s.Status = StepStatus.Running;
            s.LeaseUntil = T0.AddSeconds(30);
            db.StepRuns.Add(s);
            await db.SaveChangesAsync();
        }
        await using var db2 = pg.CreateDb();
        Assert.Null(await new StepQueue(db2, new FakeTime(T0)).ClaimNextAsync(TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Not_before_in_future_is_not_claimed()
    {
        var run = await Seed.RunAsync(pg);
        await using (var db = pg.CreateDb())
        {
            var s = StepQueue.Create(run.Id, new NextStep("collect", null, null), T0.AddSeconds(10));
            db.StepRuns.Add(s);
            await db.SaveChangesAsync();
        }
        var time = new FakeTime(T0);
        await using var db2 = pg.CreateDb();
        var q = new StepQueue(db2, time);
        Assert.Null(await q.ClaimNextAsync(TimeSpan.FromMinutes(5)));
        time.Now = T0.AddSeconds(11);
        Assert.NotNull(await q.ClaimNextAsync(TimeSpan.FromMinutes(5)));
    }
}
