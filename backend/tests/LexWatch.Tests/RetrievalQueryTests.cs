using LexWatch.Api.Retrieval;
using EvidenceRow = LexWatch.Api.Data.Entities.Evidence;

namespace LexWatch.Tests;

public class RetrievalQueryTests
{
    private static EvidenceRow OldNew(string title, string after, string? marks) => new()
    {
        EvidenceId = "oldnew:L1:제56조", Kind = "oldnew", Title = $"근로기준법 제56조({title}) 신구조문 대비",
        Body = $"[구조문]\n옛 조문\n\n[신조문]\n{after}", ChangeMarksJson = marks,
    };

    [Fact]
    public void New_segment_comes_from_marks_on_the_new_text_only()
    {
        var after = "제56조 사용자는 통상임금의 100분의 50 이상을 가산하여 지급하여야 한다.";
        var start = after.IndexOf("통상임금", StringComparison.Ordinal);
        var q = RetrievalQuery.Build("임금 관련", "근로기준법", [OldNew("연장·야간 및 휴일 근로", after, $$"""{"old":[[0,3]],"new":[[{{start}},{{start + 4}}]]}""")]);

        Assert.StartsWith("통상임금", q.Text);
        Assert.Equal("통상임금", q.Tokens[0]);
        Assert.Contains("연장", q.Tokens);                 // 괄호 안 조문 제목
        Assert.DoesNotContain("신구조문", q.Tokens);       // 잡음은 넣지 않는다
        Assert.DoesNotContain("근로기준법", q.Tokens);
        Assert.Contains("임금", q.Tokens);                 // 관심 분야
    }

    [Fact]
    public void Without_marks_uses_head_of_new_text_and_respects_quotas()
    {
        var after = string.Join(" ", Enumerable.Range(0, 200).Select(i => $"단어{i}"));
        var interest = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"관심{i}"));
        var q = RetrievalQuery.Build(interest, "법", [OldNew("제목", after, null)]);

        Assert.True(q.Tokens.Length <= RetrievalQuery.MaxTokens);
        // 앞 48개는 신조문 앞 200자에서, 관심 분야는 32개까지.
        var head = KoreanTokenizer.Tokenize(after[..RetrievalQuery.NoMarkHeadChars]).ToHashSet();
        Assert.All(q.Tokens.Take(RetrievalQuery.NewQuota), t => Assert.Contains(t, head));
        // 새 구간 48 + 제목 "제목" 1 + 관심 분야 32(앞에서 이미 나온 토큰은 빼고 새 것으로 채운다).
        Assert.Equal(RetrievalQuery.NewQuota + 1 + RetrievalQuery.InterestQuota, q.Tokens.Length);
        Assert.Contains(q.Tokens, t => t.StartsWith("관심"));
        Assert.Equal(q.Tokens.Length, q.Tokens.Distinct().Count());
    }

    [Fact]
    public void No_changed_articles_uses_interest_and_law_name()
    {
        var q = RetrievalQuery.Build("개인정보 처리", "개인정보 보호법", []);
        Assert.Contains("처리", q.Tokens);
        Assert.Contains("보호법", q.Tokens);
    }
}
