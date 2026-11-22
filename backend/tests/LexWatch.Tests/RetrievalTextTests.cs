using System.Text.Json;
using LexWatch.Api.Retrieval;

namespace LexWatch.Tests;

public class RetrievalTextTests
{
    private record Golden(string Text, string[] Tokens);

    [Fact]
    public void Tokenizer_matches_shared_golden_file()
    {
        // ai/app/rag/lexical.py의 tokenize와 같은 파일을 통과해야 색인과 질의 토큰이 맞는다(RAG-03).
        var path = Path.Combine(Path.GetDirectoryName(LawFixtures.Dir)!, "retrieval", "tokenizer_golden.json");
        var cases = JsonSerializer.Deserialize<List<Golden>>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.NotEmpty(cases);
        foreach (var c in cases) Assert.True(c.Tokens.SequenceEqual(KoreanTokenizer.Tokenize(c.Text)), c.Text);
    }

    [Fact]
    public void Query_tokens_are_distinct_and_capped()
    {
        var q = KoreanTokenizer.QueryTokens(string.Join(" ", Enumerable.Range(0, 100).Select(i => $"단어{i}")), 64);
        Assert.Equal(64, q.Length);
        Assert.Equal(q.Length, q.Distinct().Count());
    }

    [Theory]
    [InlineData("제2조", "제10조")]
    [InlineData("제10조", "제10조의2")]
    [InlineData("제10조의2", "제10조의10")]
    [InlineData("제99조", "제100조")]
    [InlineData("제100조", "부칙")]
    public void Article_order_is_numeric(string before, string after)
    {
        Assert.True(ArticleOrder.Compare(before, after) < 0);
        Assert.True(ArticleOrder.Compare($"law:001872:{before}", $"law:001872:{after}") < 0);
    }
}
