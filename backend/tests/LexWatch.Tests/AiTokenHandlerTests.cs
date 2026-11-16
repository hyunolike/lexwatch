using LexWatch.Api.Ai;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace LexWatch.Tests;

public class AiTokenHandlerTests
{
    private static async Task<IDictionary<string, WireMock.Types.WireMockList<string>>?> SendAsync(string? token)
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"status\":\"ok\"}"));
        using var http = new HttpClient(new AiTokenHandler(() => token) { InnerHandler = new HttpClientHandler() })
        {
            BaseAddress = new Uri(server.Url! + "/"),
        };
        (await http.GetAsync("health")).EnsureSuccessStatusCode();
        return server.LogEntries.Single().RequestMessage!.Headers;
    }

    [Fact]
    public async Task 토큰이_있으면_X_Ai_Token_헤더를_붙인다()
    {
        var headers = await SendAsync(" t0ken ");
        Assert.Equal("t0ken", headers![AiTokenHandler.HeaderName].Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task 토큰이_비어_있으면_헤더를_붙이지_않는다(string? token)
    {
        var headers = await SendAsync(token);
        Assert.False(headers!.ContainsKey(AiTokenHandler.HeaderName));
    }
}
