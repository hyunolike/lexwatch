using LexWatch.Api.Ai;

namespace LexWatch.Tests;

public class AiMappingTests
{
    [Theory]
    [InlineData(ProviderInfoKind.Nvidia, "nvidia")]
    [InlineData(ProviderInfoKind.Cloudflare, "cloudflare")]
    [InlineData(ProviderInfoKind.Groq, "groq")]
    [InlineData(ProviderInfoKind.Fallback, "fallback")]
    public void Provider_kind_maps_to_stored_value(ProviderInfoKind kind, string stored) =>
        Assert.Equal(stored, AiMapping.ProviderKind(new ProviderInfo { Kind = kind, Name = "x" }));
}
