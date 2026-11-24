using LexWatch.Api.Ai;
using LexWatch.Api.Critic;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using LexWatch.Api.Workflow.Steps;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

public class CriticTests
{
    private static readonly LawChange Change = new() { EffectiveDate = new DateOnly(2026, 12, 1) };
    private static MemoSentenceDto S(string s, params string[] c) => new(s, c);
    private static Dictionary<string, string> Ev(string id, string body) => new() { [id] = body };

    [Fact]
    public void Unknown_citation_id_is_flagged()
    {
        var f = CitationCritic.Check([S("해고가 제한된다.", "law:L1:제99조")], new HashSet<string> { "law:L1:제23조" });
        Assert.Equal("C1-UNKNOWN", Assert.Single(f).RuleId);
    }

    [Fact]
    public void Sentence_without_citation_is_flagged()
    {
        var f = CitationCritic.Check([S("근거 없는 문장.")], new HashSet<string>());
        var x = Assert.Single(f);
        Assert.Equal(("C1-NOCITE", 0), (x.RuleId, x.SentenceIndex));
    }

    [Fact]
    public void Article_number_not_in_cited_evidence_is_flagged()
    {
        var f = SourceMatchCritic.Check([S("제24조에 따라 해고가 제한된다.", "law:L1:제23조")],
            Ev("law:L1:제23조", "제23조 사용자는 해고를 하지 못한다."), Change);
        Assert.Contains(f, x => x.RuleId == "C2-MISMATCH");
    }

    [Fact]
    public void Article_with_branch_number_is_not_matched_by_base_article()
    {
        var f = SourceMatchCritic.Check([S("제23조의2가 신설된다.", "e")], Ev("e", "제23조 본문"), Change);
        Assert.Contains(f, x => x.RuleId == "C2-MISMATCH");
    }

    [Fact]
    public void Effective_date_mismatch_is_flagged()
    {
        var f = SourceMatchCritic.Check([S("2027년 1월 1일부터 시행한다.", "e")], Ev("e", "2027. 1. 1. 부터 시행"), Change);
        Assert.Equal("C2-EFFECTIVE", Assert.Single(f).RuleId);
    }

    [Fact]
    public void Effective_rule_skipped_when_date_unknown()
    {
        var f = SourceMatchCritic.Check([S("2027년 1월 1일부터 시행한다.", "e")], Ev("e", "2027. 1. 1."), new LawChange());
        Assert.Empty(f);
    }

    private static IReadOnlyList<CriticFindingDto> Src(string sentence, string body) =>
        SourceMatchCritic.Check([S(sentence, "e")], Ev("e", body), Change);

    [Fact]
    public void Year_only_and_year_month_are_not_periods() =>
        Assert.Empty(Src("2026년 12월 시행 예정이며 2026년 시행.", "2026년 12월 시행"));

    [Fact]
    public void Bare_comma_is_not_an_amount() =>
        Assert.Empty(Src("해고, 원칙적으로 제한된다.", "제23조 해고 제한"));

    [Fact]
    public void Amount_notations_are_equivalent()
    {
        Assert.Empty(Src("과태료 1,000만원.", "과태료 10,000,000원"));
        Assert.Empty(Src("과태료 1천만 원.", "과태료 1,000만원"));
        Assert.Empty(Src("과태료 3천만원.", "과태료 30,000,000원"));
        Assert.Empty(Src("벌금 1억 원.", "벌금 100,000,000원"));
        Assert.Contains(Src("과태료 2천만 원.", "과태료 1,000만원"), x => x.RuleId == "C2-MISMATCH");
    }

    [Theory]
    [InlineData("500원", "500원")]
    [InlineData("3천만원", "30,000,000원")]
    [InlineData("3천500만 원", "35,000,000원")]
    [InlineData("1억 2천만 원", "120,000,000원")]
    [InlineData("1억 2천500만 원", "125,000,000원")]
    [InlineData("5백만원", "5,000,000원")]
    [InlineData("2천 5백만원", "25,000,000원")]
    [InlineData("1,000만원", "10,000,000원")]
    [InlineData("5천원", "5,000원")]
    public void Mixed_korean_amounts_parse_to_won(string a, string b)
    {
        Assert.Empty(Src($"과태료 {a}.", $"과태료 {b}"));
        Assert.Empty(Src($"과태료 {b}.", $"과태료 {a}"));
    }

    [Fact]
    public void Wrong_mixed_amount_is_flagged() =>
        Assert.Contains(Src("과태료 3천500만 원.", "과태료 5,003,000원"), x => x.RuleId == "C2-MISMATCH");

    [Fact]
    public void Enforcement_decree_sentence_is_not_effective_check() =>
        Assert.DoesNotContain(Src("시행령 개정안은 2030년 1월 1일 공포된다.", "2030. 1. 1."), x => x.RuleId == "C2-EFFECTIVE");

    [Fact]
    public void Effective_date_plus_other_date_is_not_flagged() =>
        Assert.DoesNotContain(
            Src("2026년 12월 1일 시행되며 유예는 2027년 6월 1일까지다.", "2026. 12. 1. 시행 2027. 6. 1."),
            x => x.RuleId == "C2-EFFECTIVE");

    private const string Premium = "사용자는 연장근로에 대하여는 통상임금의 100분의 50 이상을 가산하여 지급하여야 한다.";

