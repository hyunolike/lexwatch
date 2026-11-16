namespace LexWatch.Api.Ai;

/// <summary>
/// AI 서비스에 공유 토큰(<c>X-Ai-Token</c>)을 붙인다. 값은 환경 변수 <c>AI_TOKEN</c>이고, 비어 있으면 붙이지 않는다(로컬).
/// 생성된 클라이언트(AiClient.g.cs)를 건드리지 않으려고 HttpClient 파이프라인에서 붙인다. 값은 로그로 남기지 않는다.
/// </summary>
public sealed class AiTokenHandler(Func<string?>? tokenSource = null) : DelegatingHandler
{
    public const string HeaderName = "X-Ai-Token";

    private readonly Func<string?> _token = tokenSource ?? (() => Environment.GetEnvironmentVariable("AI_TOKEN"));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = _token()?.Trim();
        request.Headers.Remove(HeaderName);
        if (!string.IsNullOrEmpty(token)) request.Headers.TryAddWithoutValidation(HeaderName, token);
        return base.SendAsync(request, ct);
    }
}
