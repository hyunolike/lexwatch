namespace LexWatch.Api.LawApi;

public static class LawApiServiceCollectionExtensions
{
    public static IServiceCollection AddLawApi(this IServiceCollection services)
    {
        services.AddSingleton(new LawApiOptions(Environment.GetEnvironmentVariable("LAW_OC") ?? ""));
        // OC가 쿼리스트링에 실리므로 기본 HttpClient 요청 로그(전체 URL)를 제거한다.
        services.AddHttpClient<LawApiClient>().RemoveAllLoggers();
        return services;
    }
}
