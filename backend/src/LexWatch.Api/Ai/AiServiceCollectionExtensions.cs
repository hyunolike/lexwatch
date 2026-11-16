namespace LexWatch.Api.Ai;

public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddAiGateway(this IServiceCollection services)
    {
        var baseUrl = Environment.GetEnvironmentVariable("AI_BASE_URL") ?? "http://localhost:8000";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        services.AddSingleton(AiGatewayOptions.Default);
        // 단계별 타임아웃은 게이트웨이가 건다. HttpClient 자체는 가장 긴 호출(analyze)에 맞춘다.
        services.AddHttpClient<IAiClient, AiClient>(c =>
        {
            c.BaseAddress = new Uri(baseUrl);
            c.Timeout = TimeSpan.FromSeconds(130);
        });
        services.AddTransient<AiGateway>();
        return services;
    }
}
