using LexWatch.Api.Data.Entities;

namespace LexWatch.Api.Retrieval;

/// <summary>실행의 law 근거를 청크 엔터티로 만든다(임베딩 없음). 평가도 이 함수를 쓴다.</summary>
public static class ArticleIndexer
{
    public static List<ArticleChunk> Build(Guid runId, IEnumerable<Evidence> evidence)
    {
        var ord = 0;
        return evidence
            .Where(e => e.Kind == "law")
            .OrderBy(e => e.EvidenceId, ArticleOrder.Comparer)
            .SelectMany(e => ArticleChunker.Chunk(new ChunkSource(e.EvidenceId, e.Title, e.Body)))
            .Select(c => new ArticleChunk
            {
                RunId = runId, ChunkId = c.ChunkId, EvidenceId = c.EvidenceId, Ord = ord++, Text = c.Text,
                Tokens = KoreanTokenizer.Tokenize(c.Text).ToArray(),
            })
            .ToList();
    }
}
