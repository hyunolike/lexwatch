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

    [Fact]
    public void Enforcement_decree_sentence_is_not_effective_check() =>
        Assert.DoesNotContain(Src("시행령 개정안은 2030년 1월 1일 공포된다.", "2030. 1. 1."), x => x.RuleId == "C2-EFFECTIVE");

    [Fact]
    public void Effective_date_plus_other_date_is_not_flagged() =>
        Assert.DoesNotContain(
            Src("2026년 12월 1일 시행되며 유예는 2027년 6월 1일까지다.", "2026. 12. 1. 시행 2027. 6. 1."),
            x => x.RuleId == "C2-EFFECTIVE");

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
