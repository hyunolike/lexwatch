using LexWatch.Api.LawApi;

namespace LexWatch.Tests;

/// <summary>2026-11-19 국가법령정보센터 실응답 녹화본(fixtures/lawapi/recorded)으로 파서를 확인한다.</summary>
public class LawApiRecordedTests
{
    private static string Rec(string name) => LawFixtures.Read(Path.Combine("recorded", name));

    private static LawApiClient Client() => LawFixtures.Client(r =>
    {
        var q = r.RequestUri!.Query;
        if (r.RequestUri.AbsolutePath.EndsWith("lawSearch.do")) return LawFixtures.Json(Rec("list_recent.json"));
        var mst = System.Text.RegularExpressions.Regex.Match(q, @"MST=(\d+)").Groups[1].Value;
        var id = mst switch { "291575" => "005698", "291383" => "002065", "291875" => "004750", _ => "" };
        return LawFixtures.Json(Rec(q.Contains("target=oldAndNew") ? $"oldnew_{id}.json" : $"law_{id}.json"));
    });

    [Fact]
    public async Task Real_list_parses()
    {
        var r = Assert.IsType<LawApiResult<IReadOnlyList<LawListItem>>.Ok>(
            await Client().ListPromulgatedAsync(new(2026, 10, 20), new(2026, 11, 19)));
        // 녹화본은 한 페이지(100건)만 있어 같은 페이지를 반복해서 받더라도 항목 파싱만 본다.
        Assert.Contains(r.Value, x => x.LawId == "005698" && x.Mst == "291575" && x.LawName == "가정보호심판규칙");
    }

    [Fact]
    public async Task Real_law_article_text_includes_paragraphs()
    {
        var law = Assert.IsType<LawApiResult<LawBody>.Ok>(await Client().GetLawAsync("005698", "291575")).Value;
        var a7 = Assert.Single(law.Articles, a => a.Number == "제7조");
        Assert.StartsWith("제7조(검사에 대한 결정의 통지)", a7.Text);
        Assert.Contains("① 법 제12조에 따라 송치 결정을", a7.Text);
        Assert.Contains("② 임시조치", a7.Text);
    }

    [Fact]
    public async Task Real_old_new_parses_object_entries_and_groups_paragraphs()
    {
        var on = Assert.IsType<LawApiResult<OldNewBody>.Ok>(await Client().GetOldNewAsync("005698", "291575")).Value;
        Assert.True(on.Exists);
        Assert.Equal(["제7조", "제8조", "제10조", "제45조"], on.Articles.Select(a => a.Number));
        var a7 = on.Articles[0];
        Assert.Contains("② 임시조치", a7.NewText);
        Assert.Contains("공소청", a7.NewText);
        Assert.Contains("검찰청", a7.OldText);
        Assert.All(on.Articles, a => Assert.DoesNotContain("<P>", (a.OldText ?? "") + (a.NewText ?? "")));
    }

    [Fact]
    public async Task Real_old_new_keeps_official_change_marks_as_ranges()
    {
        var on = Assert.IsType<LawApiResult<OldNewBody>.Ok>(await Client().GetOldNewAsync("005698", "291575")).Value;
        var a7 = on.Articles[0];
        string Cut(string text, (int Start, int End) r) => text[r.Start..r.End];
        Assert.Equal(
            ["공소청(광역공소청, 지방공소청 및 지방공소청 지청을 포함한다. 이하 같다)", "공소청"],
            a7.NewMarks.Select(r => Cut(a7.NewText!, r)));
        Assert.Equal(["검찰청", "검찰청"], a7.OldMarks.Select(r => Cut(a7.OldText!, r)));
    }

    [Fact]
    public async Task Real_old_new_absent_is_not_exists()
    {
        var on = Assert.IsType<LawApiResult<OldNewBody>.Ok>(await Client().GetOldNewAsync("002065", "291383")).Value;
        Assert.False(on.Exists);
    }
}
