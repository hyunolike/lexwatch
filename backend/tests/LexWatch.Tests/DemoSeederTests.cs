using LexWatch.Api.Data.Entities;
using LexWatch.Api.Demo;
using LexWatch.Api.Workflow;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

[Collection("pg")]
public class DemoSeederTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static DemoSeeder Seeder(LexWatch.Api.Data.LexWatchDb db) => new(db, TimeProvider.System);

    [Fact]
    public async Task Replay_tier_1_and_2_findings_match_what_the_approval_recheck_finds()
    {
        // 재현 fixture는 크리틱 적발을 미리 적어 둔다. 승인 재검사가 다른 결과를 내면 데모 승인이 엉뚱한 422를 받는다.
        await using var db = pg.CreateDb();
        await Seeder(db).ResetAsync();
        var drafts = await db.MemoDrafts.AsNoTracking().ToListAsync();
        Assert.NotEmpty(drafts);
        foreach (var d in drafts)
        {
            var g = await db.GroupReviews.AsNoTracking().SingleAsync(x => x.Id == d.GroupReviewId);
            var run = await db.WorkflowRuns.AsNoTracking().SingleAsync(r => r.Id == g.RunId);
            var change = await db.LawChanges.AsNoTracking().SingleAsync(l => l.Id == run.LawChangeId);
            var evidence = await db.Evidence.AsNoTracking().Where(e => e.RunId == run.Id).ToListAsync();
            var memo = System.Text.Json.JsonSerializer.Deserialize<List<LexWatch.Api.Ai.MemoSentenceDto>>(d.SentencesJson, LexWatch.Api.Ai.AiMapping.Json)!;
            var stored = await db.CriticFindings.AsNoTracking().Where(f => f.MemoDraftId == d.Id && f.Tier < 3)
                .Select(f => new { f.SentenceIndex, f.Tier, f.RuleId, f.Message }).ToListAsync();
            var recheck = LexWatch.Api.Critic.DraftRecheck.Run(memo, memo, [], evidence, change)
                .Select(f => new { f.SentenceIndex, f.Tier, f.RuleId, f.Message }).ToList();
            Assert.Equal(stored.OrderBy(x => x.SentenceIndex).ThenBy(x => x.Tier).ThenBy(x => x.RuleId), recheck);
        }
    }

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

        // 재현 법령은 실제 공포 법령처럼 보이면 안 된다.
        var laws = await db.LawChanges.ToListAsync();
        Assert.Equal(5, laws.Count);
        Assert.All(laws, l =>
        {
            Assert.StartsWith("[재현 예시] ", l.LawName);
            Assert.Equal("", l.Mst);
            Assert.Null(l.EffectiveDate);
        });
        var bodies = await db.Evidence.Select(e => e.Body).ToListAsync();
        Assert.All(bodies, b => Assert.Contains("재현 예시", b.Replace("예시 구조문", "재현 예시")));

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
    public async Task Reset_leaves_real_groups_decisions_and_drafts_untouched()
    {
        var real = await Seed.RunAsync(pg);
        Guid flagged, notified, waiting, stepId, notifyId;
        await using (var db = pg.CreateDb())
        {
            // 크리틱에 걸려 검토 필요였다가 반려된 그룹, 알림까지 끝난 그룹, 승인 뒤 알림 대기 그룹
            var f = new GroupReview { Id = Guid.NewGuid(), RunId = real.Id, GroupCode = "labor", Status = ReviewStatus.Rejected, Confidence = 0.5 };
            var n = new GroupReview { Id = Guid.NewGuid(), RunId = real.Id, GroupCode = "privacy", Status = ReviewStatus.Notified };
            var w = new GroupReview { Id = Guid.NewGuid(), RunId = real.Id, GroupCode = "finance", Status = ReviewStatus.Approved };
            var draft = new MemoDraft { Id = Guid.NewGuid(), GroupReviewId = f.Id, Version = 1, CreatedBy = "agent", SentencesJson = "[]", OpenQuestionsJson = "[]" };
            var edit = new MemoDraft { Id = Guid.NewGuid(), GroupReviewId = n.Id, Version = 2, CreatedBy = "lawyer", SentencesJson = "[]", OpenQuestionsJson = "[]" };
            var s = new StepRun { Id = Guid.NewGuid(), RunId = real.Id, GroupReviewId = f.Id, Step = "analyze", Status = StepStatus.Succeeded, ProviderKind = "nvidia", NotBefore = DateTimeOffset.UtcNow };
            var pend = new StepRun { Id = Guid.NewGuid(), RunId = real.Id, GroupReviewId = w.Id, Step = "notify", Status = StepStatus.Pending, ProviderKind = "none", NotBefore = DateTimeOffset.UtcNow };
            db.AddRange(f, n, w, draft, edit, s, pend,
                new CriticFinding { Id = Guid.NewGuid(), MemoDraftId = draft.Id, Tier = 3, SentenceIndex = 0, RuleId = "OC-01", Message = "단정" },
                new Approval { Id = Guid.NewGuid(), GroupReviewId = f.Id, Decision = "reject", ActorRole = "lawyer", Comment = "근거 부족", DecidedAt = DateTimeOffset.UtcNow },
                new Approval { Id = Guid.NewGuid(), GroupReviewId = n.Id, Decision = "approve", ActorRole = "lawyer", DecidedAt = DateTimeOffset.UtcNow },
                new Approval { Id = Guid.NewGuid(), GroupReviewId = w.Id, Decision = "approve", ActorRole = "lawyer", DecidedAt = DateTimeOffset.UtcNow },
                new Notification { Id = Guid.NewGuid(), GroupReviewId = n.Id, Status = "sent", Body = "x" },
                new Notification { Id = Guid.NewGuid(), Channel = "admin", Status = "pending", Body = "수집 0건" });
            (await db.WorkflowRuns.SingleAsync(r => r.Id == real.Id)).Status = RunStatus.InReview;
            await db.SaveChangesAsync();
            flagged = f.Id; notified = n.Id; waiting = w.Id; stepId = s.Id; notifyId = pend.Id;
        }

        for (var i = 0; i < 2; i++) // 멱등
            await using (var db = pg.CreateDb()) await Seeder(db).ResetAsync();

        await using var chk = pg.CreateDb();
        Assert.False((await chk.WorkflowRuns.SingleAsync(r => r.Id == real.Id)).IsReplay);
        Assert.Equal(RunStatus.InReview, (await chk.WorkflowRuns.SingleAsync(r => r.Id == real.Id)).Status);
        Assert.Equal(ReviewStatus.Rejected, (await chk.GroupReviews.SingleAsync(g => g.Id == flagged)).Status);
        Assert.Equal(ReviewStatus.Notified, (await chk.GroupReviews.SingleAsync(g => g.Id == notified)).Status);
        Assert.Equal(ReviewStatus.Approved, (await chk.GroupReviews.SingleAsync(g => g.Id == waiting)).Status);
        Assert.Equal(3, await chk.Approvals.CountAsync(a => a.GroupReviewId == flagged || a.GroupReviewId == notified || a.GroupReviewId == waiting));
        Assert.Equal(1, await chk.CriticFindings.CountAsync(x => x.RuleId == "OC-01"));
        Assert.Equal(2, await chk.MemoDrafts.CountAsync(d => d.GroupReviewId == flagged || d.GroupReviewId == notified));
        Assert.Equal(1, await chk.Notifications.CountAsync(x => x.GroupReviewId == notified));
        Assert.Equal(1, await chk.Notifications.CountAsync(x => x.Channel == "admin"));
        Assert.NotNull(await chk.StepRuns.SingleOrDefaultAsync(s => s.Id == stepId));
        Assert.NotNull(await chk.StepRuns.SingleOrDefaultAsync(s => s.Id == notifyId)); // 실제 알림 단계도 그대로 진행된다
        Assert.Equal(6, await chk.WorkflowRuns.CountAsync());
    }
}
