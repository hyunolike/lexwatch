using LexWatch.Api.Data.Entities;
using LexWatch.Api.Demo;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

[Collection("pg")]
public class DemoSeederTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static DemoSeeder Seeder(LexWatch.Api.Data.LexWatchDb db) => new(db, TimeProvider.System);

    [Fact]
    public async Task Reset_restores_waiting_reviews_and_replays_have_replay_kind()
    {
        await using var db = pg.CreateDb();
        await Seeder(db).ResetAsync();

        var replays = await db.WorkflowRuns.Where(r => r.IsReplay).ToListAsync();
        Assert.Equal(5, replays.Count);
        var steps = await db.StepRuns.Where(s => replays.Select(r => r.Id).Contains(s.RunId)).ToListAsync();
        Assert.NotEmpty(steps);
        Assert.All(steps, s => Assert.Equal("replay", s.ProviderKind));

        // 다섯 갈래가 타임라인에 드러나는 상태를 갖는다.
        Assert.Contains(steps, s => s is { Status: StepStatus.Failed, ErrorCode: "LLM_RATE_LIMITED" });
        Assert.Contains(steps, s => s is { Status: StepStatus.Succeeded, Step: "analyze", Attempt: 1 });
        Assert.Contains(steps, s => s.Status == StepStatus.NeedsReview);
        Assert.Contains(await db.CriticFindings.ToListAsync(), f => f.Tier == 2);
        var statuses = (await db.GroupReviews.ToListAsync()).Select(g => g.Status).ToHashSet();
        Assert.Contains(ReviewStatus.WaitingApproval, statuses);
        Assert.Contains(ReviewStatus.NeedsReview, statuses);
        Assert.Contains(ReviewStatus.Rejected, statuses);
        Assert.Equal(3, await db.PracticeGroups.CountAsync());

        // 승인·알림·채팅 할당량을 지운다. 두 번 호출해도 같은 상태다.
        var g = await db.GroupReviews.FirstAsync(x => x.Status == ReviewStatus.WaitingApproval);
        db.Approvals.Add(new Approval { Id = Guid.NewGuid(), GroupReviewId = g.Id, Decision = "approve", ActorRole = "lawyer", DecidedAt = DateTimeOffset.UtcNow });
        g.Status = ReviewStatus.Notified;
        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), GroupReviewId = g.Id, Status = "sent", Body = "x" });
        db.ChatQuotas.Add(new ChatQuota { Day = new DateOnly(2026, 11, 16), IpHash = "h", Count = 3 });
        await db.SaveChangesAsync();

        await using var db2 = pg.CreateDb();
        await Seeder(db2).ResetAsync();
        await using var db3 = pg.CreateDb();
        await Seeder(db3).ResetAsync();

        await using var chk = pg.CreateDb();
        Assert.Equal(1, await chk.Approvals.CountAsync()); // 반려 재현 1건만
        Assert.Equal(0, await chk.Notifications.CountAsync());
        Assert.Equal(0, await chk.ChatQuotas.CountAsync());
        Assert.Equal(5, await chk.WorkflowRuns.CountAsync(r => r.IsReplay));
        Assert.Equal(5, await chk.WorkflowRuns.CountAsync());
        Assert.Equal(1, await chk.GroupReviews.CountAsync(x => x.Status == ReviewStatus.Rejected));
    }

    [Fact]
    public async Task Reset_keeps_real_runs()
    {
        var real = await Seed.RunAsync(pg);
        Guid gid, stepId;
        await using (var db = pg.CreateDb())
        {
            var g = new GroupReview { Id = Guid.NewGuid(), RunId = real.Id, GroupCode = "labor", Status = ReviewStatus.Notified };
            var s = new StepRun { Id = Guid.NewGuid(), RunId = real.Id, GroupReviewId = g.Id, Step = "analyze", Status = StepStatus.Succeeded, ProviderKind = "nvidia", NotBefore = DateTimeOffset.UtcNow };
            var pend = new StepRun { Id = Guid.NewGuid(), RunId = real.Id, GroupReviewId = g.Id, Step = "notify", Status = StepStatus.Pending, ProviderKind = "none", NotBefore = DateTimeOffset.UtcNow };
            db.AddRange(g, s, pend,
                new Approval { Id = Guid.NewGuid(), GroupReviewId = g.Id, Decision = "approve", ActorRole = "lawyer", DecidedAt = DateTimeOffset.UtcNow },
                new Notification { Id = Guid.NewGuid(), GroupReviewId = g.Id, Status = "sent", Body = "x" });
            (await db.WorkflowRuns.SingleAsync(r => r.Id == real.Id)).Status = RunStatus.Completed;
            await db.SaveChangesAsync();
            gid = g.Id; stepId = s.Id;
        }

        await using (var db = pg.CreateDb()) await Seeder(db).ResetAsync();

        await using var chk = pg.CreateDb();
        Assert.False((await chk.WorkflowRuns.SingleAsync(r => r.Id == real.Id)).IsReplay);
        Assert.Equal(ReviewStatus.WaitingApproval, (await chk.GroupReviews.SingleAsync(g => g.Id == gid)).Status);
        Assert.Equal(RunStatus.InReview, (await chk.WorkflowRuns.SingleAsync(r => r.Id == real.Id)).Status);
        Assert.NotNull(await chk.StepRuns.SingleOrDefaultAsync(s => s.Id == stepId)); // 실제 이력은 지우지 않는다
        Assert.Equal(0, await chk.StepRuns.CountAsync(s => s.RunId == real.Id && s.Step == "notify")); // 대기 중이던 알림 단계만 치운다
        Assert.Equal(0, await chk.Approvals.CountAsync(a => a.GroupReviewId == gid));
        Assert.Equal(6, await chk.WorkflowRuns.CountAsync());
    }
}
