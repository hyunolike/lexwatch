using LexWatch.Api.Ai;
using LexWatch.Api.Workflow;

namespace LexWatch.Tests;

public class AiGatewayTests
{
    private static Task<AiOutcome<EmbedResponse>> Embed(AiGateway g, int attempt = 1) =>
        g.EmbedAsync(["x"], EmbedRequestInput_type.Query, attempt, default);

    [Fact]
    public async Task Rate_limited_maps_to_retry_with_after()
    {
        using var ai = new AiStub();
        ai.Json("/embed", 429, new { code = "LLM_RATE_LIMITED", retry_after_s = 12 });

        var r = await Embed(ai.Gateway());

        var retry = Assert.IsType<StepResult.Retry>(r.Failure);
        Assert.Equal("LLM_RATE_LIMITED", retry.ErrorCode);
        Assert.Equal(TimeSpan.FromSeconds(12), retry.After);
    }

    [Fact]
    public async Task Second_output_invalid_maps_to_needs_review()
    {
        using var ai = new AiStub();
        ai.Json("/embed", 422, new { code = "LLM_OUTPUT_INVALID", detail = "bad json" });
        var gw = ai.Gateway();

        var first = Assert.IsType<StepResult.Retry>((await Embed(gw, 1)).Failure);
        Assert.Equal("LLM_OUTPUT_INVALID", first.ErrorCode);
        Assert.Null(first.After);

        var second = Assert.IsType<StepResult.NeedsReview>((await Embed(gw, 2)).Failure);
        Assert.Equal("LLM_OUTPUT_INVALID", second.Reason);
    }

    [Fact]
    public async Task Request_validation_422_without_code_fails_instead_of_retrying()
    {
        using var ai = new AiStub();
        ai.Json("/embed", 422, new { detail = new[] { new { loc = new[] { "body" }, msg = "x", type = "y" } } });
        var f = Assert.IsType<StepResult.Failed>((await Embed(ai.Gateway())).Failure);
        Assert.Equal("AI_REQUEST_INVALID", f.ErrorCode);
    }

    [Fact]
    public async Task Upstream_502_maps_to_retry_with_service_code()
    {
        using var ai = new AiStub();
        ai.Json("/embed", 502, new { code = "RETRIEVER_ERROR", detail = "down" });
        var r = Assert.IsType<StepResult.Retry>((await Embed(ai.Gateway())).Failure);
        Assert.Equal("RETRIEVER_ERROR", r.ErrorCode);
    }

    [Theory]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Proxy_5xx_without_code_maps_to_ai_waking(int status)
    {
        // 잠든 Render 서비스는 프록시가 본문 코드 없이 502, 503을 돌려준다(2026-11-25 수집 실패, ai-wake-retry WR-01).
        using var ai = new AiStub();
        ai.Json("/embed", status, "Service Unavailable");
        var r = Assert.IsType<StepResult.Retry>((await Embed(ai.Gateway())).Failure);
        Assert.Equal("AI_WAKING", r.ErrorCode);
        Assert.Contains(status.ToString(), r.Message);
    }

    [Fact]
    public async Task App_500_without_code_stays_upstream_error()
    {
        using var ai = new AiStub();
        ai.Json("/embed", 500, "Internal Server Error");
        var r = Assert.IsType<StepResult.Retry>((await Embed(ai.Gateway())).Failure);
        Assert.Equal("AI_UPSTREAM_ERROR", r.ErrorCode);
    }

    [Fact]
    public async Task Timeout_maps_to_ai_timeout()
    {
        using var ai = new AiStub();
        ai.Json("/embed", 200, new { vectors = new object[0], provider = AiStub.Provider() }, delayMs: 2000);
        var gw = ai.Gateway(new AiGatewayOptions(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300)));
        var r = Assert.IsType<StepResult.Retry>((await Embed(gw)).Failure);
        Assert.Equal("AI_TIMEOUT", r.ErrorCode);
    }

    [Fact]
    public async Task Critique_waits_for_the_long_timeout()
    {
        // 추론 모델의 크리틱은 운영에서 27~52초가 걸렸다. 분석과 같은 긴 대기 시간을 쓴다.
        using var ai = new AiStub();
        ai.Json("/critique", 200, new { findings = new object[0], provider = AiStub.Provider() }, delayMs: 800);
        var gw = ai.Gateway(new AiGatewayOptions(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(5)));
        var r = await gw.CritiqueAsync(new CritiqueRequest
        {
            Run_id = "r", Memo = [new MemoSentence { Sentence = "s", Citations = ["c"] }], Evidence = [], Use_rules = true,
        }, 1, CancellationToken.None);
        Assert.True(r.Ok);
    }

    [Fact]
    public async Task Connection_failure_maps_to_ai_unavailable()
    {
        using var ai = new AiStub();
        var gw = new AiGateway(ai.Client("http://127.0.0.1:1"), AiGatewayOptions.Default);
        var r = Assert.IsType<StepResult.Retry>((await Embed(gw)).Failure);
        Assert.Equal("AI_UNAVAILABLE", r.ErrorCode);
    }

    [Fact]
    public async Task Success_returns_value()
    {
        using var ai = new AiStub();
        ai.Json("/embed", 200, new { vectors = new[] { new[] { 1.0, 0.0 } }, provider = AiStub.Provider("fallback", "lexical") });
        var r = await Embed(ai.Gateway());
        Assert.Null(r.Failure);
        Assert.Equal("fallback", r.Value!.Provider.Kind.ToString().ToLowerInvariant());
    }
}
