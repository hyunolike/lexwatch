using System.Net;
using System.Net.Http.Json;
using LexWatch.Api.LawApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LexWatch.Tests;

[Collection("pg")]
public class JobEndpointTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", pg.ConnectionString);
            b.UseSetting("JOB_TOKEN", "secret-token");
            b.ConfigureTestServices(s =>
            {
                s.AddScoped(sp => new LexWatch.Api.Workflow.Steps.CollectService(
                    sp.GetRequiredService<LexWatch.Api.Data.LexWatchDb>(), LawFixtures.Client(LawFixtures.Recorded),
                    TimeProvider.System, (_, _) => Task.CompletedTask));
            });
        });

    [Fact]
    public async Task Collect_without_token_returns_401()
    {
        await using var f = Factory();
        var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsync("/jobs/collect", null)).StatusCode);
        var req = new HttpRequestMessage(HttpMethod.Post, "/jobs/collect");
        req.Headers.Add("X-Job-Token", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Collect_with_token_returns_created_and_skipped()
    {
        await using var f = Factory();
        var c = f.CreateClient();
        for (var i = 0; i < 2; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/jobs/collect");
            req.Headers.Add("X-Job-Token", "secret-token");
            var resp = await c.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, int>>();
            Assert.Equal(i == 0 ? 3 : 0, body!["created"]);
            Assert.Equal(i == 0 ? 0 : 3, body["skipped"]);
        }
    }
}
