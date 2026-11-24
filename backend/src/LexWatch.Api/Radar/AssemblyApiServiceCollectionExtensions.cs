namespace LexWatch.Api.Radar;

public static class AssemblyApiServiceCollectionExtensions
{
    /// <summary>ASSEMBLY_API_KEY가 없으면 키 없이 부른다(호출마다 5건).</summary>
    public static IServiceCollection AddAssemblyApi(this IServiceCollection services)
    {
        services.AddSingleton(new AssemblyApiOptions(Environment.GetEnvironmentVariable("ASSEMBLY_API_KEY")?.Trim() ?? ""));
        // 호출마다 자체 20초 제한을 두므로 HttpClient 기본 제한은 조금 길게 둔다. 로그에 주소(키)가 남지 않게 한다.
        services.AddHttpClient<AssemblyApiClient>(c => c.Timeout = TimeSpan.FromSeconds(30)).RemoveAllLoggers();
        return services;
    }
}
