using System.Text.RegularExpressions;
using LexWatch.Api.Ai;
using LexWatch.Api.Data.Entities;

namespace LexWatch.Api.Critic;

/// <summary>크리틱 2단: 문장 속 조문·날짜·금액·기간이 인용한 근거 본문에 실제로 있는지, 시행일이 법령 변경과 맞는지 대조한다.</summary>
public static partial class SourceMatchCritic
{
    [GeneratedRegex(@"제\d+조(?:의\d+)?")] private static partial Regex ArticleRx();
    [GeneratedRegex(@"(\d{4})\.\s?(\d{1,2})\.\s?(\d{1,2})\.?")] private static partial Regex DateDotRx();
    [GeneratedRegex(@"(\d{4})년\s?(\d{1,2})월\s?(\d{1,2})일")] private static partial Regex DateKoRx();
    [GeneratedRegex(@"\d[\d,]*(?:\s?(?:\d[\d,]*|[억만천백십]))*\s?원")] private static partial Regex AmountRx();
    [GeneratedRegex(@"(?<!\d)\d{1,3}\s?(?:일|개월|년)")] private static partial Regex PeriodRx();
    [GeneratedRegex(@"\d{4}년(?:\s?\d{1,2}월(?:\s?\d{1,2}일)?)?|\d{1,2}월\s?\d{1,2}일|\d{1,2}월")] private static partial Regex PartialDateRx();
    [GeneratedRegex(@"시행(?!령|규칙)")] private static partial Regex EnforceRx();
    [GeneratedRegex(@"\s+")] private static partial Regex WsRx();

    public static IReadOnlyList<CriticFindingDto> Check(
        IReadOnlyList<MemoSentenceDto> memo, IReadOnlyDictionary<string, string> evidenceBodies, LawChange change)
    {
        var findings = new List<CriticFindingDto>();
        for (var i = 0; i < memo.Count; i++)
        {
            var sentence = Normalize(memo[i].Sentence);
            var bodies = (memo[i].Citations ?? []).Where(evidenceBodies.ContainsKey).Select(c => Normalize(evidenceBodies[c])).ToList();
            var sTokens = Tokens(sentence);

            if (bodies.Count > 0)
            {
                var bTokens = bodies.Select(Tokens).ToList();
                foreach (var t in sTokens)
                    if (!bTokens.Any(b => b.Contains(t)))
                        findings.Add(new CriticFindingDto(i, "C2-MISMATCH", $"인용한 근거 본문에 없는 표기: {t.Display}"));
            }

            if (change.EffectiveDate is { } eff && EnforceRx().IsMatch(sentence))
            {
                var effText = eff.ToString("yyyy-MM-dd");
                var ds = Dates(sentence).Distinct().ToList();
                if (ds.Count > 0 && !ds.Contains(effText))
                    findings.Add(new CriticFindingDto(i, "C2-EFFECTIVE", $"시행일 {effText}과 다른 시행 날짜: {string.Join(", ", ds)}"));
            }
        }
        return findings;
    }

    private readonly struct Token(string kind, string key, string display) : IEquatable<Token>
    {
        public string Kind { get; } = kind;
        public string Key { get; } = key;
        public string Display { get; } = display;
        public bool Equals(Token o) => Kind == o.Kind && Key == o.Key;
        public override bool Equals(object? obj) => obj is Token t && Equals(t);
        public override int GetHashCode() => HashCode.Combine(Kind, Key);
    }

    private static HashSet<Token> Tokens(string text)
    {
        var set = new HashSet<Token>();
        foreach (Match m in ArticleRx().Matches(text)) set.Add(new Token("art", m.Value, m.Value));
        foreach (var d in Dates(text)) set.Add(new Token("date", d, d));
        var rest = DateKoRx().Replace(DateDotRx().Replace(text, " "), " ");
        foreach (Match m in AmountRx().Matches(rest)) set.Add(new Token("amt", AmountValue(m.Value), m.Value));
        rest = PartialDateRx().Replace(AmountRx().Replace(rest, " "), " ");
        foreach (Match m in PeriodRx().Matches(rest)) set.Add(new Token("per", Compact(m.Value), m.Value));
        return set;
    }

    private static IEnumerable<string> Dates(string text) =>
        DateDotRx().Matches(text).Concat(DateKoRx().Matches(text))
            .Select(m => $"{m.Groups[1].Value}-{int.Parse(m.Groups[2].Value):00}-{int.Parse(m.Groups[3].Value):00}");

    /// <summary>한국어 수 표기를 원 단위 값으로 바꾼다: 억·만 묶음 안에서 천·백·십과 숫자를 모아 곱한다.</summary>
    private static string AmountValue(string text)
    {
        decimal total = 0, group = 0, num = 0;
        foreach (Match m in AmountTokenRx().Matches(text))
        {
            var t = m.Value;
            if (char.IsDigit(t[0])) { num = decimal.Parse(t.Replace(",", "")); continue; }
            switch (t[0])
            {
                case '천': group += (num == 0 ? 1 : num) * 1000; num = 0; break;
                case '백': group += (num == 0 ? 1 : num) * 100; num = 0; break;
                case '십': group += (num == 0 ? 1 : num) * 10; num = 0; break;
                default:
                    group += num;
                    total += (group == 0 ? 1 : group) * (t[0] == '억' ? 100_000_000m : 10_000m);
                    group = 0; num = 0; break;
            }
        }
        return (total + group + num).ToString("0");
    }

    [GeneratedRegex(@"\d[\d,]*|[억만천백십]")] private static partial Regex AmountTokenRx();

    private static string Compact(string s) => s.Replace(" ", "").Replace(",", "");

    private static string Normalize(string s)
    {
        var chars = (s ?? "").Select(c => c is >= '０' and <= '９' ? (char)('0' + (c - '０')) : c).ToArray();
        return WsRx().Replace(new string(chars), " ").Trim();
    }
}
