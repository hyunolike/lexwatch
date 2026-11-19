using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexWatch.Tests;

/// <summary>배포 자동화(ci.yml deploy-api)가 /health의 commit으로 새 코드가 떴는지 확인한다.</summary>
[Collection("pg")]
public class HealthTests(PostgresFixture pg)
{
    private async Task<JsonElement> Health(string? commit)
    {
        Environment.SetEnvironmentVariable("DATABASE_URL", pg.ConnectionString);
        await using var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            if (commit is not null) b.UseSetting("RENDER_GIT_COMMIT", commit);
        });
        return await f.CreateClient().GetFromJsonAsync<JsonElement>("/health");
    }

    [Fact]
    public async Task Health_reports_deployed_commit()
    {
        var h = await Health("0e76b57b870186d9baff5540e2550b63ba661851");
        Assert.Equal("ok", h.GetProperty("status").GetString());
        Assert.Equal("0e76b57b870186d9baff5540e2550b63ba661851", h.GetProperty("commit").GetString());
    }

    [Fact]
    public async Task Health_without_render_commit_returns_null_commit()
    {
        var h = await Health(null);
        Assert.Equal("ok", h.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, h.GetProperty("commit").ValueKind);
    }
}
