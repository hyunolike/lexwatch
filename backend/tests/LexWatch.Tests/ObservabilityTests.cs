using System.Diagnostics;
using LexWatch.Api.Data;
using LexWatch.Api.Observability;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;

namespace LexWatch.Tests;

public class ObservabilityTests
{
    [Fact]
    public void NewTraceId는_W3C_형식이고_실행마다_다르다()
    {
        var a = Telemetry.NewTraceId();
        Assert.Matches("^[0-9a-f]{32}$", a);
        Assert.NotEqual(a, Telemetry.NewTraceId());
    }

    [Fact]
    public void StartStep은_실행의_TraceId를_이어받는다()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == Telemetry.WorkflowSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        var trace = Telemetry.NewTraceId();
        using var a = Telemetry.StartStep(trace, "analyze", 2);
        Assert.NotNull(a);
        Assert.Equal(trace, a!.TraceId.ToString());
        Assert.Contains(a.Tags, t => t.Key == "step" && t.Value == "analyze");
    }

    [Theory]
    [InlineData("replay-demo")]
    [InlineData("")]
    public void StartStep은_형식이_다른_TraceId에도_죽지_않는다(string trace)
    {
        using var a = Telemetry.StartStep(trace, "fetch_articles", 1);
        Assert.True(a is null || a.TraceId != default);
    }

    [Fact]
    public void 법제처_OC_쿼리가_있는_호출과_health는_추적하지_않는다()
    {
        Assert.False(Telemetry.ShouldTraceOutbound(new Uri("https://www.law.go.kr/DRF/lawSearch.do?OC=secret&target=law")));
        Assert.True(Telemetry.ShouldTraceOutbound(new Uri("https://ai.example.com/analyze")));
        Assert.False(Telemetry.ShouldTraceRequest(new PathString("/health")));
        Assert.True(Telemetry.ShouldTraceRequest(new PathString("/api/runs")));
    }

    [Fact]
    public void 스크러버가_IP와_쿼리_태그를_지운다()
    {
        using var src = new ActivitySource("scrub-test");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "scrub-test",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var a = src.StartActivity("x")!;
        foreach (var k in SensitiveTagScrubber.Keys) a.SetTag(k, "v");
        a.SetTag("keep", "ok");
        new SensitiveTagScrubber().OnEnd(a);
        Assert.All(SensitiveTagScrubber.Keys, k => Assert.DoesNotContain(a.Tags, t => t.Key == k));
        Assert.Contains(a.Tags, t => t.Key == "keep");
    }

    [Fact]
    public void 연결_문자열_URI를_Npgsql_형식으로_바꾼다()
    {
        var cs = ConnectionStringNormalizer.Normalize("postgresql://postgres.abc:p%40ss@aws-0.pooler.supabase.com:5432/postgres?sslmode=require")!;
        var b = new Npgsql.NpgsqlConnectionStringBuilder(cs);
        Assert.Equal("aws-0.pooler.supabase.com", b.Host);
        Assert.Equal(5432, b.Port);
        Assert.Equal("postgres.abc", b.Username);
        Assert.Equal("p@ss", b.Password);
        Assert.Equal("postgres", b.Database);
        Assert.Equal(Npgsql.SslMode.Require, b.SslMode);
        Assert.Equal("Host=db;Database=x", ConnectionStringNormalizer.Normalize("Host=db;Database=x"));
        Assert.Null(ConnectionStringNormalizer.Normalize(""));
    }
}
