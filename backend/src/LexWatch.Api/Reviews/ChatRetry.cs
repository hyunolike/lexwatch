using LexWatch.Api.Ai;
using LexWatch.Api.Workflow;

namespace LexWatch.Api.Reviews;

/// <summary>
/// 검토 보조 채팅의 AI 호출 재시도. 무료 호스팅의 AI 서비스는 잠들었다가 깨는 데 20~60초가 걸리고,
/// 그동안 게이트웨이가 5xx나 연결 실패를 바로 돌려준다. 이런 일시 오류만 마감 안에서 다시 시도한다.
/// 한도 초과(429)와 요청 거절은 다시 보내도 같으므로 바로 돌려준다.
/// </summary>
public static class ChatRetry
{
    public record Options(TimeSpan Deadline, TimeSpan PerAttempt, TimeSpan Delay, int MaxAttempts = 8)
    {
        public static readonly Options Default = new(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(4));

        /// <summary>테스트가 기다림을 줄일 수 있게 Chat:RetryDelayMs만 설정에서 받는다.</summary>
        public static Options From(IConfiguration cfg) =>
            int.TryParse(cfg["Chat:RetryDelayMs"], out var ms) ? Default with { Delay = TimeSpan.FromMilliseconds(ms) } : Default;
    }

    private static readonly HashSet<string> Transient = ["AI_UNAVAILABLE", "AI_UPSTREAM_ERROR", "AI_BAD_RESPONSE"];

    public static bool IsTransient(StepResult? failure) =>
        failure is StepResult.Retry r && (Transient.Contains(r.ErrorCode) || r.ErrorCode.StartsWith("AI_UPSTREAM", StringComparison.Ordinal));

    public static async Task<AiOutcome<T>> RunAsync<T>(
        Func<int, TimeSpan, Task<AiOutcome<T>>> call, Options opt, TimeProvider time, CancellationToken ct)
    {
        var start = time.GetTimestamp();
        for (var attempt = 1; ; attempt++)
        {
            var left = opt.Deadline - time.GetElapsedTime(start);
            var outcome = await call(attempt, left < opt.PerAttempt ? left : opt.PerAttempt);
            if (outcome.Failure is null || !IsTransient(outcome.Failure) || attempt >= opt.MaxAttempts) return outcome;
            // 다음 시도에 쓸 시간이 기다림보다 넉넉하지 않으면 멈춘다.
            if (opt.Deadline - time.GetElapsedTime(start) <= opt.Delay + TimeSpan.FromSeconds(5)) return outcome;
            await Task.Delay(opt.Delay, time, ct);
        }
    }
}
