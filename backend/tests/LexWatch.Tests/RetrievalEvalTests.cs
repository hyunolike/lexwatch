using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.LawApi;
using LexWatch.Api.Retrieval;
using Pgvector;
using EvidenceRow = LexWatch.Api.Data.Entities.Evidence;

namespace LexWatch.Tests;

public sealed record EvalQuery(string Id, string LawId, string Type, string? Query, string? Interest, string? ChangeTitle,
    string? ChangeText, string[] Answers);

/// <summary>검색 평가(hybrid-rag RAG-06) 공용 코드: 질의셋, 녹화 법령, 지표, 임베딩 캐시.</summary>
public static class RetrievalEval
{
    public static string Dir => Path.Combine(Path.GetDirectoryName(LawFixtures.Dir)!, "retrieval");
    public static readonly string[] Laws = ["001872", "011357", "010199"];
    public const string Model = "@cf/baai/bge-m3";
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static List<EvalQuery> Queries() => File.ReadAllLines(Path.Combine(Dir, "queries.jsonl"))
        .Where(l => l.Trim() != "").Select(l => JsonSerializer.Deserialize<EvalQuery>(l, J)!).ToList();

    public static async Task<(string Name, List<EvidenceRow> Evidence)> LawAsync(string lawId, Guid runId)
    {
        var law = Assert.IsType<LawApiResult<LawBody>.Ok>(await RecordedCurrent.Client().GetLawAsync(lawId)).Value;
        // FetchArticlesStep과 같은 ID와 제목 형식
        var rows = law.Articles.Select(a => new EvidenceRow
        {
            RunId = runId, EvidenceId = $"law:{lawId}:{a.Number}", Kind = "law",
            Title = a.Title == "" ? $"{law.LawName} {a.Number}" : $"{law.LawName} {a.Number}({a.Title})", Body = a.Text,
            FetchedAt = DateTimeOffset.UtcNow,
        }).ToList();
        return (law.LawName, rows);
    }

    /// <summary>운영형 질의는 분석 단계와 같은 RetrievalQuery로 만든다(가상 변경 조문).</summary>
    public static SearchQuery Build(EvalQuery q, string lawName)
    {
        if (q.Type != "operational") return new SearchQuery(q.Query!, KoreanTokenizer.QueryTokens(q.Query!, RetrievalQuery.MaxTokens));
        var oldNew = new EvidenceRow
        {
            EvidenceId = $"oldnew:{q.LawId}:변경", Kind = "oldnew", Title = $"{lawName} 변경({q.ChangeTitle}) 신구조문 대비",
            Body = $"[구조문]\n(없음)\n\n[신조문]\n{q.ChangeText}",
        };
        return RetrievalQuery.Build(q.Interest!, lawName, [oldNew]);
    }

    public static double RecallAt(IReadOnlyList<string> ranked, IReadOnlyCollection<string> answers, int k) =>
        answers.Count == 0 ? 0 : (double)ranked.Take(k).Count(answers.Contains) / answers.Count;

    public static double Reciprocal(IReadOnlyList<string> ranked, IReadOnlyCollection<string> answers)
    {
        for (var i = 0; i < ranked.Count; i++) if (answers.Contains(ranked[i])) return 1.0 / (i + 1);
        return 0;
    }

    public static string Key(string inputType, string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{Model}\0{inputType}\0{text}")));

    /// <summary>fixtures/retrieval/embeddings.json. 없으면 빈 캐시.</summary>
    public static Dictionary<string, float[]> Cache()
    {
        var path = Path.Combine(Dir, "embeddings.json");
        if (!File.Exists(path)) return [];
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        return root["vectors"]!.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.AsArray().Select(x => x!.GetValue<float>()).ToArray());
    }
}

/// <summary>CI에서 도는 질의셋 형식과 지표 계산 테스트.</summary>
public class RetrievalEvalDataTests
{
    [Fact]
    public async Task Query_set_is_well_formed_and_answers_exist_in_recordings()
    {
        var qs = RetrievalEval.Queries();
        Assert.True(qs.Count >= 30);
        Assert.Equal(RetrievalEval.Laws.Order(), qs.Select(q => q.LawId).Distinct().Order());
        Assert.Equal(qs.Count, qs.Select(q => q.Id).Distinct().Count());
        Assert.All(qs, q => Assert.Contains(q.Type, new[] { "keyword", "paraphrase", "operational" }));
        Assert.All(qs, q => Assert.NotEmpty(q.Answers));
        Assert.All(qs.Where(q => q.Type == "operational"), q => Assert.False(string.IsNullOrEmpty(q.Interest) || string.IsNullOrEmpty(q.ChangeText)));
        foreach (var law in RetrievalEval.Laws)
        {
            var (_, ev) = await RetrievalEval.LawAsync(law, Guid.Empty);
            var ids = ev.Select(e => e.EvidenceId).ToHashSet();
            Assert.All(qs.Where(q => q.LawId == law).SelectMany(q => q.Answers), a => Assert.Contains($"law:{law}:{a}", ids));
        }
    }

    [Fact]
    public void Metrics_are_computed_per_answer_set()
    {
        string[] ranked = ["a", "b", "c", "d"];
        Assert.Equal(0.5, RetrievalEval.RecallAt(ranked, ["b", "z"], 5));
        Assert.Equal(0, RetrievalEval.RecallAt(ranked, ["d"], 3));
        Assert.Equal(0.5, RetrievalEval.Reciprocal(ranked, ["b", "d"]));
        Assert.Equal(0, RetrievalEval.Reciprocal(ranked, ["z"]));
        Assert.Equal(RetrievalEval.Key("query", "x"), RetrievalEval.Key("query", "x"));
        Assert.NotEqual(RetrievalEval.Key("query", "x"), RetrievalEval.Key("passage", "x"));
    }
}

