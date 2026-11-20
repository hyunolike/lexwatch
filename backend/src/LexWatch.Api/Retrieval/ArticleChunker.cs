using System.Text.RegularExpressions;

namespace LexWatch.Api.Retrieval;

public sealed record ChunkSource(string EvidenceId, string Title, string Body);

public sealed record Chunk(string ChunkId, string EvidenceId, string Text);

/// <summary>
/// 조문 구조 단위 청킹(hybrid-rag RAG-01). 본문 앞의 "제N조(제목)"은 근거 제목과 겹치므로 떼고,
/// 근거 제목 + 줄바꿈 + 본문으로 만든다. 본문이 MaxChars를 넘으면 항(①~⑳) → 줄(호·목) → 문장("다.") 순서로 나눈 뒤
/// 앞에서부터 MaxChars 안으로 묶는다.
/// </summary>
public static partial class ArticleChunker
{
    public const int MaxChars = 1200;

    [GeneratedRegex(@"^제\d+조(?:의\d+)?(?:\([^)]*\))?\s*")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^삭제\s*(<[^>]*>)?\s*$")]
    private static partial Regex Deleted();

    [GeneratedRegex(@"(?<=다\.)\s+")]
    private static partial Regex SentenceEnd();

    public static IReadOnlyList<Chunk> Chunk(ChunkSource src)
    {
        var body = Heading().Replace(src.Body.Trim(), "", 1).Trim();
        if (body == "" || Deleted().IsMatch(body)) return [];

        var pieces = body.Length <= MaxChars ? [body] : Pack(Split(body));
        return pieces.Select((p, i) => new Chunk($"{src.EvidenceId}#{i + 1}", src.EvidenceId, $"{src.Title}\n{p}")).ToList();
    }

    /// <summary>항 단위 조각. 항이 MaxChars를 넘으면 줄로, 줄이 넘으면 문장으로 더 나눈다.</summary>
    private static IEnumerable<string> Split(string body)
    {
        var paragraphs = new List<List<string>>();
        foreach (var line in body.Split('\n'))
        {
            if (paragraphs.Count == 0 || IsParagraphStart(line)) paragraphs.Add([]);
            paragraphs[^1].Add(line);
        }
        foreach (var para in paragraphs)
        {
            var text = string.Join("\n", para);
            if (text.Length <= MaxChars) { yield return text; continue; }
            foreach (var line in para)
            {
                if (line.Length <= MaxChars) { yield return line; continue; }
                foreach (var s in SentenceEnd().Split(line)) foreach (var hard in Hard(s)) yield return hard;
            }
        }
    }

    /// <summary>문장 하나가 MaxChars를 넘는 드문 경우 길이로 자른다.</summary>
    private static IEnumerable<string> Hard(string s)
    {
        for (var i = 0; i < s.Length; i += MaxChars) yield return s.Substring(i, Math.Min(MaxChars, s.Length - i));
    }

    /// <summary>조각을 순서대로 MaxChars 안에서 묶는다. 항이 바뀌는 곳에서 줄바꿈으로 잇는다.</summary>
    private static List<string> Pack(IEnumerable<string> parts)
    {
        var chunks = new List<string>();
        var cur = "";
        foreach (var p in parts)
        {
            var joined = cur == "" ? p : cur + "\n" + p;
            if (joined.Length <= MaxChars) { cur = joined; continue; }
            if (cur != "") chunks.Add(cur);
            cur = p;
        }
        if (cur != "") chunks.Add(cur);
        return chunks;
    }

    private static bool IsParagraphStart(string line) => line.Length > 0 && line[0] >= '①' && line[0] <= '⑳';
}
