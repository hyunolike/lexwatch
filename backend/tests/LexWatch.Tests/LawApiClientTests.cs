using LexWatch.Api.LawApi;

namespace LexWatch.Tests;

public class LawApiClientTests
{
    [Fact]
    public async Task Parses_recorded_list()
    {
        var c = LawFixtures.Client(LawFixtures.Recorded);
        var r = Assert.IsType<LawApiResult<IReadOnlyList<LawListItem>>.Ok>(
            await c.ListPromulgatedAsync(new(2026, 11, 3), new(2026, 11, 4)));
        Assert.Equal(3, r.Value.Count);
        var first = r.Value[0];
        Assert.Equal("001872", first.LawId);
        Assert.Equal("근로기준법", first.LawName);
        Assert.Equal(new DateOnly(2026, 11, 4), first.PromulgationDate);
        Assert.Equal(new DateOnly(2027, 5, 5), first.EffectiveDate);
    }

    [Fact]
    public async Task Empty_list_is_ok_not_error()
    {
        var c = LawFixtures.Client(_ => LawFixtures.Json(LawFixtures.Read("empty_list.json")));
        var r = Assert.IsType<LawApiResult<IReadOnlyList<LawListItem>>.Ok>(
            await c.ListPromulgatedAsync(new(2026, 11, 3), new(2026, 11, 4)));
        Assert.Empty(r.Value);
    }

    [Fact]
    public async Task Error_body_with_200_is_error()
    {
        var c = LawFixtures.Client(_ => LawFixtures.Json(LawFixtures.Read("error_body.json")));
        var r = Assert.IsType<LawApiResult<IReadOnlyList<LawListItem>>.Error>(
            await c.ListPromulgatedAsync(new(2026, 11, 3), new(2026, 11, 4)));
        Assert.Equal("LAW_API_ERROR_BODY", r.Code);
    }

    [Fact]
    public async Task Error_body_with_200_is_error_for_law_and_oldnew()
    {
        var c = LawFixtures.Client(_ => LawFixtures.Json(LawFixtures.Read("error_body.json")));
        Assert.Equal("LAW_API_ERROR_BODY", Assert.IsType<LawApiResult<LawBody>.Error>(await c.GetLawAsync("001872")).Code);
        Assert.Equal("LAW_API_ERROR_BODY", Assert.IsType<LawApiResult<OldNewBody>.Error>(await c.GetOldNewAsync("001872")).Code);
    }

    [Fact]
    public async Task Http_failure_and_html_are_errors_without_leaking_oc()
    {
        var c = LawFixtures.Client(_ => LawFixtures.Json("oops", System.Net.HttpStatusCode.BadGateway));
        var e = Assert.IsType<LawApiResult<LawBody>.Error>(await c.GetLawAsync("001872"));
        Assert.Equal("LAW_API_HTTP_502", e.Code);
        Assert.DoesNotContain("OCSECRET", e.Message);
        var c2 = LawFixtures.Client(_ => LawFixtures.Json("<html>x</html>"));
        Assert.Equal("LAW_API_ERROR_BODY", Assert.IsType<LawApiResult<LawBody>.Error>(await c2.GetLawAsync("001872")).Code);
    }

    [Fact]
    public async Task Parses_law_and_oldnew()
    {
        var c = LawFixtures.Client(LawFixtures.Recorded);
        var law = Assert.IsType<LawApiResult<LawBody>.Ok>(await c.GetLawAsync("001872")).Value;
        Assert.Equal("근로기준법", law.LawName);
        Assert.Equal(["제50조", "제56조"], law.Articles.Select(a => a.Number));
        var on = Assert.IsType<LawApiResult<OldNewBody>.Ok>(await c.GetOldNewAsync("001872")).Value;
        Assert.True(on.Exists);
        var a50 = on.Articles.Single(a => a.Number == "제50조");
        Assert.Contains("구조문 예시", a50.OldText);
        Assert.Contains("40시간", a50.NewText);
    }
}
