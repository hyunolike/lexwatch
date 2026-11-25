namespace LexWatch.Api.Workflow;

public static class RetryPolicy
{
    private const int DefaultMaxAttempts = 3;

    private static readonly Dictionary<string, int> MaxAttempts = new()
    {
        ["collect"] = 3,
        ["fetch_articles"] = 3,
        ["classify"] = 5,
        ["index_articles"] = 3,
        ["analyze"] = 3,
        ["critic_judge"] = 3,
        ["notify"] = 3,
    };

    /// <summary>
    /// AI 서비스가 꺼져 있어 요청이 앱에 닿지 않은 오류(Render 무료 플랜이 잠든 경우). 이 오류만 분 단위로 기다린다(ai-wake-retry WR-02).
    /// </summary>
    public static readonly IReadOnlySet<string> WakingCodes = new HashSet<string> { "AI_WAKING", "AI_UNAVAILABLE" };

    /// <summary>깨는 중 오류의 시도 상한. 지터 없는 대기 합 30+60+120×4 = 570초, 시도 시간까지 약 10분.</summary>
    public const int WakingMaxAttempts = 7;
    private static readonly TimeSpan WakingFirst = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WakingCap = TimeSpan.FromSeconds(120);

    public static int MaxAttemptsFor(string step) => MaxAttempts.GetValueOrDefault(step, DefaultMaxAttempts);

    /// <summary>
    /// attempt는 방금 실행한 시도 번호(1부터). 최대 시도에 도달하면 retry=false.
    /// 대기: After가 있으면 그것(429), 깨는 중 오류면 30초부터 2배씩 최대 120초에 지터, 그 밖에는 2^attempt초.
    /// </summary>
    /// <param name="unit">0 이상 1 미만의 난수. 테스트가 고정한다.</param>
    public static (bool retry, DateTimeOffset notBefore) Decide(
        string step, int attempt, StepResult.Retry r, DateTimeOffset now, Func<double>? unit = null)
    {
        var waking = WakingCodes.Contains(r.ErrorCode);
        var max = waking ? Math.Max(MaxAttemptsFor(step), WakingMaxAttempts) : MaxAttemptsFor(step);
        if (attempt >= max)
            return (false, now);
        var wait = r.After ?? (waking ? WakingDelay(attempt, unit ?? Random.Shared.NextDouble) : TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        return (true, now + wait);
    }

    private static TimeSpan WakingDelay(int attempt, Func<double> unit)
    {
        var baseSeconds = Math.Min(WakingFirst.TotalSeconds * Math.Pow(2, attempt - 1), WakingCap.TotalSeconds);
        return TimeSpan.FromSeconds(baseSeconds * (0.8 + 0.4 * unit()));
    }
}
