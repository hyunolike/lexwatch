using LexWatch.Api.Governance;

namespace LexWatch.Tests;

/// <summary>ai-governance GV-01~03: 민감정보 가리기.</summary>
public class SensitiveDataMaskerTests
{
    private static readonly string[] Clients = ["한빛전자", "Garam Bio"];

    private static (string Masked, Dictionary<string, int> Hits) M(string text, IReadOnlyCollection<string>? clients = null)
    {
        var (masked, hits) = SensitiveDataMasker.Mask(text, clients ?? Clients);
        return (masked, hits.ToDictionary(h => h.Kind, h => h.Count));
    }

    [Theory]
    [InlineData("대표 900101-1234567 건", "대표 [주민등록번호] 건", "rrn")]
    [InlineData("번호 9001011234567 확인", "번호 [주민등록번호] 확인", "rrn")]
    [InlineData("외국인 851231-5123456", "외국인 [주민등록번호]", "rrn")]
    [InlineData("띄어 쓴 900101 1234567 번호", "띄어 쓴 [주민등록번호] 번호", "rrn")]
    [InlineData("연락처 010 1234 5678", "연락처 [전화번호]", "phone")]
    [InlineData("연락처 010-1234-5678로", "연락처 [전화번호]로", "phone")]
    [InlineData("사무실 02-123-4567", "사무실 [전화번호]", "phone")]
    [InlineData("휴대폰 01012345678", "휴대폰 [전화번호]", "phone")]
    [InlineData("메일 kim.lee@example.co.kr 로", "메일 [이메일] 로", "email")]
    [InlineData("카드 4111 1111 1111 1111 결제", "카드 [카드번호] 결제", "card")]
    [InlineData("카드 4111-1111-1111-1111", "카드 [카드번호]", "card")]
    [InlineData("사업자 123-45-67890 법인", "사업자 [사업자등록번호] 법인", "brn")]
    [InlineData("계좌 110-123-456789 입금", "계좌 [계좌번호] 입금", "account")]
    [InlineData("여권 M12345678 사본", "여권 [여권번호] 사본", "passport")]
    public void Masks_each_kind(string input, string expected, string kind)
    {
        var (masked, hits) = M(input);
        Assert.Equal(expected, masked);
        Assert.Equal(1, Assert.Single(hits, h => h.Key == kind).Value);
    }

    [Theory]
    [InlineData("제2조의3제1항이 바뀌나요?")]
    [InlineData("시행일은 2026-11-24인가요?")]
    [InlineData("공포일 2026. 11. 24. 기준")]
    [InlineData("과태료가 3,000,000원으로 올랐나요?")]
    [InlineData("가산율이 100분의 50에서 100분의 70으로 바뀌나요?")]
    [InlineData("시행령 제12조제1항과 법률 제20123호")]
    [InlineData("1천분의 3 비율과 50% 기준")]
    [InlineData("근로자 300명 이상 사업장, 2027년 1월 1일 시행")]
    [InlineData("카드처럼 보이는 1234 5678 9012 3456 은 Luhn 불통과")]
    [InlineData("생년월일 아닌 901301-1234567")]
    [InlineData("기간 2026-11-24 2026-12-31 사이")]
    [InlineData("공고 2026-11-24-01 과 2026-11-24-1234")]
    public void Leaves_legal_text_alone(string input)
    {
        var (masked, hits) = M(input);
        Assert.Equal(input, masked);
        Assert.Empty(hits);
    }

    [Fact]
    public void Masks_registered_client_names_case_insensitively()
    {
        var (masked, hits) = M("한빛전자에도 적용되나요? garam bio는요?");
        Assert.Equal("[고객사]에도 적용되나요? [고객사]는요?", masked);
        Assert.Equal(2, hits["client"]);
    }

    [Fact]
    public void Client_names_go_first_so_placeholders_stay_intact()
    {
        var (masked, hits) = M("카드 4111-1111-1111-1111", ["카드"]);
        Assert.Equal("[고객사] [카드번호]", masked);
        Assert.Equal(new Dictionary<string, int> { ["client"] = 1, ["card"] = 1 }, hits);
    }

    [Fact]
    public void Client_names_match_as_substrings()
    {
        Assert.Equal("[고객사]공업 건", M("한빛전자공업 건").Masked);
    }

    [Fact]
    public void Counts_several_hits_and_kinds()
    {
        var (masked, hits) = M("한빛전자 대표 900101-1234567, 010-1234-5678, 010-9876-5432");
        Assert.Equal("[고객사] 대표 [주민등록번호], [전화번호], [전화번호]", masked);
        Assert.Equal(new Dictionary<string, int> { ["rrn"] = 1, ["phone"] = 2, ["client"] = 1 }, hits);
    }

    [Fact]
    public void Empty_client_list_only_masks_formats()
    {
        var (masked, hits) = M("한빛전자 010-1234-5678", []);
        Assert.Equal("한빛전자 [전화번호]", masked);
        Assert.Equal(["phone"], hits.Keys);
    }

    [Theory]
    [InlineData("abc", 5, "abc")]
    [InlineData("가나다라마바", 3, "가나다")]
    [InlineData("질문 [주민등록번호] 끝", 6, "질문 ")]
    [InlineData("질문 [고객사]", 8, "질문 [고객사]")]
    public void Clip_never_cuts_inside_a_placeholder(string text, int max, string expected) =>
        Assert.Equal(expected, SensitiveDataMasker.Clip(text, max));

    [Fact]
    public void Masks_the_whole_text_before_clipping()
    {
        var text = new string('가', 1995) + " 900101-1234567";
        var (masked, _) = SensitiveDataMasker.Mask(text, []);
        Assert.Equal(new string('가', 1995) + " ", SensitiveDataMasker.Clip(masked, 2000));
    }

    [Fact]
    public void Merge_sums_by_kind()
    {
        var merged = SensitiveDataMasker.Merge([new("phone", "전화번호", 1), new("client", "고객사", 2), new("phone", "전화번호", 3)]);
        Assert.Equal([new MaskHit("phone", "전화번호", 4), new MaskHit("client", "고객사", 2)], merged);
    }
}
