using System.Text.RegularExpressions;

namespace LexWatch.Api.Governance;

/// <summary>가린 민감정보 한 종류와 건수. 원문 값은 담지 않는다.</summary>
public sealed record MaskHit(string Kind, string Label, int Count);

/// <summary>
/// AI로 보내기 전에 형식이 있는 민감정보와 등록된 고객사 이름을 자리표시로 바꾼다(ai-governance GV-01~03).
/// 결정적 규칙이라 실패하지 않는다. 고객사 이름을 먼저 가리고, 형식 규칙은 표의 순서대로 돈다.
/// 자리표시(<c>[…]</c>)에는 숫자가 없어 뒤 규칙이 다시 잡지 않는다. 13자리 숫자가 주민등록번호와 카드번호 둘 다로 읽히면
/// 먼저 도는 주민등록번호가 이긴다. 고객사 이름은 부분 문자열로도 가린다(한빛전자공업 → [고객사]공업).
/// 법령 문장에 흔한 조문번호, 날짜, 금액, 비율은 잡지 않도록 패턴과 추가 검사로 거른다.
/// </summary>
public static class SensitiveDataMasker
{
    private delegate bool Accept(string match);

    private sealed record Rule(string Kind, string Label, Regex Pattern, Accept? Check = null);

    private const RegexOptions O = RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly Rule[] Rules =
    [
        new("email", "이메일", new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", O)),
        new("rrn", "주민등록번호", new(@"(?<!\d)\d{6}[- ]?[1-8]\d{6}(?!\d)", O), IsBirthDate),
        // 카드는 실제 묶음 형식만: 구분자 없는 13~19자리, 같은 구분자로 4-4-4-(1~7), Amex 4-6-5.
        new("card", "카드번호", new(@"(?<!\d)(?:\d{13,19}|\d{4}([ -])\d{4}\1\d{4}\1\d{1,7}|\d{4}([ -])\d{6}\2\d{5})(?!\d)", O), PassesLuhn),
        new("phone", "전화번호", new(@"(?<!\d)(?:0\d{1,2}([- ])\d{3,4}\1\d{4}|01[016789]\d{7,8})(?!\d)", O)),
        new("brn", "사업자등록번호", new(@"(?<!\d)\d{3}-\d{2}-\d{5}(?!\d)", O)),
        new("account", "계좌번호", new(@"(?<!\d)\d{2,6}(?:-\d{2,8}){2,3}(?!\d)", O), IsAccountLike),
        new("passport", "여권번호", new(@"(?<![A-Za-z0-9])[A-Za-z]\d{8}(?![A-Za-z0-9])", O)),
    ];

    public const string ClientKind = "client";
    public const string ClientLabel = "고객사";

    /// <summary>종류 → 화면 라벨. 기록 화면과 응답이 같은 라벨을 쓴다.</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels =
        Rules.ToDictionary(r => r.Kind, r => r.Label).Append(new(ClientKind, ClientLabel)).ToDictionary();

    public static (string Masked, IReadOnlyList<MaskHit> Hits) Mask(string text, IReadOnlyCollection<string> clientNames)
    {
        var counts = new Dictionary<string, int>();
        var masked = text;
        // 고객사 이름을 먼저 가린다. 자리표시가 생긴 뒤에 돌면 "카드" 같은 이름이 [카드번호]를 깨뜨린다.
        var names = clientNames.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length).ToList();
        if (names.Count > 0)
        {
            var pattern = new Regex(string.Join('|', names.Select(Regex.Escape)), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            masked = pattern.Replace(masked, _ =>
            {
                counts[ClientKind] = counts.GetValueOrDefault(ClientKind) + 1;
                return $"[{ClientLabel}]";
            });
        }
        foreach (var rule in Rules)
        {
            masked = rule.Pattern.Replace(masked, m =>
            {
                if (rule.Check is not null && !rule.Check(m.Value)) return m.Value;
                counts[rule.Kind] = counts.GetValueOrDefault(rule.Kind) + 1;
                return $"[{rule.Label}]";
            });
        }
        var hits = counts.Select(kv => new MaskHit(kv.Key, Labels[kv.Key], kv.Value)).ToList();
        return (masked, hits);
    }

    /// <summary>여러 번 가린 결과를 종류별로 합친다. 순서는 처음 나온 순.</summary>
    public static IReadOnlyList<MaskHit> Merge(IEnumerable<MaskHit> hits) =>
        hits.GroupBy(h => h.Kind).Select(g => new MaskHit(g.Key, g.First().Label, g.Sum(h => h.Count))).ToList();

    /// <summary>
    /// 가린 글을 AI 계약 한도로 자른다. 자를 위치가 자리표시(<c>[…]</c>) 안이면 <c>[</c> 앞에서 자른다.
    /// </summary>
    public static string Clip(string text, int max)
    {
        if (text.Length <= max) return text;
        var cut = max;
        var open = text.LastIndexOf('[', cut - 1);
        if (open >= 0 && text.IndexOf(']', open) >= cut) cut = open;
        return text[..cut];
    }

    private static bool IsBirthDate(string s)
    {
        var month = int.Parse(s.AsSpan(2, 2));
        var day = int.Parse(s.AsSpan(4, 2));
        return month is >= 1 and <= 12 && day is >= 1 and <= 31;
    }

    private static bool PassesLuhn(string s)
    {
        var digits = s.Where(char.IsAsciiDigit).Select(c => c - '0').ToArray();
        if (digits.Length is < 13 or > 19) return false;
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i];
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }

    /// <summary>날짜로 시작하는 하이픈 숫자열(2026-11-24, 2026-11-24-01)은 계좌로 보지 않는다.</summary>
    private static readonly Regex DateLike = new(@"^(19|20)\d{2}-(0?[1-9]|1[0-2])-(0?[1-9]|[12]\d|3[01])(-|$)", O);

    private static bool IsAccountLike(string s)
    {
        if (DateLike.IsMatch(s)) return false;
        var n = s.Count(char.IsAsciiDigit);
        return n is >= 10 and <= 14;
    }
}
