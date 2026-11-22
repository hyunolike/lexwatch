using LexWatch.Api.Ai;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Retrieval;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace LexWatch.Api.Workflow.Steps;

/// <summary>
/// 조문 검색 색인(hybrid-rag RAG-02, design 5.2). classify가 진행할 그룹이 있을 때만 이 단계를 낸다.
/// 임베딩은 전부 받거나 하나도 쓰지 않는다. 받지 못하면 키워드(BM25) 색인만으로 계속하고 출력에 저하를 남긴다.
/// 무료 한도 소진처럼 몇 시간 풀리지 않는 실패에 파이프라인을 묶지 않으려고 Retry로 돌리지 않는다.
/// 저장은 EF 추적 엔터티로만 한다: 워커가 소유권을 확인한 뒤 같은 트랜잭션에서 저장한다.
/// </summary>
public class IndexArticlesStep(AiGateway ai, TimeProvider time) : IStepHandler
{
    public const int BatchSize = 50;
    public static readonly TimeSpan EmbedBudget = TimeSpan.FromSeconds(90);

    public string Step => "index_articles";

    public async Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var runId = ctx.Run.Id;
        var evidence = await db.Evidence.AsNoTracking().Where(e => e.RunId == runId && e.Kind == "law").ToListAsync(ct);
        var chunks = ArticleIndexer.Build(runId, evidence);

        var (vectors, provider, reason) = await EmbedAllAsync(chunks.Select(c => c.Text).ToList(), ctx.Step.Attempt, ct);
        if (vectors is not null)
            for (var i = 0; i < chunks.Count; i++)
            {
                chunks[i].Embedding = new Vector(vectors[i]);
                chunks[i].EmbedModel = provider!.Name;
            }

        // 재시도에 안전한 저장: 키가 같으면 고치고, 새 키는 추가하고, 남은 키는 지운다.
        var existing = await db.ArticleChunks.Where(c => c.RunId == runId).ToDictionaryAsync(c => c.ChunkId, ct);
        foreach (var c in chunks)
        {
            if (existing.Remove(c.ChunkId, out var row))
            {
                row.EvidenceId = c.EvidenceId; row.Ord = c.Ord; row.Text = c.Text; row.Tokens = c.Tokens;
                row.Embedding = c.Embedding; row.EmbedModel = c.EmbedModel;
            }
            else db.ArticleChunks.Add(c);
        }
        db.ArticleChunks.RemoveRange(existing.Values);

        var groups = await db.GroupReviews.Where(g => g.RunId == runId && g.Status != ReviewStatus.ClosedIrrelevant)
            .OrderBy(g => g.GroupCode).Select(g => g.Id).ToListAsync(ct);
        var dense = vectors is not null;
        return new StepResult.Succeeded(
            new { chunks = chunks.Count, embedded = dense ? chunks.Count : 0, dense = dense ? "ok" : "unavailable", reason, model = provider?.Name },
            groups.Select(g => new NextStep("analyze", g, null)).ToList(),
            dense ? AiMapping.ProviderKind(provider!) : "fallback", dense ? provider!.Name : "keyword-only");
    }

    /// <summary>모두 받으면 벡터를, 하나라도 실패하면 null과 이유를 돌려준다.</summary>
    private async Task<(List<float[]>? Vectors, ProviderInfo? Provider, string? Reason)> EmbedAllAsync(
        IReadOnlyList<string> texts, int attempt, CancellationToken ct)
    {
        if (texts.Count == 0) return ([], null, "no_chunks");
        var started = time.GetTimestamp();
        var all = new List<float[]>();
        ProviderInfo? provider = null;
        foreach (var batch in texts.Chunk(BatchSize))
        {
            if (time.GetElapsedTime(started) > EmbedBudget) return (null, provider, "budget");
            var r = await ai.EmbedAsync(batch, EmbedRequestInput_type.Passage, attempt, ct);
            if (r.Failure is not null) return (null, provider, "embed_failed");
            provider = r.Value!.Provider;
            if (provider.Kind == ProviderInfoKind.Fallback) return (null, provider, "fallback_provider");
            var vs = r.Value.Vectors.Select(v => v.Select(x => (float)x).ToArray()).ToList();
            if (vs.Count != batch.Length || vs.Any(v => v.Length != LexWatchDb.ChunkEmbedDim)) return (null, provider, "dim_mismatch");
            all.AddRange(vs);
        }
        return (all, provider, null);
    }
}