/// <summary>
/// 실제 평가. CI 기본 실행에서 빠지고 make eval-retrieval로 돈다. 녹화 법령 3개를 운영 코드(ArticleIndexer,
/// HybridSearch)로 색인하고 세 모드를 잰다. 임베딩 캐시가 없거나 빠진 텍스트가 있으면 BM25만 재고,
/// 필요한 텍스트 목록을 needed.jsonl에 남긴다(scripts/embed_cache.py가 채운다). 네트워크는 쓰지 않는다.
/// </summary>
[Collection("pg")]
[Trait("Category", "eval")]
public class RetrievalEvalRun(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Measure()
    {
        var cache = RetrievalEval.Cache();
        var needed = new List<object>();
        var queries = RetrievalEval.Queries();
        var perQuery = new List<Dictionary<string, object?>>();
        var denseReady = true;

        foreach (var lawId in RetrievalEval.Laws)
        {
            var run = await Seed.RunAsync(pg);
            var (name, evidence) = await RetrievalEval.LawAsync(lawId, run.Id);
            var chunks = ArticleIndexer.Build(run.Id, evidence);
            foreach (var c in chunks)
            {
                if (cache.TryGetValue(RetrievalEval.Key("passage", c.Text), out var v)) { c.Embedding = new Vector(v); c.EmbedModel = RetrievalEval.Model; }
                else { denseReady = false; needed.Add(new { input_type = "passage", text = c.Text }); }
            }
            await using (var db = pg.CreateDb())
            {
                db.Evidence.AddRange(evidence);
                db.ArticleChunks.AddRange(chunks);
                await db.SaveChangesAsync();
            }

            await using var s = pg.CreateDb();
            foreach (var q in queries.Where(q => q.LawId == lawId))
            {
                var sq = RetrievalEval.Build(q, name);
                float[]? qv = cache.GetValueOrDefault(RetrievalEval.Key("query", sq.Text));
                if (qv is null) { denseReady = false; needed.Add(new { input_type = "query", text = sq.Text }); }
                var answers = q.Answers.Select(a => $"law:{lawId}:{a}").ToHashSet();
                var row = new Dictionary<string, object?> { ["id"] = q.Id, ["type"] = q.Type, ["answers"] = q.Answers };
                foreach (var mode in new[] { SearchMode.Bm25, SearchMode.Dense, SearchMode.Hybrid })
                {
                    if (mode != SearchMode.Bm25 && qv is null) continue;
                    var hits = await HybridSearch.SearchAsync(s, run.Id, sq.Tokens, qv, RetrievalEval.Model, mode, 20);
                    var ranked = hits.Select(h => h.EvidenceId).ToList();
                    row[mode.ToString()] = new
                    {
                        recall5 = RetrievalEval.RecallAt(ranked, answers, 5), recall10 = RetrievalEval.RecallAt(ranked, answers, 10),
                        rr = RetrievalEval.Reciprocal(ranked, answers),
                        top = ranked.Take(5).Select(i => i[(i.LastIndexOf(':') + 1)..]).ToArray(),
                    };
                }
                perQuery.Add(row);
            }
        }

        var modes = denseReady ? new[] { "Bm25", "Dense", "Hybrid" } : ["Bm25"];
        double Avg(IEnumerable<Dictionary<string, object?>> rows, string mode, string metric) =>
            rows.Average(r => (double)r[mode]!.GetType().GetProperty(metric)!.GetValue(r[mode])!);
        var summary = modes.ToDictionary(m => m, m => new
        {
            all = new { recall5 = Avg(perQuery, m, "recall5"), recall10 = Avg(perQuery, m, "recall10"), mrr = Avg(perQuery, m, "rr") },
            byType = perQuery.GroupBy(r => (string)r["type"]!).ToDictionary(g => g.Key, g => new
            {
                n = g.Count(), recall5 = Avg(g, m, "recall5"), recall10 = Avg(g, m, "recall10"), mrr = Avg(g, m, "rr"),
            }),
        });
        object? versus = denseReady ? new
        {
            hybridWins = perQuery.Count(r => Rr(r, "Hybrid") > Rr(r, "Bm25")),
            bm25Wins = perQuery.Count(r => Rr(r, "Hybrid") < Rr(r, "Bm25")),
            ties = perQuery.Count(r => Rr(r, "Hybrid") == Rr(r, "Bm25")),
        } : null;

        var results = Path.Combine(Path.GetDirectoryName(RetrievalEval.Dir)!, "..", "eval", "results");
        var tag = denseReady ? "bge-m3" : "bm25-only";
        var file = Path.GetFullPath(Path.Combine(results, $"retrieval_{tag}_{DateTime.UtcNow:yyyy-MM-dd}.json"));
        File.WriteAllText(file, JsonSerializer.Serialize(new
        {
            note = "queries and answers written by the project author over recorded statutes (fixtures/lawapi/recorded/current); not a general benchmark",
            model = denseReady ? RetrievalEval.Model : null, queries = perQuery.Count, summary, versus, perQuery,
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        File.WriteAllLines(Path.Combine(RetrievalEval.Dir, "needed.jsonl"), needed.Select(n => JsonSerializer.Serialize(n,
            new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));
        Assert.True(File.Exists(file));
    }

    private static double Rr(Dictionary<string, object?> r, string mode) =>
        (double)r[mode]!.GetType().GetProperty("rr")!.GetValue(r[mode])!;
}
