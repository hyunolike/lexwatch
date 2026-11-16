using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using LexWatch.Api.Workflow.Steps;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

[Collection("pg")]
public class NotifyStepTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Notify_stores_demo_inbox_message_and_marks_notified()
    {
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id, "labor", ReviewStatus.Approved);
        await using (var db = pg.CreateDb())
        {
            db.MemoDrafts.Add(new MemoDraft { Id = Guid.NewGuid(), GroupReviewId = g.Id, Version = 1, CreatedBy = "agent",
                OpenQuestionsJson = "[]", SentencesJson = """[{"sentence":"가나다.","citations":["law:L1:제23조"]}]""" });
            db.Approvals.Add(new Approval { Id = Guid.NewGuid(), GroupReviewId = g.Id, Decision = "approve", ActorRole = "lawyer",
                DecidedAt = new DateTimeOffset(2026, 11, 16, 0, 30, 0, TimeSpan.Zero) });
            await db.SaveChangesAsync();
        }
        var step = await AiSeed.StepAsync(pg, run.Id, "notify", g.Id);
        var r = await AiSeed.ExecuteAsync(pg, new NotifyStep(), step.Id);
        Assert.IsType<StepResult.Succeeded>(r);
        await using var v = pg.CreateDb();
        var n = await v.Notifications.SingleAsync();
        Assert.Equal("demo_inbox", n.Channel);
        Assert.Equal("[노동] 근로기준법 개정 안내\n가나다.\n(승인: 변호사, 2026-11-16 09:30)", n.Body);
        Assert.Equal(ReviewStatus.Notified, (await v.GroupReviews.SingleAsync()).Status);
    }
}
