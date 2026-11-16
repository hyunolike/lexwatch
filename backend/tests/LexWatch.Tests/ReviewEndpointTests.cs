using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LexWatch.Api.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LexWatch.Tests;

[Collection("pg")]
public class ReviewEndpointTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", pg.ConnectionString);
            b.ConfigureTestServices(s =>
            {
                // 백그라운드 워커가 notify 단계를 먼저 집어가면 단정이 흔들린다.
                foreach (var d in s.Where(d => d.ImplementationType == typeof(LexWatch.Api.Workflow.WorkflowWorker)).ToList())
                    s.Remove(d);
            });
        });

    private static HttpClient As(WebApplicationFactory<Program> f, string? role)
    {
        var c = f.CreateClient();
        if (role is not null) c.DefaultRequestHeaders.Add("X-Demo-Role", role);
        return c;
    }

    private async Task<GroupReview> SeedAsync()
    {
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id, "labor", ReviewStatus.WaitingApproval);
        await using var db = pg.CreateDb();
        var draft = new MemoDraft
        {
            Id = Guid.NewGuid(), GroupReviewId = g.Id, Version = 1, CreatedBy = "agent", OpenQuestionsJson = "[\"시행일 확인\"]",
            SentencesJson = """[{"sentence":"해고가 제한된다.","citations":["law:L1:제23조"]},{"sentence":"신구조문이 있다.","citations":["oldnew:L1:제23조"]}]""",
        };
        db.MemoDrafts.Add(draft);
        db.CriticFindings.Add(new CriticFinding { Id = Guid.NewGuid(), MemoDraftId = draft.Id, Tier = 3, SentenceIndex = 0, RuleId = "OC-01", Message = "단정" });
        await db.SaveChangesAsync();
        return await db.GroupReviews.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    }

    [Fact]
    public async Task Missing_role_header_is_403()
    {
        await using var f = Factory();
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, null).GetAsync("/api/reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, "intern").GetAsync("/api/inbox")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await As(f, "admin").GetAsync("/api/reviews")).StatusCode);
    }

    [Fact]
    public async Task List_and_detail_return_draft_findings_and_oldnew()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var c = As(f, "lawyer");
        var list = await c.GetFromJsonAsync<JsonElement>("/api/reviews");
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(1, list[0].GetProperty("findingCount").GetInt32());
        Assert.Equal("노동", list[0].GetProperty("groupName").GetString());

        var d = await c.GetFromJsonAsync<JsonElement>($"/api/reviews/{g.Id}");
        Assert.Equal(g.Version, d.GetProperty("version").GetUInt32());
        Assert.Equal("제23조", d.GetProperty("oldNew")[0].GetProperty("articleNo").GetString());
        Assert.Equal("OC-01", d.GetProperty("draft").GetProperty("sentences")[0].GetProperty("findings")[0].GetProperty("ruleId").GetString());
        Assert.Equal(2, d.GetProperty("evidence").GetArrayLength());
    }

    [Fact]
    public async Task Approve_enqueues_notify_and_records_actor()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var resp = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, comment = "ok" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        await using var db = pg.CreateDb();
        Assert.Equal(ReviewStatus.Approved, (await db.GroupReviews.SingleAsync()).Status);
        var a = await db.Approvals.SingleAsync();
        Assert.Equal(("approve", "lawyer", "ok"), (a.Decision, a.ActorRole, a.Comment));
        var step = await db.StepRuns.SingleAsync(s => s.Step == "notify");
        Assert.Equal(g.Id, step.GroupReviewId);

        // 이미 결정된 건은 409
        var again = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/reject", new { version = g.Version, comment = "x" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("REVIEW_ALREADY_DECIDED", (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_cannot_approve()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var resp = await As(f, "admin").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Reject_requires_comment()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var c = As(f, "lawyer");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync($"/api/reviews/{g.Id}/reject", new { version = g.Version, comment = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await c.PostAsJsonAsync($"/api/reviews/{g.Id}/reject", new { version = g.Version, comment = "근거 부족" })).StatusCode);
        await using var db = pg.CreateDb();
        Assert.Equal(ReviewStatus.Rejected, (await db.GroupReviews.SingleAsync()).Status);
    }

    [Fact]
    public async Task Concurrent_approve_and_reject_only_one_wins()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var c = As(f, "lawyer");
        var t1 = c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version });
        var t2 = c.PostAsJsonAsync($"/api/reviews/{g.Id}/reject", new { version = g.Version, comment = "반려" });
        var codes = (await Task.WhenAll(t1, t2)).Select(r => r.StatusCode).OrderBy(x => x).ToArray();
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], codes);
        await using var db = pg.CreateDb();
        Assert.Equal(1, await db.Approvals.CountAsync());
    }

    [Fact]
    public async Task Edited_sentences_create_new_draft_version_with_diff()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var c = As(f, "lawyer");
        var bad = await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new
        {
            version = g.Version,
            editedSentences = new[] { new { sentence = "수정", citations = new[] { "law:NOPE" } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("INVALID_CITATION", (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var ok = await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new
        {
            version = g.Version,
            editedSentences = new[]
            {
                new { sentence = "해고가 엄격히 제한된다.", citations = new[] { "law:L1:제23조" } },
                new { sentence = "신구조문이 있다.", citations = new[] { "oldnew:L1:제23조" } },
            },
        });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        await using var db = pg.CreateDb();
        var drafts = await db.MemoDrafts.OrderBy(d => d.Version).ToListAsync();
        Assert.Equal([1, 2], drafts.Select(d => d.Version).ToArray());
        Assert.Equal("lawyer", drafts[1].CreatedBy);
        var diff = JsonDocument.Parse((await db.Approvals.SingleAsync()).DiffJson!).RootElement;
        Assert.Equal(1, diff.GetArrayLength());
        Assert.Equal(0, diff[0].GetProperty("index").GetInt32());
        Assert.Equal("해고가 제한된다.", diff[0].GetProperty("before").GetProperty("sentence").GetString());
    }
}
