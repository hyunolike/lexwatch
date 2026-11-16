using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LexWatch.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexWatch.Tests;

[Collection("pg")]
public class RunEndpointTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", pg.ConnectionString);
            b.ConfigureTestServices(s =>
            {
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

    [Fact]
    public async Task Timeline_merges_steps_tool_calls_findings_and_approvals_in_time_order()
    {
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id, "labor", ReviewStatus.Approved);
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        await using (var db = pg.CreateDb())
        {
            var collect = new StepRun { Id = Guid.NewGuid(), RunId = run.Id, Step = "collect", Attempt = 1, Status = StepStatus.Succeeded,
                NotBefore = t0, StartedAt = t0, FinishedAt = t0.AddSeconds(2), DurationMs = 2000, ProviderKind = "none", OutputJson = "{\"ok\":true}" };
            var analyze = new StepRun { Id = Guid.NewGuid(), RunId = run.Id, GroupReviewId = g.Id, Step = "analyze", Attempt = 1,
                Status = StepStatus.Succeeded, NotBefore = t0.AddSeconds(3), StartedAt = t0.AddSeconds(3), FinishedAt = t0.AddSeconds(30),
                DurationMs = 27000, ProviderKind = "nvidia", ProviderName = "m", OutputJson = "\"" + new string((char)120, 500) + "\"" };
            var critique = new StepRun { Id = Guid.NewGuid(), RunId = run.Id, GroupReviewId = g.Id, Step = "critique", Attempt = 1,
                Status = StepStatus.Succeeded, NotBefore = t0.AddSeconds(31), StartedAt = t0.AddSeconds(31), FinishedAt = t0.AddSeconds(35),
                ProviderKind = "nvidia" };
            var notify = new StepRun { Id = Guid.NewGuid(), RunId = run.Id, GroupReviewId = g.Id, Step = "notify", Attempt = 0,
                Status = StepStatus.Pending, NotBefore = t0.AddMinutes(5), ProviderKind = "none" };
            db.StepRuns.AddRange(collect, analyze, critique, notify);
            db.AgentToolCalls.AddRange(
                new AgentToolCall { Id = Guid.NewGuid(), StepRunId = analyze.Id, Seq = 2, Tool = "get_law_text", ArgsJson = "{}", ResultSummary = "b", EvidenceIds = ["law:L1:제23조"] },
                new AgentToolCall { Id = Guid.NewGuid(), StepRunId = analyze.Id, Seq = 1, Tool = "get_old_new", ArgsJson = "{}", ResultSummary = "a", EvidenceIds = ["oldnew:L1:제23조"] });
            var draft = new MemoDraft { Id = Guid.NewGuid(), GroupReviewId = g.Id, Version = 1, CreatedBy = "agent", SentencesJson = "[]", OpenQuestionsJson = "[]" };
            db.MemoDrafts.Add(draft);
            db.CriticFindings.Add(new CriticFinding { Id = Guid.NewGuid(), MemoDraftId = draft.Id, Tier = 3, SentenceIndex = 0, RuleId = "OC-01", Message = "단정" });
            db.Approvals.Add(new Approval { Id = Guid.NewGuid(), GroupReviewId = g.Id, Decision = "approve", ActorRole = "lawyer", Comment = "ok", DecidedAt = t0.AddMinutes(2) });
            db.Notifications.Add(new Notification { Id = Guid.NewGuid(), GroupReviewId = g.Id, Channel = "demo_inbox", Status = "sent", Body = "b", SentAt = t0.AddMinutes(3) });
            await db.SaveChangesAsync();
        }

        await using var f = Factory();
        var c = As(f, "lawyer");
        var list = await c.GetFromJsonAsync<JsonElement>("/api/runs");
        Assert.Equal(run.Id, list[0].GetProperty("id").GetGuid());
        Assert.Equal("근로기준법", list[0].GetProperty("lawName").GetString());
        Assert.Equal("labor", list[0].GetProperty("groupStatuses")[0].GetProperty("groupCode").GetString());

        var detail = await c.GetFromJsonAsync<JsonElement>($"/api/runs/{run.Id}");
        var tl = detail.GetProperty("timeline").EnumerateArray().ToList();
        var kinds = tl.Select(e => e.GetProperty("kind").GetString() + ":" +
            (e.TryGetProperty("tool", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : e.TryGetProperty("step", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : "")).ToList();
        Assert.Equal(new[]
        {
            "step:collect", "step:analyze", "tool_call:get_old_new", "tool_call:get_law_text", "step:critique",
            "finding:", "approval:", "notification:", "step:notify",
        }, kinds);
        var at = tl.Select(e => e.GetProperty("at").GetDateTimeOffset()).ToList();
        Assert.Equal(at.OrderBy(x => x).ToList(), at);
        Assert.True(tl[1].GetProperty("summary").GetString()!.Length <= 300);
        Assert.Equal("근로기준법", detail.GetProperty("lawChange").GetProperty("lawName").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/runs/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Retry_requires_admin_and_requeues_failed_step()
    {
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id, "labor", ReviewStatus.NeedsReview);
        Guid failedId;
        await using (var db = pg.CreateDb())
        {
            var st = new StepRun { Id = Guid.NewGuid(), RunId = run.Id, GroupReviewId = g.Id, Step = "analyze", Attempt = 3,
                Status = StepStatus.Failed, NotBefore = DateTimeOffset.UtcNow, StartedAt = DateTimeOffset.UtcNow, ErrorCode = "X",
                ProviderKind = "nvidia", InputJson = "{\"a\": 1}" };
            failedId = st.Id;
            db.StepRuns.Add(st);
            (await db.WorkflowRuns.SingleAsync(r => r.Id == run.Id)).Status = RunStatus.Failed;
            await db.SaveChangesAsync();
        }
        await using var f = Factory();
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, "lawyer").PostAsync($"/api/runs/{run.Id}/retry", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(f, "admin").PostAsync($"/api/runs/{Guid.NewGuid()}/retry", null)).StatusCode);

        var ok = await As(f, "admin").PostAsync($"/api/runs/{run.Id}/retry", null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        await using (var db = pg.CreateDb())
        {
            var rows = await db.StepRuns.Where(s => s.RunId == run.Id).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Equal(StepStatus.Failed, rows.Single(r => r.Id == failedId).Status);
            var fresh = rows.Single(r => r.Id != failedId);
            Assert.Equal((StepStatus.Pending, "analyze", g.Id, "{\"a\": 1}"), (fresh.Status, fresh.Step, fresh.GroupReviewId, fresh.InputJson));
            Assert.Equal(RunStatus.Running, (await db.WorkflowRuns.SingleAsync(r => r.Id == run.Id)).Status);
            Assert.Equal(ReviewStatus.Analyzing, (await db.GroupReviews.SingleAsync(x => x.Id == g.Id)).Status);
        }
        var again = await As(f, "admin").PostAsync($"/api/runs/{run.Id}/retry", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("NOTHING_TO_RETRY", (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