    [Fact]
    public void Wrong_ratio_is_flagged()
    {
        var f = Src("연장근로 가산임금은 통상임금의 100분의 70 이상으로 바뀐다.", Premium);
        Assert.Equal(("C2-MISMATCH", "인용한 근거 본문에 없는 표기: 100분의 70"), (Assert.Single(f).RuleId, f[0].Message));
    }

    [Theory]
    [InlineData("통상임금의 100분의 50 이상을 가산한다.")]
    [InlineData("통상임금의 50% 이상을 가산한다.")]
    [InlineData("통상임금의 50 퍼센트 이상을 가산한다.")]
    [InlineData("통상임금의 2분의 1 이상을 가산한다.")]
    public void Equivalent_ratio_notations_pass(string sentence) => Assert.Empty(Src(sentence, Premium));

    [Theory]
    [InlineData("통상임금의 80% 이상을 가산한다.", "80%")]
    [InlineData("과징금은 매출액의 1천분의 40 이하다.", "1천분의 40")]
    [InlineData("의결정족수는 3분의 2다.", "3분의 2")]
    public void Ratio_not_in_evidence_is_flagged(string sentence, string display) =>
        Assert.Contains(Src(sentence, Premium + " 매출액의 100분의 3 이하, 재적 과반수"),
            x => x.RuleId == "C2-MISMATCH" && x.Message.EndsWith(display));

    [Fact]
    public void Thousandth_ratio_matches_equivalent_value() =>
        Assert.Empty(Src("과징금은 매출액의 1천분의 30 이하다.", "매출액의 100분의 3 이하"));

    [Fact]
    public void Two_thirds_matches_rounded_percent() =>
        Assert.Empty(Src("의결정족수는 66.67%다.", "재적 3분의 2 이상"));

    [Fact]
    public void Percent_point_is_not_a_ratio() =>
        Assert.Empty(Src("가산율이 20%p 오른다.", Premium));

    [Fact]
    public void Ratio_digits_are_not_reused_as_periods_or_amounts() =>
        Assert.DoesNotContain(Src("100분의 50 이상 가산한다.", "100분의 50 이상"), x => x.RuleId == "C2-MISMATCH");

    private const string Hours = "제50조(근로시간) ① 1주 간의 근로시간은 휴게시간을 제외하고 40시간을 초과할 수 없다. 제53조 ① 1주 간에 12시간을 한도로 근로시간을 연장할 수 있다.";

    [Theory]
    [InlineData("연장근로 한도는 주 16시간으로 늘어난다.", "16시간")]
    [InlineData("1주 근로시간 한도는 48시간이다.", "48시간")]
    [InlineData("2주 단위로 평균한다.", "2주")]
    public void Hours_and_weeks_not_in_evidence_are_flagged(string sentence, string display) =>
        Assert.Contains(Src(sentence, Hours), x => x.RuleId == "C2-MISMATCH" && x.Message.EndsWith(display));

    [Theory]
    [InlineData("1주 근로시간은 40시간을 넘을 수 없고 연장은 12시간까지다.")]
    [InlineData("1주 40 시간 한도다.")]
    public void Hours_and_weeks_in_evidence_pass(string sentence) => Assert.Empty(Src(sentence, Hours));

    [Fact]
    public void Housing_count_is_not_a_week() =>
        Assert.Empty(Src("3주택 이상 보유자가 대상이다.", "다주택자"));

    [Fact]
    public void Clean_memo_has_no_findings()
    {
        var memo = new[] { S("제23조에 따라 2026년 12월 1일부터 시행하며 과태료는 1,000만 원, 유예는 30일이다.", "e") };
        var body = "제23조 ... 2026. 12. 1. 시행 ... 과태료 １０００만원 ... 30 일";
        Assert.Empty(SourceMatchCritic.Check(memo, Ev("e", body), Change));
        Assert.Empty(CitationCritic.Check(memo, new HashSet<string> { "e" }));
    }
}

[Collection("pg")]
public class CriticStepTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Steps_chain_store_findings_idempotently_and_mark_needs_review()
    {
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id);
        await using (var db = pg.CreateDb())
        {
            db.MemoDrafts.Add(new MemoDraft
            {
                Id = Guid.NewGuid(), GroupReviewId = g.Id, Version = 1, CreatedBy = "agent", OpenQuestionsJson = "[]",
                SentencesJson = """[{"sentence":"인용 없음","citations":[]},{"sentence":"제24조 위반","citations":["law:L1:제23조"]}]""",
            });
            await db.SaveChangesAsync();
        }
        var s1 = await AiSeed.StepAsync(pg, run.Id, "critic_citation", g.Id);
        var r1 = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, new CriticCitationStep(), s1.Id));
        Assert.Equal("critic_source", Assert.Single(r1.Next).Step);
        Assert.Equal("none", r1.ProviderKind);
        var s2 = await AiSeed.StepAsync(pg, run.Id, "critic_source", g.Id);
        var r2 = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, new CriticSourceStep(), s2.Id));
        Assert.Equal("critic_judge", Assert.Single(r2.Next).Step);
        await AiSeed.ExecuteAsync(pg, new CriticSourceStep(), s2.Id); // 재시도

        await using var v = pg.CreateDb();
        var rules = (await v.CriticFindings.ToListAsync()).Select(f => (f.Tier, f.RuleId)).OrderBy(x => x).ToList();
        Assert.Equal([(1, "C1-NOCITE"), (2, "C2-MISMATCH")], rules);
        Assert.Equal(ReviewStatus.NeedsReview, (await v.GroupReviews.SingleAsync(x => x.Id == g.Id)).Status);
    }
}
