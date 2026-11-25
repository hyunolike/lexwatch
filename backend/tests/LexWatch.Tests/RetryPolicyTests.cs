using LexWatch.Api.Workflow;

namespace LexWatch.Tests;

/// <summary>ai-wake-retry WR-02, WR-03. DB 없이 대기와 상한만 본다.</summary>
public class RetryPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 0, 0, 0, TimeSpan.Zero);
    private static StepResult.Retry R(string code, TimeSpan? after = null) => new(code, "x", after);
    private static double Seconds((bool retry, DateTimeOffset notBefore) d) => (d.notBefore - T0).TotalSeconds;

    [Theory]
    [InlineData("AI_WAKING")]
    [InlineData("AI_UNAVAILABLE")]
    public void Waking_errors_wait_from_30_seconds_doubling_to_a_120_second_cap(string code)
    {
        var waits = Enumerable.Range(1, 6).Select(a => Seconds(RetryPolicy.Decide("classify", a, R(code), T0, () => 0.5))).ToArray();
        Assert.Equal([30, 60, 120, 120, 120, 120], waits);
        Assert.Equal(570, waits.Sum()); // 7번째 시도 전까지 기다리는 시간의 합(지터 없음), 약 9분 30초
    }

    [Fact]
    public void Waking_delay_has_jitter_between_80_and_120_percent()
    {
        Assert.Equal(24, Seconds(RetryPolicy.Decide("classify", 1, R("AI_WAKING"), T0, () => 0.0)), 6);
        Assert.Equal(143.99, Seconds(RetryPolicy.Decide("classify", 5, R("AI_WAKING"), T0, () => 0.99999)), 1);
    }

    [Fact]
    public void Waking_errors_get_seven_attempts_even_on_short_steps()
    {
        Assert.True(RetryPolicy.Decide("analyze", 6, R("AI_WAKING"), T0).retry);
        Assert.False(RetryPolicy.Decide("analyze", 7, R("AI_WAKING"), T0).retry);
        Assert.False(RetryPolicy.Decide("classify", 7, R("AI_UNAVAILABLE"), T0).retry);
    }

    [Theory]
    [InlineData("AI_UPSTREAM_ERROR")]
    [InlineData("LLM_UPSTREAM")]
    [InlineData("AI_TIMEOUT")]
    public void Other_errors_keep_the_short_backoff_and_step_limit(string code)
    {
        Assert.Equal(2, Seconds(RetryPolicy.Decide("classify", 1, R(code), T0)));
        Assert.Equal(16, Seconds(RetryPolicy.Decide("classify", 4, R(code), T0)));
        Assert.False(RetryPolicy.Decide("classify", 5, R(code), T0).retry);
    }

    [Fact]
    public void Server_retry_after_wins_over_the_waking_delay()
    {
        Assert.Equal(12, Seconds(RetryPolicy.Decide("classify", 1, R("AI_WAKING", TimeSpan.FromSeconds(12)), T0)));
    }
}
