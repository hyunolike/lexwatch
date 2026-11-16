namespace LexWatch.Api.Workflow;

public static class RetryPolicy
{
    private const int DefaultMaxAttempts = 3;

    private static readonly Dictionary<string, int> MaxAttempts = new()
    {
        ["collect"] = 3,
        ["fetch_articles"] = 3,
        ["classify"] = 5,
        ["analyze"] = 3,
        ["critic_judge"] = 3,
        ["notify"] = 3,
    };

    public static int MaxAttemptsFor(string step) => MaxAttempts.GetValueOrDefault(step, DefaultMaxAttempts);

    /// <summary>
    /// attempt는 방금 실행한 시도 번호(1부터). 최대 시도에 도달하면 retry=false.
    /// 대기: After가 있으면 그것(429), 없으면 2^attempt초.
    /// </summary>
    public static (bool retry, DateTimeOffset notBefore) Decide(
        string step, int attempt, StepResult.Retry r, DateTimeOffset now)
    {
        if (attempt >= MaxAttemptsFor(step))
            return (false, now);
        var wait = r.After ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
        return (true, now + wait);
    }
}
