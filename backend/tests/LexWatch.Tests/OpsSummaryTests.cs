using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Ops;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexWatch.Tests;

/// <summary>ops-visibility OV-10~16.</summary>
public class OpsSummaryTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);
    private static readonly DateOnly From = Today.AddDays(-13);
    // KST 2026-11-24 10:00 = UTC 01:00
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 1, 0, 0, TimeSpan.Zero);

    private static JsonElement Run(OpsInput input) =>
        JsonSerializer.SerializeToElement(OpsSummary.Build(input, From, Today), new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static OpsInput Input(List<WorkflowRun>? runs = null, List<StepRun>? steps = null, List<MemoDraft>? drafts = null,
        List<CriticFinding>? findings = null, List<Approval>? approvals = null, List<AiUsageLog>? chat = null,
        List<CollectLog>? collect = null, List<BillCollectLog>? bills = null, int waiting = 0) =>
        new(runs ?? [], steps ?? [], [], drafts ?? [], findings ?? [], approvals ?? [], chat ?? [], collect ?? [], bills ?? [], waiting);

    private static StepRun Step(Guid run, string step, StepStatus status, DateTimeOffset start, int? ms = 1000, int attempt = 1,
        string provider = "cloudflare", Guid? group = null) => new()
    {
        Id = Guid.NewGuid(), RunId = run, GroupReviewId = group, Step = step, Status = status, Attempt = attempt, ProviderKind = provider,
        NotBefore = start, StartedAt = start, FinishedAt = status is StepStatus.Pending or StepStatus.Running or StepStatus.RetryScheduled ? null : start.AddMilliseconds(ms ?? 0),
        DurationMs = ms,
    };

    [Fact]
    public void Empty_data_gives_zeros_and_nulls_with_dense_dates()
    {
        var j = Run(Input());
        Assert.Equal(14, j.GetProperty("days").GetInt32());
        Assert.Equal(14, j.GetProperty("dates").GetArrayLength());
        Assert.Equal(14, j.GetProperty("runs").GetProperty("daily").GetArrayLength());
        Assert.Equal(6, j.GetProperty("runs").GetProperty("byStatus").EnumerateObject().Count());
        Assert.All(j.GetProperty("collect").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.ValueKind));
        Assert.Equal(8, j.GetProperty("steps").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, j.GetProperty("steps")[0].GetProperty("failureRate").ValueKind);
        Assert.Equal(JsonValueKind.Null, j.GetProperty("stepTotals").GetProperty("failureRate").ValueKind);
        Assert.Equal(JsonValueKind.Null, j.GetProperty("critic").GetProperty("flaggedRate").ValueKind);
        Assert.Equal(JsonValueKind.Null, j.GetProperty("decisions").GetProperty("medianHoursToFirstDecision").ValueKind);
        Assert.Equal(JsonValueKind.Null, j.GetProperty("bills").GetProperty("lastSuccessAt").ValueKind);
    }

    [Fact]
    public void Window_starts_at_kst_midnight_and_daily_sums_match_total()
    {
        var edge = OpsSummary.KstMidnight(From);
        var runs = new List<WorkflowRun>
        {
            new() { Id = Guid.NewGuid(), CreatedAt = edge.AddMilliseconds(-1), Status = RunStatus.Completed },   // 기간 밖
            new() { Id = Guid.NewGuid(), CreatedAt = edge, Status = RunStatus.ClosedIrrelevant },
            new() { Id = Guid.NewGuid(), CreatedAt = Now, Status = RunStatus.Queued },
        };
        var j = Run(Input(runs: runs, collect: [new() { Day = Today, ItemCount = 0 }]));
        var r = j.GetProperty("runs");
        Assert.Equal(2, r.GetProperty("total").GetInt32());
        Assert.Equal(2, r.GetProperty("daily").EnumerateArray().Sum(x => x.GetInt32()));
        Assert.Equal(1, r.GetProperty("daily")[0].GetInt32());
        Assert.Equal(1, r.GetProperty("byStatus").GetProperty("Queued").GetInt32());
        // 0건인 날과 기록 없는 날을 구분한다
        Assert.Equal(0, j.GetProperty("collect")[13].GetInt32());
        Assert.Equal(JsonValueKind.Null, j.GetProperty("collect")[12].ValueKind);
    }

    [Fact]
    public void Failure_rate_is_by_final_outcome_and_retries_and_extra_records_are_counted()
    {
        var run = Guid.NewGuid();
        var steps = new List<StepRun>
        {
            // 자동 재시도 4번 끝에 실패한 행, 관리자 다시 실행으로 성공한 새 행
            Step(run, "classify", StepStatus.Failed, Now.AddHours(-3), 53, attempt: 5, provider: "none"),
            Step(run, "classify", StepStatus.Succeeded, Now.AddHours(-2), 6600),
            // 다른 실행에서 최종 실패
            Step(Guid.NewGuid(), "classify", StepStatus.Failed, Now.AddHours(-1), 40, provider: "none"),
            Step(run, "analyze", StepStatus.Running, Now, null),   // 끝나지 않았다
        };
        var j = Run(Input(steps: steps));
        var classify = j.GetProperty("steps").EnumerateArray().Single(x => x.GetProperty("step").GetString() == "classify");
        Assert.Equal(2, classify.GetProperty("outcomes").GetInt32());
        Assert.Equal(1, classify.GetProperty("failed").GetInt32());
        Assert.Equal(0.5, classify.GetProperty("failureRate").GetDouble());
        Assert.Equal(4, classify.GetProperty("autoRetries").GetInt32());
        Assert.Equal(1, classify.GetProperty("extraRecords").GetInt32());
        Assert.Equal(6600, classify.GetProperty("p50Ms").GetInt32());
        Assert.Equal(JsonValueKind.Null, classify.GetProperty("p90Ms").ValueKind);   // 표본 부족
        var analyze = j.GetProperty("steps").EnumerateArray().Single(x => x.GetProperty("step").GetString() == "analyze");
        Assert.Equal(0, analyze.GetProperty("outcomes").GetInt32());
    }

    [Fact]
    public void P90_needs_five_samples()
    {
        var steps = Enumerable.Range(1, 10).Select(i => Step(Guid.NewGuid(), "analyze", StepStatus.Succeeded, Now.AddMinutes(-i), i * 1000)).ToList();
        var analyze = Run(Input(steps: steps)).GetProperty("steps").EnumerateArray().Single(x => x.GetProperty("step").GetString() == "analyze");
        Assert.Equal(5000, analyze.GetProperty("p50Ms").GetInt32());
        Assert.Equal(9000, analyze.GetProperty("p90Ms").GetInt32());
        Assert.Equal(10, analyze.GetProperty("samples").GetInt32());
    }

    [Fact]
    public void Ai_counts_final_provider_and_separates_rule_fallback_and_failures()
    {
        var steps = new List<StepRun>
        {
            Step(Guid.NewGuid(), "classify", StepStatus.Succeeded, Now.AddHours(-1), provider: "cloudflare"),
            Step(Guid.NewGuid(), "analyze", StepStatus.Succeeded, Now.AddHours(-1), provider: "nvidia"),
            Step(Guid.NewGuid(), "index_articles", StepStatus.Succeeded, Now.AddHours(-1), provider: "fallback"),
            Step(Guid.NewGuid(), "critic_judge", StepStatus.Failed, Now.AddHours(-1), provider: "none"),
            Step(Guid.NewGuid(), "fetch_articles", StepStatus.Succeeded, Now.AddHours(-1), provider: "none"),
        };
        var chat = new List<AiUsageLog>
        {
            new() { Id = Guid.NewGuid(), At = Now, Outcome = "answered", ProviderKind = "cloudflare" },
            new() { Id = Guid.NewGuid(), At = Now, Outcome = "failed", ProviderKind = null },
        };
        var ai = Run(Input(steps: steps, chat: chat)).GetProperty("ai");
        Assert.Equal(1, ai.GetProperty("byProvider").GetProperty("nvidia").GetInt32());
        Assert.Equal(1, ai.GetProperty("byProvider").GetProperty("cloudflare").GetInt32());
        Assert.Equal(1, ai.GetProperty("ruleFallback").GetInt32());
        Assert.Equal(1, ai.GetProperty("failed").GetInt32());
        Assert.Equal(2, ai.GetProperty("chat").GetProperty("total").GetInt32());
        // 일별 합 = 모델 공급자 단계 + 모델이 답한 채팅
        Assert.Equal(3, ai.GetProperty("total").GetInt32());
        Assert.Equal(3, ai.GetProperty("daily").EnumerateArray().Sum(x => x.GetInt32()));
    }

    [Fact]
    public void Critic_rate_counts_only_judged_drafts_and_decision_time_uses_last_ready_step()
    {
        var run = Guid.NewGuid();
        Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid();
        var t = Now.AddHours(-10);
        var steps = new List<StepRun>
        {
            Step(run, "analyze", StepStatus.Succeeded, t, group: g1),
            Step(run, "critic_judge", StepStatus.Succeeded, t.AddMinutes(1), group: g1),
            Step(run, "analyze", StepStatus.Succeeded, t, group: g2),
            Step(run, "critic_judge", StepStatus.Succeeded, t.AddMinutes(1), group: g2),
            Step(run, "analyze", StepStatus.Succeeded, t, group: g3),   // 판정이 아직이다
            Step(run, "critic_judge", StepStatus.Pending, t.AddMinutes(1), null, attempt: 0, group: g3),
        };
        var d1 = new MemoDraft { Id = Guid.NewGuid(), GroupReviewId = g1, Version = 1, CreatedBy = "agent" };
        var d2 = new MemoDraft { Id = Guid.NewGuid(), GroupReviewId = g2, Version = 1, CreatedBy = "agent" };
        var d3 = new MemoDraft { Id = Guid.NewGuid(), GroupReviewId = g3, Version = 1, CreatedBy = "agent" };
        var findings = new List<CriticFinding>
        {
            new() { Id = Guid.NewGuid(), MemoDraftId = d1.Id, Tier = 3 },
            new() { Id = Guid.NewGuid(), MemoDraftId = d1.Id, Tier = 1 },
        };
        var judgeEnd = t.AddMinutes(1).AddSeconds(1);
        var approvals = new List<Approval>
        {
            new() { GroupReviewId = g1, Decision = "approve", ActorRole = "lawyer", DecidedAt = judgeEnd.AddHours(2) },
            new() { GroupReviewId = g1, Decision = "confirm", ActorRole = "partner", DecidedAt = judgeEnd.AddHours(3) },
            new() { GroupReviewId = g2, Decision = "reject", ActorRole = "lawyer", DecidedAt = judgeEnd.AddHours(4) },
            new() { GroupReviewId = g3, Decision = "confirm", ActorRole = "partner", DecidedAt = Now },   // 파트너만
        };
        var j = Run(Input(steps: steps, drafts: [d1, d2, d3], findings: findings, approvals: approvals, waiting: 2));
        var c = j.GetProperty("critic");
        Assert.Equal(2, c.GetProperty("drafts").GetInt32());
        Assert.Equal(1, c.GetProperty("flagged").GetInt32());
        Assert.Equal(0.5, c.GetProperty("flaggedRate").GetDouble());
        Assert.Equal(1, c.GetProperty("byTier").GetProperty("3").GetInt32());
        var d = j.GetProperty("decisions");
        Assert.Equal(4, d.GetProperty("total").GetInt32());
        Assert.Equal(2, d.GetProperty("firstDecisionSamples").GetInt32());
        Assert.Equal(3.0, d.GetProperty("medianHoursToFirstDecision").GetDouble());   // (2 + 4) / 2
        Assert.Equal(2, d.GetProperty("waiting").GetInt32());
    }
}

[Collection("pg")]
public class OpsEndpointTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Admin_only_and_days_validation()
    {
        Environment.SetEnvironmentVariable("DATABASE_URL", pg.ConnectionString);
        await using var f = new WebApplicationFactory<Program>();
        HttpClient As(string role) { var c = f.CreateClient(); c.DefaultRequestHeaders.Add("X-Demo-Role", role); return c; }
        Assert.Equal(HttpStatusCode.Forbidden, (await As("lawyer").GetAsync("/api/ops/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await As("admin").GetAsync("/api/ops/summary?days=abc")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await As("admin").GetAsync("/api/ops/summary?days=9")).StatusCode);
        var j = await As("admin").GetFromJsonAsync<JsonElement>("/api/ops/summary");
        Assert.Equal(14, j.GetProperty("days").GetInt32());
        var seven = await As("admin").GetFromJsonAsync<JsonElement>("/api/ops/summary?days=7");
        Assert.Equal(7, seven.GetProperty("dates").GetArrayLength());
    }
}
