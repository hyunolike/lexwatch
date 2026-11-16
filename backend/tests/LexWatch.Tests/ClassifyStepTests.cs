using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using LexWatch.Api.Workflow.Steps;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

[Collection("pg")]
public class ClassifyStepTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly FakeTime Time = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));

    private static void StubEmbed(AiStub ai, int dim = 2048, string kind = "nvidia")
    {
        ai.Json("/embed", 200, new { vectors = new[] { AiStub.Unit(0, dim) }, provider = AiStub.Provider(kind) },
            partialMatch: new { input_type = "query" });
        ai.Json("/embed", 200,
            new { vectors = new[] { AiStub.Unit(0, dim), AiStub.Unit(1, dim), AiStub.Unit(2, dim) }, provider = AiStub.Provider(kind) },
            partialMatch: new { input_type = "passage" });
    }

    private static void StubClassify(AiStub ai, double labor, double privacy, double finance, string kind = "nvidia") =>
        ai.Json("/classify", 200, new
        {
            groups = new object[]
            {
                new { group_code = "labor", relevant = labor >= 0.4, confidence = labor, reason = "r1" },
                new { group_code = "privacy", relevant = privacy >= 0.4, confidence = privacy, reason = "r2" },
                new { group_code = "finance", relevant = finance >= 0.4, confidence = finance, reason = "r3" },
            },
            provider = AiStub.Provider(kind),
        });

    private ClassifyStep Step(AiStub ai) => new(ai.Gateway(), Time);

    [Fact]
    public async Task Confidence_thresholds_create_reviews_and_close_low()
    {
        using var ai = new AiStub();
        StubEmbed(ai);
        StubClassify(ai, 0.8, 0.5, 0.2);
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var step = await AiSeed.StepAsync(pg, run.Id, "classify");

        var result = await AiSeed.ExecuteAsync(pg, Step(ai), step.Id);

        var ok = Assert.IsType<StepResult.Succeeded>(result);
        Assert.Equal("nvidia", ok.ProviderKind);
        Assert.Equal(2, ok.Next.Count);
        Assert.All(ok.Next, n => Assert.Equal("analyze", n.Step));

        await using var v = pg.CreateDb();
        var reviews = await v.GroupReviews.Where(g => g.RunId == run.Id).ToDictionaryAsync(g => g.GroupCode);
        Assert.Equal(ReviewStatus.Analyzing, reviews["labor"].Status);
        Assert.Equal(ReviewStatus.NeedsReview, reviews["privacy"].Status);
        Assert.Equal(ReviewStatus.ClosedIrrelevant, reviews["finance"].Status);
        Assert.Equal(0.8, reviews["labor"].Confidence);
        Assert.Equal("r1", reviews["labor"].Reason);
        Assert.Equal(
            new[] { reviews["labor"].Id, reviews["privacy"].Id }.Order(),
            ok.Next.Select(n => n.GroupReviewId!.Value).Order());
        Assert.Equal(RunStatus.InReview, (await v.WorkflowRuns.SingleAsync(r => r.Id == run.Id)).Status);
    }

    [Fact]
    public async Task Not_relevant_verdict_never_proceeds_even_with_high_confidence()
    {
        using var ai = new AiStub();
        StubEmbed(ai);
        ai.Json("/classify", 200, new
        {
            groups = new object[]
            {
                new { group_code = "labor", relevant = false, confidence = 0.9, reason = "관련 없음 확신" },
                new { group_code = "privacy", relevant = true, confidence = 0.8, reason = "r" },
                new { group_code = "finance", relevant = false, confidence = 0.1, reason = "r" },
            },
            provider = AiStub.Provider(),
        });
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var step = await AiSeed.StepAsync(pg, run.Id, "classify");

        var ok = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, Step(ai), step.Id));

        Assert.Single(ok.Next);
        await using var v = pg.CreateDb();
        var labor = await v.GroupReviews.SingleAsync(g => g.GroupCode == "labor");
        Assert.Equal(ReviewStatus.ClosedIrrelevant, labor.Status);
        Assert.True(labor.Confidence < 0.4);
        Assert.Contains("modelConfidence", System.Text.Json.JsonSerializer.Serialize(ok.Output));
    }

    [Fact]
    public async Task All_below_threshold_closes_run_without_next_steps()
    {
        using var ai = new AiStub();
        StubEmbed(ai);
        StubClassify(ai, 0.1, 0.2, 0.3);
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var step = await AiSeed.StepAsync(pg, run.Id, "classify");

        var ok = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, Step(ai), step.Id));

        Assert.Empty(ok.Next);
        await using var v = pg.CreateDb();
        var r = await v.WorkflowRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(RunStatus.ClosedIrrelevant, r.Status);
        Assert.NotNull(r.FinishedAt);
        Assert.All(await v.GroupReviews.ToListAsync(), g => Assert.Equal(ReviewStatus.ClosedIrrelevant, g.Status));
    }

    [Fact]
    public async Task Missing_group_embeddings_are_filled_lazily_and_candidates_carry_similarity()
    {
        using var ai = new AiStub();
        StubEmbed(ai);
        StubClassify(ai, 0.8, 0.8, 0.8);
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var step = await AiSeed.StepAsync(pg, run.Id, "classify");

        await AiSeed.ExecuteAsync(pg, Step(ai), step.Id);

        await using var v = pg.CreateDb();
        Assert.All(await v.PracticeGroups.ToListAsync(), g => Assert.Equal(2048, g.Embedding!.ToArray().Length));

        var req = ai.Requests("/classify").Single().RootElement;
        Assert.Equal("100003", req.GetProperty("law").GetProperty("mst").GetString());
        var sims = req.GetProperty("candidates").EnumerateArray()
            .ToDictionary(c => c.GetProperty("group_code").GetString()!, c => c.GetProperty("similarity").GetDouble());
        Assert.Equal(3, sims.Count);
        Assert.Equal(1.0, sims["finance"] + sims["labor"] + sims["privacy"], 3); // 단위 벡터 셋: 질의와 같은 그룹 하나만 1
        // 질의 입력은 법령명 + 신구조문 앞 2000자
        var query = ai.Requests("/embed").First(d => d.RootElement.GetProperty("input_type").GetString() == "query")
            .RootElement.GetProperty("texts")[0].GetString()!;
        Assert.StartsWith("근로기준법", query);
        Assert.True(query.Length <= "근로기준법".Length + 2000 + 4);
    }

    [Fact]
    public async Task Mismatched_dimension_vectors_are_not_stored()
    {
        using var ai = new AiStub();
        StubEmbed(ai, dim: 16, kind: "fallback");
        StubClassify(ai, 0.8, 0.5, 0.2, kind: "fallback");
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var step = await AiSeed.StepAsync(pg, run.Id, "classify");

        var ok = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, Step(ai), step.Id));

        Assert.Equal("fallback", ok.ProviderKind);
        await using var v = pg.CreateDb();
        Assert.All(await v.PracticeGroups.ToListAsync(), g => Assert.Null(g.Embedding));
        var sims = ai.Requests("/classify").Single().RootElement.GetProperty("candidates").EnumerateArray()
            .Select(c => c.GetProperty("similarity").GetDouble()).ToList();
        Assert.Equal(3, sims.Count);
    }

    [Fact]
    public async Task Ai_failure_returns_retry_and_creates_no_reviews()
    {
        using var ai = new AiStub();
        StubEmbed(ai);
        ai.Json("/classify", 429, new { code = "LLM_RATE_LIMITED", retry_after_s = 5 });
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var step = await AiSeed.StepAsync(pg, run.Id, "classify");

        var result = await AiSeed.ExecuteAsync(pg, Step(ai), step.Id);

        Assert.IsType<StepResult.Retry>(result);
        await using var v = pg.CreateDb();
        Assert.Empty(await v.GroupReviews.ToListAsync());
    }
}
