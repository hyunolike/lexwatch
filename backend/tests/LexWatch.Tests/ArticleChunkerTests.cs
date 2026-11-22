using LexWatch.Api.LawApi;
using LexWatch.Api.Retrieval;

namespace LexWatch.Tests;

public class ArticleChunkerTests
{
    private static ChunkSource Src(string id, string title, string body) => new(id, title, body);

    [Fact]
    public void Short_article_is_one_chunk_with_title_and_no_duplicate_heading()
    {
        var c = Assert.Single(ArticleChunker.Chunk(Src("law:011357:제29조", "개인정보 보호법 제29조(안전조치의무)",
            "제29조(안전조치의무) 개인정보처리자는 안전성 확보에 필요한 조치를 하여야 한다.")));
        Assert.Equal("law:011357:제29조#1", c.ChunkId);
        Assert.Equal("law:011357:제29조", c.EvidenceId);
        Assert.Equal("개인정보 보호법 제29조(안전조치의무)\n개인정보처리자는 안전성 확보에 필요한 조치를 하여야 한다.", c.Text);
    }

    [Fact]
    public void Long_article_splits_on_paragraph_boundaries_within_limit()
    {
        var p = string.Join("\n", Enumerable.Range(1, 4).Select(i => $"{"①②③④"[i - 1]} " + new string('가', 500) + "한다."));
        var chunks = ArticleChunker.Chunk(Src("law:1:제2조", "법 제2조(정의)", "제2조(정의)\n" + p));
        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Text.Length - "법 제2조(정의)\n".Length <= ArticleChunker.MaxChars));
        Assert.StartsWith("법 제2조(정의)\n① ", chunks[0].Text);
        Assert.StartsWith("법 제2조(정의)\n③ ", chunks[1].Text);
        Assert.Equal(["law:1:제2조#1", "law:1:제2조#2"], chunks.Select(c => c.ChunkId));
    }

    [Fact]
    public void Paragraph_longer_than_limit_splits_by_lines_then_sentences()
    {
        var items = string.Join("\n", Enumerable.Range(1, 6).Select(i => $"{i}. " + new string('나', 300) + "을 말한다."));
        var one = "① " + string.Join(" ", Enumerable.Range(1, 8).Select(_ => new string('다', 200) + "한다.")); // 한 줄 1,600자 이상
        var chunks = ArticleChunker.Chunk(Src("law:1:제3조", "법 제3조", items + "\n" + one));
        Assert.All(chunks, c => Assert.True(c.Text.Length - "법 제3조\n".Length <= ArticleChunker.MaxChars, $"{c.Text.Length}"));
        Assert.True(chunks.Count >= 3);
        // 나눠도 내용이 빠지거나 바뀌지 않는다(공백만 다를 수 있다).
        Assert.Equal(Squash(items + one), Squash(string.Concat(chunks.Select(c => c.Text["법 제3조\n".Length..]))));
    }

    private static string Squash(string s) => string.Concat(s.Where(ch => !char.IsWhiteSpace(ch)));

    [Theory]
    [InlineData("제5조 삭제 <2010.6.4>")]
    [InlineData("제5조(벌칙) 삭제 <2015.7.24>")]
    [InlineData("")]
    public void Deleted_or_empty_article_has_no_chunks(string body) =>
        Assert.Empty(ArticleChunker.Chunk(Src("law:1:제5조", "법 제5조", body)));

    [Theory]
    [InlineData("001872", 120, 1)]
    [InlineData("011357", 120, 1)]
    [InlineData("010199", 65, 1)]
    public async Task Recorded_laws_chunk_within_limit_and_map_back(string lawId, int minChunks, int minSplit)
    {
        var law = Assert.IsType<LawApiResult<LawBody>.Ok>(await RecordedCurrent.Client().GetLawAsync(lawId)).Value;
        var sources = law.Articles.Select(a => Src($"law:{lawId}:{a.Number}", $"{law.LawName} {a.Number}({a.Title})", a.Text)).ToList();
        var chunks = sources.SelectMany(ArticleChunker.Chunk).ToList();

        Assert.True(chunks.Count >= minChunks, $"{chunks.Count}");
        Assert.All(chunks, c => Assert.True(c.Text.Length <= ArticleChunker.MaxChars + 80, $"{c.ChunkId} {c.Text.Length}"));
        Assert.All(chunks, c => Assert.Contains(c.EvidenceId, sources.Select(s => s.EvidenceId)));
        Assert.True(chunks.GroupBy(c => c.EvidenceId).Count(g => g.Count() > 1) >= minSplit);
        Assert.Equal(chunks.Count, chunks.Select(c => c.ChunkId).Distinct().Count());
    }
}

/// <summary>fixtures/lawapi/recorded/current 녹화본(근로기준법, 개인정보 보호법, 전자금융거래법)을 돌려주는 클라이언트.</summary>
public static class RecordedCurrent
{
    public static LawApiClient Client() => LawFixtures.Client(r =>
    {
        var id = System.Text.RegularExpressions.Regex.Match(r.RequestUri!.Query, @"ID=(\d+)").Groups[1].Value;
        return LawFixtures.Json(LawFixtures.Read(Path.Combine("recorded", "current", $"law_{id}.json")));
    });
}
