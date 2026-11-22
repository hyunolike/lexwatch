using System.Text.Json;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Retrieval;
using Pgvector;
using EvidenceRow = LexWatch.Api.Data.Entities.Evidence;

namespace LexWatch.Tests;

[Collection("pg")]
public class HybridSearchTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>문서마다 근거와 청크 하나를 만든다. 청크 ID = "law:L:제{i}조#1", Ord = i.</summary>
    private async Task<Guid> IndexAsync(IReadOnlyList<string> texts, IReadOnlyList<float[]>? vectors = null, string model = "m")
    {
        var run = await Seed.RunAsync(pg);
        await using var db = pg.CreateDb();
        for (var i = 0; i < texts.Count; i++)
        {
            var id = $"law:L:제{i + 1}조";
            db.Evidence.Add(new EvidenceRow { RunId = run.Id, EvidenceId = id, Kind = "law", Title = id, Body = texts[i], FetchedAt = DateTimeOffset.UtcNow });
            db.ArticleChunks.Add(new ArticleChunk
            {
                RunId = run.Id, ChunkId = id + "#1", EvidenceId = id, Ord = i, Text = texts[i],
                Tokens = KoreanTokenizer.Tokenize(texts[i]).ToArray(),
                Embedding = vectors is null ? null : new Vector(vectors[i]), EmbedModel = vectors is null ? null : model,
            });
        }
        await db.SaveChangesAsync();
        return run.Id;
    }

    private static float[] Unit(int i) { var v = new float[LexWatch.Api.Data.LexWatchDb.ChunkEmbedDim]; v[i] = 1; return v; }

    private record GoldenCase(string Query, List<GoldenHit> Expected);
    private record GoldenHit(int Index, double Score);
    private record Golden(List<string> Passages, List<GoldenCase> Cases);

    [Fact]
    public async Task Bm25_matches_python_lexical_scores_and_order()
    {
        // fixtures/retrieval/bm25_golden.json은 ai/app/rag/lexical.py로 만든 정답이다(scripts/bm25_golden.py).
        var path = Path.Combine(Path.GetDirectoryName(LawFixtures.Dir)!, "retrieval", "bm25_golden.json");
        var golden = JsonSerializer.Deserialize<Golden>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var run = await IndexAsync(golden.Passages);
        await using var db = pg.CreateDb();
        foreach (var c in golden.Cases)
        {
            var hits = await HybridSearch.SearchAsync(db, run, KoreanTokenizer.Tokenize(c.Query).ToArray(), null, null, SearchMode.Bm25, 100);
            var exp = c.Expected.Take(50).ToList();
            Assert.Equal(exp.Count, hits.Count);
            for (var i = 0; i < exp.Count; i++)
            {
                // 점수는 허용 오차 안에서 같아야 하고, 허용 오차 안의 동점은 순서를 따지지 않는다.
                Assert.Equal(exp[i].Score, hits[i].BmScore!.Value, 1e-9);
                if (i + 1 < exp.Count && Math.Abs(exp[i].Score - exp[i + 1].Score) > 1e-9)
                    Assert.Equal($"law:L:제{exp[i].Index + 1}조", hits[i].EvidenceId);
            }
        }
    }

    [Fact]
    public async Task Hybrid_fuses_ranks_with_rrf_and_reports_both_paths()
    {
        var run = await IndexAsync(["연장근로 가산임금 지급", "휴게시간 부여", "야간근로 제한"], [Unit(0), Unit(1), Unit(2)]);
        await using var db = pg.CreateDb();
        // 키워드는 1번 문서, 벡터는 3번 문서를 가리킨다.
        var hits = await HybridSearch.SearchAsync(db, run, KoreanTokenizer.QueryTokens("가산임금"), Unit(2), "m", SearchMode.Hybrid, 10);

        Assert.Equal(["law:L:제1조", "law:L:제3조", "law:L:제2조"], hits.Select(h => h.EvidenceId));
        var first = hits[0];
        // 벡터 경로: 3번이 1위, 1번과 2번은 거리가 같아 Ord 순으로 2위, 3위.
        Assert.Equal((1, 2), (first.BmRank!.Value, first.DenseRank!.Value));
        Assert.Equal(1.0 / 61 + 1.0 / 62, first.Rrf, 1e-12);
        Assert.Null(hits[2].BmRank);
    }

    [Fact]
    public async Task Dense_path_is_skipped_when_vector_or_model_does_not_fit()
    {
        var run = await IndexAsync(["연장근로 가산임금", "휴게시간"], [Unit(0), Unit(1)]);
        await using var db = pg.CreateDb();
        var q = KoreanTokenizer.QueryTokens("가산임금");

        var wrongModel = await HybridSearch.SearchAsync(db, run, q, Unit(1), "other", SearchMode.Hybrid, 10);
        Assert.All(wrongModel, h => Assert.Null(h.DenseRank));
        var wrongDim = await HybridSearch.SearchAsync(db, run, q, new float[8], "m", SearchMode.Hybrid, 10);
        Assert.All(wrongDim, h => Assert.Null(h.DenseRank));
        Assert.Empty(await HybridSearch.SearchAsync(db, run, q, null, null, SearchMode.Dense, 10));
        Assert.Empty(await HybridSearch.SearchAsync(db, run, [], null, null, SearchMode.Hybrid, 10));
    }

    [Fact]
    public async Task Results_are_per_evidence_deterministic_and_run_scoped()
    {
        var run = await Seed.RunAsync(pg);
        await using (var db = pg.CreateDb())
        {
            db.Evidence.Add(new EvidenceRow { RunId = run.Id, EvidenceId = "law:L:제1조", Kind = "law", Title = "t", Body = "b", FetchedAt = DateTimeOffset.UtcNow });
            for (var n = 1; n <= 3; n++)
                db.ArticleChunks.Add(new ArticleChunk { RunId = run.Id, ChunkId = $"law:L:제1조#{n}", EvidenceId = "law:L:제1조", Ord = n, Text = "가산임금", Tokens = KoreanTokenizer.Tokenize("가산임금").ToArray() });
            await db.SaveChangesAsync();
        }
        var other = await IndexAsync(["가산임금 가산임금"]);
        await using var v = pg.CreateDb();
        var q = KoreanTokenizer.QueryTokens("가산임금");
        var a = await HybridSearch.SearchAsync(v, run.Id, q, null, null, SearchMode.Bm25, 10);
        var b = await HybridSearch.SearchAsync(v, run.Id, q, null, null, SearchMode.Bm25, 10);

        var hit = Assert.Single(a);                       // 같은 근거의 청크 3개는 하나로 합친다
        Assert.Equal("law:L:제1조#1", hit.ChunkId);       // 동점이면 Ord가 앞선 청크
        Assert.Equal(a, b);
        Assert.Single(await HybridSearch.SearchAsync(v, other, q, null, null, SearchMode.Bm25, 10));
    }
}
