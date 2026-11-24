using LexWatch.Api.Ai;
using LexWatch.Api.Reviews;
using LexWatch.Api.Workflow;

namespace LexWatch.Tests;

public class ChatRetryTests
{
    private static readonly ChatRetry.Options Fast = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), TimeSpan.Zero, MaxAttempts: 4);
    private static AiOutcome<string> Ok => new("답", null);
    private static AiOutcome<string> Fail(string code) => new(null, new StepResult.Retry(code, "x", null));

    [Fact]
    public async Task Waking_ai_is_retried_until_it_answers()
    {
        var calls = new Queue<AiOutcome<string>>([Fail("AI_UPSTREAM_ERROR"), Fail("AI_UNAVAILABLE"), Ok]);
        var n = 0;
        var r = await ChatRetry.RunAsync((_, _) => { n++; return Task.FromResult(calls.Dequeue()); }, Fast, TimeProvider.System, default);
        Assert.Equal(("답", 3), (r.Value, n));
    }

    [Theory]
    [InlineData("LLM_RATE_LIMITED")]
    [InlineData("AI_TIMEOUT")]
    public async Task Rate_limit_and_timeout_are_not_retried(string code)
    {
        var n = 0;
        var r = await ChatRetry.RunAsync((_, _) => { n++; return Task.FromResult(Fail(code)); }, Fast, TimeProvider.System, default);
        Assert.Equal(1, n);
        Assert.NotNull(r.Failure);
    }

    [Fact]
    public async Task Rejected_request_is_not_retried()
    {
        var n = 0;
        await ChatRetry.RunAsync((_, _) => { n++; return Task.FromResult(new AiOutcome<string>(null, new StepResult.Failed("AI_REQUEST_INVALID", "x"))); },
            Fast, TimeProvider.System, default);
        Assert.Equal(1, n);
    }

    [Fact]
    public async Task Persistent_outage_stops_at_max_attempts()
    {
        var n = 0;
        var r = await ChatRetry.RunAsync((_, _) => { n++; return Task.FromResult(Fail("AI_UPSTREAM_ERROR")); }, Fast, TimeProvider.System, default);
        Assert.Equal(4, n);
        Assert.NotNull(r.Failure);
    }

    [Fact]
    public async Task Per_attempt_timeout_never_exceeds_time_left()
    {
        var opt = new ChatRetry.Options(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(40), TimeSpan.Zero, 1);
        TimeSpan seen = default;
        await ChatRetry.RunAsync((_, t) => { seen = t; return Task.FromResult(Ok); }, opt, TimeProvider.System, default);
        Assert.True(seen <= TimeSpan.FromSeconds(8));
    }

    [Fact]
    public void Default_options_let_the_deadline_decide_not_the_attempt_count()
    {
        var d = ChatRetry.Options.Default;
        // 4초 간격으로 마감까지 시도할 수 있어야 한다. 잠든 AI 서비스가 깨는 데 2분 가까이 걸린 적이 있다.
        Assert.True(d.MaxAttempts * d.Delay.TotalSeconds >= d.Deadline.TotalSeconds);
        Assert.True(d.Deadline >= TimeSpan.FromSeconds(90));
    }
}
