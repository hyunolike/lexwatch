using LexWatch.Api.LawApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        var law = Assert.IsType<LawApiResult<LawBody>.Ok>(await c.GetLawAsync("001872", "100001")).Value;
        Assert.Equal("근로기준법", law.LawName);
        Assert.Equal(["제50조", "제56조"], law.Articles.Select(a => a.Number));
        var on = Assert.IsType<LawApiResult<OldNewBody>.Ok>(await c.GetOldNewAsync("001872", "100001")).Value;
        Assert.True(on.Exists);
        var a50 = on.Articles.Single(a => a.Number == "제50조");
        Assert.Contains("구조문 예시", a50.OldText);
        Assert.Contains("40시간", a50.NewText);
    }

    [Fact]
    public async Task Calls_use_mst_when_given_and_id_otherwise()
    {
        var urls = new List<string>();
        var c = LawFixtures.Client(r => { urls.Add(r.RequestUri!.Query); return LawFixtures.Recorded(r); });
        await c.GetLawAsync("001872", "100001");
        await c.GetOldNewAsync("001872", "100001");
        await c.GetOldNewAsync("001872");
        Assert.Contains("MST=100001", urls[0]);
        Assert.Contains("MST=100001", urls[1]);
        Assert.DoesNotContain("ID=", urls[1]);
        Assert.Contains("ID=001872", urls[2]);
    }

    [Fact]
    public async Task List_carries_mst()
    {
        var c = LawFixtures.Client(LawFixtures.Recorded);
        var r = Assert.IsType<LawApiResult<IReadOnlyList<LawListItem>>.Ok>(
            await c.ListPromulgatedAsync(new(2026, 11, 3), new(2026, 11, 4)));
        Assert.Equal(["100001", "100002", "100003"], r.Value.Select(i => i.Mst));
    }

    [Fact]
    public async Task Oc_never_reaches_logs_through_the_di_pipeline()
    {
        var sink = new CapturingLoggerProvider();
        var sc = new ServiceCollection();
        sc.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(sink));
        sc.AddLawApi();
        sc.AddSingleton(new LawApiOptions("OCSECRET"));
        sc.AddHttpClient<LawApiClient>().ConfigurePrimaryHttpMessageHandler(() => new StubHandler(LawFixtures.Recorded));
        await using var sp = sc.BuildServiceProvider();
        var client = sp.GetRequiredService<LawApiClient>();
        Assert.IsType<LawApiResult<LawBody>.Ok>(await client.GetLawAsync("001872", "100001"));
        Assert.IsType<LawApiResult<LawBody>.Error>(await client.GetLawAsync("x", "999")); // 오류 본문 경로
        Assert.DoesNotContain(sink.Lines, l => l.Contains("OCSECRET"));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Lines { get; } = [];
        public ILogger CreateLogger(string category) => new L(this);
        public void Dispose() { }
        private sealed class L(CapturingLoggerProvider p) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> f)
            {
                lock (p.Lines) p.Lines.Add(f(state, ex) + " " + ex);
            }
        }
    }
}
