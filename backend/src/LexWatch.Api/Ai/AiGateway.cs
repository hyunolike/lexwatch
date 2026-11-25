using System.Text.Json;
using LexWatch.Api.Workflow;

namespace LexWatch.Api.Ai;

/// <param name="Standard">분류, 임베딩. AI 서비스 분류 예산(50초)보다 길어야 한다.</param>
/// <param name="Analyze">분석과 크리틱. 추론 모델의 크리틱은 운영에서 27~52초가 걸렸다. AI 서비스 예산(120초)보다 길어야 한다.</param>
public record AiGatewayOptions(TimeSpan Standard, TimeSpan Analyze)
{
    public static readonly AiGatewayOptions Default = new(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(130));
}

/// <summary>AI 호출 결과: 값이거나, 워커가 그대로 반환할 수 있는 실패 StepResult.</summary>
public sealed record AiOutcome<T>(T? Value, StepResult? Failure)
{
    public bool Ok => Failure is null;
}

/// <summary>생성 클라이언트를 감싸 HTTP 오류를 <see cref="StepResult"/>로 바꾼다.</summary>
public class AiGateway(IAiClient client, AiGatewayOptions options)
{
    public Task<AiOutcome<EmbedResponse>> EmbedAsync(IEnumerable<string> texts, EmbedRequestInput_type type, int attempt,
        CancellationToken ct) =>
        CallAsync((c, t) => c.EmbedAsync(new EmbedRequest { Texts = texts.ToList(), Input_type = type }, t),
            attempt, options.Standard, ct);

    public Task<AiOutcome<ClassifyResponse>> ClassifyAsync(ClassifyRequest req, int attempt, CancellationToken ct) =>
        CallAsync((c, t) => c.ClassifyAsync(req, t), attempt, options.Standard, ct);

    public Task<AiOutcome<AnalyzeResponse>> AnalyzeAsync(AnalyzeRequest req, int attempt, CancellationToken ct) =>
        CallAsync((c, t) => c.AnalyzeAsync(req, t), attempt, options.Analyze, ct);

    public Task<AiOutcome<CritiqueResponse>> CritiqueAsync(CritiqueRequest req, int attempt, CancellationToken ct) =>
        CallAsync((c, t) => c.CritiqueAsync(req, t), attempt, options.Analyze, ct);

    /// <param name="attempt">방금 시작한 시도 번호(1부터). 두 번째 출력 오류부터 사람 검토로 넘긴다.</param>
    public async Task<AiOutcome<T>> CallAsync<T>(Func<IAiClient, CancellationToken, Task<T>> call, int attempt,
        TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            return new AiOutcome<T>(await call(client, cts.Token), null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail<T>(new StepResult.Retry("AI_TIMEOUT", $"AI 서비스 응답 시간 초과({timeout.TotalSeconds:0}초)", null));
        }
        catch (AiApiException e)
        {
            return Fail<T>(Map(e, attempt));
        }
        catch (HttpRequestException e)
        {
            return Fail<T>(new StepResult.Retry("AI_UNAVAILABLE", $"AI 서비스에 연결하지 못했다: {e.Message}", null));
        }
    }

    private static AiOutcome<T> Fail<T>(StepResult r) => new(default, r);

    private static StepResult Map(AiApiException e, int attempt)
    {
        var (code, retryAfter) = ParseBody(e.Response);
        switch (e.StatusCode)
        {
            case 429:
                return new StepResult.Retry("LLM_RATE_LIMITED", "LLM 호출 한도 초과",
                    retryAfter is { } s ? TimeSpan.FromSeconds(s) : null);
            case 422 when code == "LLM_OUTPUT_INVALID":
                return attempt <= 1
                    ? new StepResult.Retry("LLM_OUTPUT_INVALID", "LLM 출력이 계약과 달라 다시 시도한다", null)
                    : new StepResult.NeedsReview("LLM_OUTPUT_INVALID", null);
            case 422:
                // 요청 검증 오류: 다시 보내도 같다. 계약 불일치이므로 실패로 둔다.
                return new StepResult.Failed("AI_REQUEST_INVALID", "AI 서비스가 요청을 거절했다(422)");
            case 502 or 503 or 504 when code is null:
                // 본문에 오류 코드가 없으면 앱이 아니라 Render 프록시의 응답이다. AI 서비스가 꺼져 있거나 깨는 중이다(ai-wake-retry WR-01).
                return new StepResult.Retry("AI_WAKING", $"AI 서비스가 아직 깨어나지 않았다({e.StatusCode})", null);
            case >= 500:
                return new StepResult.Retry(code ?? "AI_UPSTREAM_ERROR", $"AI 서비스 오류({e.StatusCode})", null);
            case 200:
                return new StepResult.Retry("AI_BAD_RESPONSE", "AI 응답을 해석하지 못했다", null);
            default:
                return new StepResult.Failed(code ?? "AI_REQUEST_REJECTED", $"AI 서비스가 요청을 거절했다({e.StatusCode})");
        }
    }

    private static (string? code, double? retryAfter) ParseBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return (null, null);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            double? after = root.TryGetProperty("retry_after_s", out var r) && r.ValueKind == JsonValueKind.Number
                ? r.GetDouble() : null;
            return (code, after);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
