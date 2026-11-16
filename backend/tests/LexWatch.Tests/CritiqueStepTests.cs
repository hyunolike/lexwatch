using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using LexWatch.Api.Workflow.Steps;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

[Collection("pg")]
public class CritiqueStepTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Guid Run, GroupReview Group, Guid Step)> ArrangeAsync()
    {
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id);
        await using (var db = pg.CreateDb())
        {
            db.MemoDrafts.Add(new MemoDraft
            {
                Id = Guid.NewGuid(), GroupReviewId = g.Id, Version = 1, CreatedBy = "agent", OpenQuestionsJson = "[]",
                SentencesJson = """[{"sentence":"해고가 제한된다.","citations":["law:L1:제23조"]}]""",
            });
            await db.SaveChangesAsync();
        }
        var step = await AiSeed.StepAsync(pg, run.Id, "critic_judge", g.Id);
        return (run.Id, g, step.Id);
    }

    [Fact]
    public async Task Findings_are_stored_as_tier3_and_group_needs_review()
    {
        using var ai = new AiStub();
        ai.Json("/critique", 200, new
        {
            findings = new[] { new { sentence_index = 0, rule_id = "OC-01", message = "단정 표현" } },
            provider = AiStub.Provider("fallback", "rules"),
        });
        var (_, g, step) = await ArrangeAsync();

        var ok = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, new CritiqueStep(ai.Gateway()), step));

        Assert.Equal("fallback", ok.ProviderKind);
        Assert.Empty(ok.Next);
        await using var v = pg.CreateDb();
        var f = await v.CriticFindings.SingleAsync();
        Assert.Equal((3, 0, "OC-01"), (f.Tier, f.SentenceIndex, f.RuleId));
        Assert.Equal(ReviewStatus.NeedsReview, (await v.GroupReviews.SingleAsync(x => x.Id == g.Id)).Status);
    }

    [Fact]
    public async Task No_findings_waits_for_approval()
    {
        using var ai = new AiStub();
        ai.Json("/critique", 200, new { findings = new object[0], provider = AiStub.Provider() });
        var (_, g, step) = await ArrangeAsync();

        Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, new CritiqueStep(ai.Gateway()), step));

        await using var v = pg.CreateDb();
        Assert.Empty(await v.CriticFindings.ToListAsync());
        Assert.Equal(ReviewStatus.WaitingApproval, (await v.GroupReviews.SingleAsync(x => x.Id == g.Id)).Status);
    }
}
