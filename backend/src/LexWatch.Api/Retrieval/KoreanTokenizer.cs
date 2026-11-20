using System.Text.RegularExpressions;

namespace LexWatch.Api.Retrieval;

/// <summary>
/// ai/app/rag/lexical.py의 tokenize를 그대로 옮긴 것. 소문자로 바꿔 [0-9a-z가-힣]+ 어절을 뽑고,
/// 한글이 섞인 어절은 숫자를 포함한 2글자 바이그램을 더한다("제56조" → 제56조, 제5, 56, 6조).
/// 공용 골든 파일(fixtures/retrieval/tokenizer_golden.json)로 두 구현이 같음을 확인한다.
/// </summary>
public static partial class KoreanTokenizer
{
    [GeneratedRegex("[0-9a-z가-힣]+")]
    private static partial Regex Word();

    public static IReadOnlyList<string> Tokenize(string text)
    {
        var toks = new List<string>();
        foreach (Match m in Word().Matches(text.ToLowerInvariant()))
        {
            var w = m.Value;
            toks.Add(w);
            if (w.Length > 1 && w.Any(IsHangul))
                for (var i = 0; i < w.Length - 1; i++) toks.Add(w.Substring(i, 2));
        }
        return toks;
    }

    /// <summary>검색 질의용: 순서를 지키며 중복을 지우고 max개로 자른다.</summary>
    public static string[] QueryTokens(string text, int max = 64) => Tokenize(text).Distinct().Take(max).ToArray();

    private static bool IsHangul(char c) => c >= '가' && c <= '힣';
}
