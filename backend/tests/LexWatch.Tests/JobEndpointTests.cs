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
            var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(i == 0 ? 3 : 0, body.GetProperty("created").GetInt32());
            Assert.Equal(i == 0 ? 0 : 3, body.GetProperty("skipped").GetInt32());
            var runIds = body.GetProperty("runIds").EnumerateArray().Select(x => x.GetGuid()).ToList();
            Assert.Equal(i == 0 ? 3 : 0, runIds.Count);
            await using var db = pg.CreateDb();
            foreach (var id in runIds)
                Assert.True(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.WorkflowRuns, r => r.Id == id));
        }
    }

    private WebApplicationFactory<Program> WakeFactory(AiStub ai) =>
        Factory().WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => ai.Client())));

    private static HttpRequestMessage Wake(string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/jobs/wake-ai");
        if (token is not null) req.Headers.Add("X-Job-Token", token);
        return req;
    }

    private static void Health(AiStub ai, int status) =>
        ai.Server.Given(WireMock.RequestBuilders.Request.Create().WithPath("/health").UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody("""{"status":"ok"}"""));

    [Fact]
    public async Task WakeAi_requires_token_and_reports_ai_health()
    {
        using var ai = new AiStub();
        Health(ai, 200);
        await using var f = WakeFactory(ai);
        var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(Wake(null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(Wake("wrong"))).StatusCode);
        var ok = await c.SendAsync(Wake("secret-token"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("ok", (await ok.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("ai").GetString());
    }

    [Fact]
    public async Task WakeAi_returns_503_while_ai_is_asleep()
    {
        using var ai = new AiStub();
        Health(ai, 502);
        await using var f = WakeFactory(ai);
        var resp = await f.CreateClient().SendAsync(Wake("secret-token"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        Assert.Equal("AI_ASLEEP", (await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString());

        using var down = new AiStub();
        var url = down.Server.Url!;
        down.Server.Stop(); // 연결 자체가 안 되는 경우
        await using var f2 = Factory().WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => down.Client(url))));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f2.CreateClient().SendAsync(Wake("secret-token"))).StatusCode);
    }

    [Fact]
    public async Task ResetDemo_requires_token_and_seeds_replays()
    {
        await using var f = Factory();
        var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsync("/jobs/reset-demo", null)).StatusCode);
        var req = new HttpRequestMessage(HttpMethod.Post, "/jobs/reset-demo");
        req.Headers.Add("X-Job-Token", "secret-token");
        Assert.Equal(HttpStatusCode.OK, (await c.SendAsync(req)).StatusCode);
        await using var db = pg.CreateDb();
        Assert.Equal(5, await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.WorkflowRuns, r => r.IsReplay));
    }

    [Fact]
    public async Task SeedIfEmpty_seeds_once()
    {
        await using var db = pg.CreateDb();
        var seeder = new LexWatch.Api.Demo.DemoSeeder(db, TimeProvider.System);
        Assert.True(await seeder.SeedIfEmptyAsync());
        await using var db2 = pg.CreateDb();
        Assert.False(await new LexWatch.Api.Demo.DemoSeeder(db2, TimeProvider.System).SeedIfEmptyAsync());
        Assert.Equal(5, await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db2.WorkflowRuns));
    }
}
