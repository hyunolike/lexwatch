using System.Globalization;
using LexWatch.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LexWatch.Api.Retrieval;

public enum SearchMode { Bm25, Dense, Hybrid }

public sealed record RetrievalHit(string EvidenceId, string ChunkId, int? BmRank, int? DenseRank, double Rrf, double? BmScore);

/// <summary>
/// 한 실행 안의 조문 청크를 BM25와 벡터 코사인으로 찾아 RRF(k=60)로 합친다(hybrid-rag RAG-03, design 4절).
/// BM25는 ai/app/rag/lexical.py와 같은 공식을 SQL로 계산한다(질의 토큰 중복도 그대로 더한다).
/// 운영(analyze)과 평가가 이 함수를 같이 쓴다.
/// </summary>
public static class HybridSearch
{
    public const int PathLimit = 50;
    private const int RrfK = 60;
    private const double K1 = 1.4, B = 0.75;

    private sealed class Row
    {
        public string EvidenceId { get; set; } = "";
        public string ChunkId { get; set; } = "";
        public int? BmRank { get; set; }
        public int? DenseRank { get; set; }
        public double Rrf { get; set; }
        public double? BmScore { get; set; }
    }

    public static async Task<IReadOnlyList<RetrievalHit>> SearchAsync(LexWatchDb db, Guid runId, string[] queryTokens,
        float[]? queryVector, string? model, SearchMode mode, int topEvidence, CancellationToken ct = default)
    {
        var useDense = mode != SearchMode.Bm25 && queryVector is { Length: LexWatchDb.ChunkEmbedDim } && model is not null;
        var useBm = mode != SearchMode.Dense && queryTokens.Length > 0;
        if (!useDense && !useBm) return [];

        var ctes = new List<string>();
        if (useBm) ctes.Add(Bm25Cte);
        if (useDense) ctes.Add(DenseCte);
        var (from, chunkId) = (useBm, useDense) switch
        {
            (true, true) => ("bm FULL JOIN dense ON bm.\"ChunkId\" = dense.\"ChunkId\"", "coalesce(bm.\"ChunkId\", dense.\"ChunkId\")"),
            (true, false) => ("bm", "bm.\"ChunkId\""),
            _ => ("dense", "dense.\"ChunkId\""),
        };
        var bmRank = useBm ? "bm.r::int" : "NULL::int";
        var bmScore = useBm ? "bm.score" : "NULL::float8";
        var denseRank = useDense ? "dense.r::int" : "NULL::int";
        var rrf = $"coalesce(1.0 / ({RrfK} + {bmRank}), 0) + coalesce(1.0 / ({RrfK} + {denseRank}), 0)";
        var sql = $"""
            WITH {string.Join(",\n", ctes)}
            SELECT c."EvidenceId", c."ChunkId", {bmRank} AS "BmRank", {denseRank} AS "DenseRank",
                   ({rrf})::float8 AS "Rrf", {bmScore} AS "BmScore"
            FROM {from}
            JOIN "ArticleChunks" c ON c."RunId" = @run AND c."ChunkId" = {chunkId}
            ORDER BY "Rrf" DESC, c."Ord", c."ChunkId"
            LIMIT {PathLimit * 2}
            """;

        var ps = new List<object> { new NpgsqlParameter("run", runId) };
        if (useBm) ps.Add(new NpgsqlParameter("qtoks", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = queryTokens });
        if (useDense) { ps.Add(new NpgsqlParameter("qvec", VectorLiteral(queryVector!))); ps.Add(new NpgsqlParameter("model", model)); }

        var rows = await db.Database.SqlQueryRaw<Row>(sql, ps.ToArray()).ToListAsync(ct);
        // 청크 단위 결과를 근거 단위로 합친다: 근거마다 가장 앞선 청크만 남긴다.
        return rows.DistinctBy(r => r.EvidenceId).Take(topEvidence)
            .Select(r => new RetrievalHit(r.EvidenceId, r.ChunkId, r.BmRank, r.DenseRank, r.Rrf, r.BmScore)).ToList();
    }

    private static string VectorLiteral(float[] v) =>
        "[" + string.Join(",", v.Select(x => x.ToString("R", CultureInfo.InvariantCulture))) + "]";

    private static readonly string Bm25Cte = $"""
        docs AS (
          SELECT "ChunkId", "Ord", "Tokens", cardinality("Tokens") AS len
          FROM "ArticleChunks" WHERE "RunId" = @run
        ), stats AS (
          SELECT count(*)::float8 AS n, coalesce(nullif(avg(len), 0), 1)::float8 AS avgdl FROM docs
        ), q AS (
          SELECT tok, count(*)::float8 AS qn FROM unnest(@qtoks) AS u(tok) GROUP BY tok
        ), hits AS (
          SELECT d."ChunkId", t.tok, count(*)::float8 AS tf, d.len
          FROM docs d CROSS JOIN LATERAL unnest(d."Tokens") AS t(tok)
          WHERE t.tok IN (SELECT tok FROM q)
          GROUP BY d."ChunkId", t.tok, d.len
        ), df AS (
          SELECT tok, count(*)::float8 AS df FROM hits GROUP BY tok
        ), bm25 AS (
          SELECT h."ChunkId",
                 sum(q.qn * ln(1 + (s.n - df.df + 0.5) / (df.df + 0.5))
                     * h.tf * ({K1} + 1) / (h.tf + {K1} * (1 - {B} + {B} * h.len / s.avgdl))) AS score
          FROM hits h JOIN df ON df.tok = h.tok JOIN q ON q.tok = h.tok CROSS JOIN stats s
          GROUP BY h."ChunkId"
        ), bm AS (
          SELECT b."ChunkId", b.score, row_number() OVER (ORDER BY b.score DESC, d."Ord", b."ChunkId") AS r
          FROM bm25 b JOIN docs d ON d."ChunkId" = b."ChunkId"
          ORDER BY r LIMIT {PathLimit}
        )
        """;

    private static readonly string DenseCte = $"""
        dense AS (
          SELECT "ChunkId", row_number() OVER (ORDER BY "Embedding" <=> CAST(@qvec AS vector), "Ord", "ChunkId") AS r
          FROM "ArticleChunks"
          WHERE "RunId" = @run AND "EmbedModel" = @model AND "Embedding" IS NOT NULL
          ORDER BY r LIMIT {PathLimit}
        )
        """;
}
