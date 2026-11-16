using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexWatch.Tests;

[Collection("pg")]
public class CorsTests(PostgresFixture pg)
{
    private async Task<HttpResponseMessage> Preflight(string origin)
    {
        Environment.SetEnvironmentVariable("DATABASE_URL", pg.ConnectionString);
        await using var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("Web:Origins", "https://app.example.com"));
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/reviews");
        req.Headers.Add("Origin", origin);
        req.Headers.Add("Access-Control-Request-Method", "GET");
        req.Headers.Add("Access-Control-Request-Headers", "x-demo-role");
        return await f.CreateClient().SendAsync(req);
    }

    [Fact]
    public async Task Preflight_from_allowed_origin_gets_cors_headers()
    {
        var resp = await Preflight("https://app.example.com");
        Assert.Equal("https://app.example.com", resp.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("x-demo-role", string.Join(",", resp.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preflight_from_disallowed_origin_gets_no_cors_headers()
    {
        var resp = await Preflight("https://evil.example.com");
        Assert.False(resp.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
