using System.Text.Json;
using System.Text.RegularExpressions;
using LexWatch.Api.Data.Entities;

namespace LexWatch.Api.Retrieval;

public sealed record SearchQuery(string Text, string[] Tokens);

/// <summary>
/// 분석용 검색 질의(hybrid-rag design 5.3). 세 출처에서 토큰을 할당량대로 모은다:
/// 변경 조문의 새 구간 48, 괄호 안 조문 제목 16, 실무그룹 관심 분야 32. 앞에서 나온 토큰은 다시 세지 않는다.
/// 법령명과 "신구조문 대비"는 모든 조문에 흔해 질의에 넣지 않는다.
/// </summary>
public static partial class RetrievalQuery
{
    public const int NewQuota = 48, TitleQuota = 16, InterestQuota = 32;
    public const int MaxTokens = NewQuota + TitleQuota + InterestQuota;
    public const int NoMarkHeadChars = 200;
    private const string NewHeader = "\n\n[신조문]\n";

    [GeneratedRegex(@"\(([^)]*)\)")]
    private static partial Regex Parenthesized();

    public static SearchQuery Build(string interest, string lawName, IReadOnlyList<Evidence> oldNew)
    {
        if (oldNew.Count == 0)
        {
            var text = $"{interest}\n{lawName}";
            return new SearchQuery(text, KoreanTokenizer.QueryTokens(text, MaxTokens));
        }
        var segments = string.Join(" ", oldNew.Select(NewSegment));
        var titles = string.Join(" ", oldNew.Select(e => Parenthesized().Match(e.Title) is { Success: true } m ? m.Groups[1].Value : ""));
        var tokens = new List<string>();
        var seen = new HashSet<string>();
        void Take(string s, int quota)
        {
            var n = 0;
            foreach (var t in KoreanTokenizer.Tokenize(s))
            {
                if (n == quota) break;
                if (seen.Add(t)) { tokens.Add(t); n++; }
            }
        }
        Take(segments, NewQuota);
        Take(titles, TitleQuota);
        Take(interest, InterestQuota);
        return new SearchQuery($"{segments}\n{titles}\n{interest}", tokens.ToArray());
    }

    /// <summary>"[신조문]" 뒤 텍스트에 법제처 변경 구간(new)을 적용한다. 구간이 없으면 앞 200자.</summary>
    public static string NewSegment(Evidence e)
    {
        var i = e.Body.IndexOf(NewHeader, StringComparison.Ordinal);
        var after = i < 0 ? e.Body : e.Body[(i + NewHeader.Length)..];
        int[][]? marks = null;
        if (e.ChangeMarksJson is { } json)
            try { marks = JsonDocument.Parse(json).RootElement.GetProperty("new").Deserialize<int[][]>(); }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
        var parts = (marks ?? [])
            .Where(r => r.Length == 2 && 0 <= r[0] && r[0] < r[1] && r[1] <= after.Length)
            .Select(r => after[r[0]..r[1]]).ToList();
        return parts.Count > 0 ? string.Join(" ", parts) : after[..Math.Min(NoMarkHeadChars, after.Length)];
    }
}
