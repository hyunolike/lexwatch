using System.Text.RegularExpressions;

namespace LexWatch.Api.Retrieval;

/// <summary>조문 번호 자연 정렬: 제2조 &lt; 제10조 &lt; 제10조의2 &lt; 제10조의10. 근거 ID("law:001872:제56조")도 받는다.</summary>
public static partial class ArticleOrder
{
    [GeneratedRegex(@"^제(\d+)조(?:의(\d+))?$")]
    private static partial Regex Article();

    public static readonly Comparer<string> Comparer = Comparer<string>.Create(Compare);

    public static int Compare(string? a, string? b)
    {
        var (ka, ra) = Key(a ?? "");
        var (kb, rb) = Key(b ?? "");
        var c = ka.CompareTo(kb);
        return c != 0 ? c : string.CompareOrdinal(ra, rb);
    }

    /// <summary>조문 번호가 아니면(부칙 등) 맨 뒤로 보내고 문자열로 비교한다.</summary>
    private static ((int, int, int), string) Key(string s)
    {
        var number = s[(s.LastIndexOf(':') + 1)..];
        var m = Article().Match(number);
        if (!m.Success) return ((1, 0, 0), number);
        var branch = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
        return ((0, int.Parse(m.Groups[1].Value), branch), "");
    }
}
