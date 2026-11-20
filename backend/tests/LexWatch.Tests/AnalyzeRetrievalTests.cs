using System.Text.Json;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Retrieval;
using LexWatch.Api.Workflow;
using LexWatch.Api.Workflow.Steps;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using EvidenceRow = LexWatch.Api.Data.Entities.Evidence;

namespace LexWatch.Tests;

/// <summary>분석 입력 축소(hybrid-rag RAG-04, RAG-07).</summary>
[Collection("pg")]
public class AnalyzeRetrievalTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly FakeTime Time = new(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));

    private static void StubAnalyze(AiStub ai) => ai.Json("/analyze", 200, new
    {
        status = "ok",
        memo = new[] { new { sentence = "해고가 제한될 수 있다.", citations = new[] { "oldnew:L1:제23조" } } },
        open_questions = new string[0], tool_calls = new object[0], new_evidence = new object[0],
        provider = AiStub.Provider("cloudflare", "cf"),
    });

    private static EvidenceRow Law(Guid run, int n, string body, string law = "근로기준법") => new()
    {
        RunId = run, EvidenceId = $"law:L1:제{n}조", Kind = "law", Title = $"{law} 제{n}조", Body = body, FetchedAt = DateTimeOffset.UtcNow,
    };

    private static EvidenceRow OldNew(Guid run, int n, string title = "해고 등의 제한") => new()
    {
        RunId = run, EvidenceId = $"oldnew:L1:제{n}조", Kind = "oldnew", Title = $"근로기준법 제{n}조({title}) 신구조문 대비",
        Body = $"[구조문]\n제{n}조 옛 내용\n\n[신조문]\n제{n}조 사용자는 정당한 이유 없이 해고하지 못한다.", FetchedAt = DateTimeOffset.UtcNow,
        ChangeMarksJson = """{"old":[],"new":[[5,24]]}""",
    };

    /// <summary>근로시간·임금 조문 30개(제1~30조), 그중 제23조는 변경 조문과 같다. index=true면 색인한다.</summary>
    private async Task<(Guid Run, Guid Step)> ArrangeAsync(bool index, int oldnewCount = 1, float[]? chunkVector = null, bool unrelated = false)
    {
        var run = await Seed.RunAsync(pg);
        await using (var db = pg.CreateDb())
        {
            for (var n = 1; n <= 30; n++)
                db.Evidence.Add(Law(run.Id, n, unrelated ? $"zzz {n}" : n == 23 ? "사용자는 근로자에게 정당한 이유 없이 해고하지 못한다." : $"제{n}조 근로시간과 임금에 관한 규정 {n}", unrelated ? "zzz" : "근로기준법"));
            for (var n = 1; n <= oldnewCount; n++) db.Evidence.Add(OldNew(run.Id, n == 1 ? 23 : 100 + n));
            await db.SaveChangesAsync();
            if (index)
            {
                var chunks = ArticleIndexer.Build(run.Id, await db.Evidence.Where(e => e.RunId == run.Id).ToListAsync());
                foreach (var c in chunks) { if (chunkVector is not null) { c.Embedding = new Vector(chunkVector); c.EmbedModel = "m"; } }
                db.ArticleChunks.AddRange(chunks);
                await db.SaveChangesAsync();
            }
        }
        var g = await AiSeed.GroupAsync(pg, run.Id);
        var step = await AiSeed.StepAsync(pg, run.Id, "analyze", g.Id);
        return (run.Id, step.Id);
    }

    private static List<string> SentIds(AiStub ai) => ai.Requests("/analyze").Single().RootElement.GetProperty("evidence")
        .EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToList();

    private static JsonElement Retrieval(StepResult r) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<StepResult.Succeeded>(r).Output)).RootElement.GetProperty("retrieval");

    [Fact]
    public async Task Without_index_sends_everything_and_says_so()
    {
        using var ai = new AiStub();
        StubAnalyze(ai);
        var (_, step) = await ArrangeAsync(index: false);

        var r = await AiSeed.ExecuteAsync(pg, new AnalyzeStep(ai.Gateway(), Time), step);

        Assert.Equal(31, SentIds(ai).Count);
        Assert.Equal("no_index", Retrieval(r).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sends_changed_articles_then_top_search_hits_without_duplicates()
    {
        using var ai = new AiStub();
        StubAnalyze(ai);
        var (_, step) = await ArrangeAsync(index: true);

        var r = await AiSeed.ExecuteAsync(pg, new AnalyzeStep(ai.Gateway(), Time), step);

        var sent = SentIds(ai);
        Assert.Equal("oldnew:L1:제23조", sent[0]);
        Assert.Equal(1 + AnalyzeStep.SearchCount, sent.Count);
        Assert.DoesNotContain("law:L1:제23조", sent);                 // 변경 조문과 같은 조문은 검색에서 뺀다
        Assert.Empty(ai.Requests("/embed"));                           // 청크에 임베딩이 없으면 질의 임베딩도 하지 않는다
        var ret = Retrieval(r);
        Assert.Equal(("ok", false, 9), (ret.GetProperty("status").GetString(), ret.GetProperty("denseUsed").GetBoolean(), ret.GetProperty("sent").GetInt32()));
        Assert.True(JsonSerializer.Serialize(Assert.IsType<StepResult.Succeeded>(r).Output).Length < 300);
    }

    [Fact]
    public async Task Uses_dense_path_when_query_embedding_matches_chunk_model()
    {
        using var ai = new AiStub();
        StubAnalyze(ai);
        var unit = AiStub.Unit(0, LexWatchDb.ChunkEmbedDim);
        ai.Json("/embed", 200, new { vectors = new[] { unit }, provider = AiStub.Provider("cloudflare", "m") });
        var (_, step) = await ArrangeAsync(index: true, chunkVector: unit);

        var r = await AiSeed.ExecuteAsync(pg, new AnalyzeStep(ai.Gateway(), Time), step);

        Assert.True(Retrieval(r).GetProperty("denseUsed").GetBoolean());
        Assert.Equal("query", ai.Requests("/embed").Single().RootElement.GetProperty("input_type").GetString());
    }

    [Fact]
    public async Task Query_embedding_failure_falls_back_to_keywords()
    {
        using var ai = new AiStub();
        StubAnalyze(ai);
        ai.Json("/embed", 502, new { code = "RETRIEVER_ERROR", message = "x" });
        var (_, step) = await ArrangeAsync(index: true, chunkVector: AiStub.Unit(0, LexWatchDb.ChunkEmbedDim));

        var r = await AiSeed.ExecuteAsync(pg, new AnalyzeStep(ai.Gateway(), Time), step);

        Assert.False(Retrieval(r).GetProperty("denseUsed").GetBoolean());
        Assert.Equal(1 + AnalyzeStep.SearchCount, SentIds(ai).Count);
    }

    [Fact]
    public async Task Caps_changed_articles_in_natural_order()
    {
        using var ai = new AiStub();
        StubAnalyze(ai);
        var (_, step) = await ArrangeAsync(index: true, oldnewCount: 15);

        var r = await AiSeed.ExecuteAsync(pg, new AnalyzeStep(ai.Gateway(), Time), step);

        var oldnew = SentIds(ai).Where(i => i.StartsWith("oldnew:")).ToList();
        Assert.Equal(AnalyzeStep.MaxOldNew, oldnew.Count);
        Assert.Equal(oldnew.OrderBy(i => i, ArticleOrder.Comparer), oldnew);
        Assert.True(Retrieval(r).GetProperty("oldnewTruncated").GetBoolean());
    }

    [Fact]
    public async Task Nothing_found_and_no_changes_sends_everything()
    {
        using var ai = new AiStub();
        StubAnalyze(ai);
        var (_, step) = await ArrangeAsync(index: true, oldnewCount: 0, unrelated: true);

        var r = await AiSeed.ExecuteAsync(pg, new AnalyzeStep(ai.Gateway(), Time), step);

        Assert.Equal(30, SentIds(ai).Count);
        Assert.Equal("fallback_all", Retrieval(r).GetProperty("status").GetString());
    }
}
