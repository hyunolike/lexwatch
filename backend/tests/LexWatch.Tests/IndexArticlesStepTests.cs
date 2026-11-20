using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using LexWatch.Api.Workflow.Steps;
using Microsoft.EntityFrameworkCore;
using EvidenceRow = LexWatch.Api.Data.Entities.Evidence;

namespace LexWatch.Tests;

[Collection("pg")]
public class IndexArticlesStepTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static IndexArticlesStep Step(AiStub ai) => new(ai.Gateway(), TimeProvider.System);

    private static void StubEmbed(AiStub ai, int dim = LexWatchDb.ChunkEmbedDim, string kind = "cloudflare", int status = 200) =>
        ai.Json("/embed", status, status == 200
            ? new { vectors = Enumerable.Range(0, 2).Select(i => AiStub.Unit(i, dim)).ToArray(), provider = AiStub.Provider(kind, "@cf/baai/bge-m3") }
            : new { code = "RETRIEVER_ERROR", message = "x" });

    private async Task<(Guid Run, Guid Step, GroupReview Open, GroupReview Closed)> ArrangeAsync()
    {
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);   // law:L1:제23조(law), oldnew:L1:제23조(oldnew)
        await using (var db = pg.CreateDb())
        {
            db.Evidence.Add(new EvidenceRow { RunId = run.Id, EvidenceId = "law:L1:제2조", Kind = "law", Title = "근로기준법 제2조(정의)", Body = "제2조(정의) 이 법에서 사용하는 용어의 뜻은 다음과 같다.", FetchedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var open = await AiSeed.GroupAsync(pg, run.Id, "labor");
        var closed = await AiSeed.GroupAsync(pg, run.Id, "finance", ReviewStatus.ClosedIrrelevant);
        var step = await AiSeed.StepAsync(pg, run.Id, "index_articles");
        return (run.Id, step.Id, open, closed);
    }

    [Fact]
    public async Task Indexes_law_articles_with_embeddings_and_fans_out_analyze()
    {
        using var ai = new AiStub();
        StubEmbed(ai);
        var (run, step, open, _) = await ArrangeAsync();

        var ok = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, Step(ai), step));

        await using var v = pg.CreateDb();
        var chunks = await v.ArticleChunks.Where(c => c.RunId == run).OrderBy(c => c.Ord).ToListAsync();
        Assert.Equal(["law:L1:제2조#1", "law:L1:제23조#1"], chunks.Select(c => c.ChunkId));   // 자연 정렬, oldnew 제외
        Assert.All(chunks, c => Assert.Equal(("@cf/baai/bge-m3", LexWatchDb.ChunkEmbedDim), (c.EmbedModel, c.Embedding!.ToArray().Length)));
        Assert.Contains("사용하는 용어", chunks[0].Text);
        Assert.NotEmpty(chunks[0].Tokens);
        Assert.Equal("cloudflare", ok.ProviderKind);
        var next = Assert.Single(ok.Next);
        Assert.Equal(("analyze", (Guid?)open.Id), (next.Step, next.GroupReviewId));
        Assert.Equal("passage", ai.Requests("/embed").Single().RootElement.GetProperty("input_type").GetString());
    }

    [Theory]
    [InlineData(200, 256, "fallback", "fallback_provider")]
    [InlineData(200, 2048, "nvidia", "dim_mismatch")]
    [InlineData(502, 0, "", "embed_failed")]
    public async Task Embedding_problems_degrade_to_keyword_index(int status, int dim, string kind, string reason)
    {
        using var ai = new AiStub();
        StubEmbed(ai, dim == 0 ? LexWatchDb.ChunkEmbedDim : dim, kind == "" ? "cloudflare" : kind, status);
        var (run, step, _, _) = await ArrangeAsync();

        var ok = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, Step(ai), step));

        await using var v = pg.CreateDb();
        var chunks = await v.ArticleChunks.Where(c => c.RunId == run).ToListAsync();
        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, c => Assert.Null(c.Embedding));
        Assert.Equal("fallback", ok.ProviderKind);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Output);
        Assert.Contains("\"unavailable\"", json);
        Assert.Contains(reason, json);
        Assert.Single(ok.Next);
    }

    [Fact]
    public async Task Running_twice_keeps_one_copy_of_each_chunk()
    {
        using var ai = new AiStub();
        StubEmbed(ai);
        var (run, step, _, _) = await ArrangeAsync();

        await AiSeed.ExecuteAsync(pg, Step(ai), step);
        await AiSeed.ExecuteAsync(pg, Step(ai), step);

        await using var v = pg.CreateDb();
        Assert.Equal(2, await v.ArticleChunks.CountAsync(c => c.RunId == run));
    }
}
