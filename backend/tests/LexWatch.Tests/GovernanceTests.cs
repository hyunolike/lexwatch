using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LexWatch.Api.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexWatch.Tests;

/// <summary>ai-governance: 민감정보 가리기와 기록(GV-03~06), 파트너 최종 승인(GV-07~14).</summary>
[Collection("pg")]
public class GovernanceTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> Factory(AiStub? ai = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", pg.ConnectionString);
            if (ai is not null) Environment.SetEnvironmentVariable("AI_BASE_URL", ai.Server.Url);
            b.UseSetting("JOB_TOKEN", "secret-token");
            b.UseSetting("Governance:ClientNames", "한빛전자,가람바이오");
            b.ConfigureTestServices(s =>
            {
                foreach (var d in s.Where(d => d.ImplementationType == typeof(LexWatch.Api.Workflow.WorkflowWorker)).ToList())
                    s.Remove(d);
            });
        });

    private static HttpClient As(WebApplicationFactory<Program> f, string role)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-Demo-Role", role);
        return c;
    }

    /// <summary>3단 적발 1건이 있는 승인 대기 그룹.</summary>
    private async Task<GroupReview> SeedAsync(bool withFinding = true)
    {
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id, "labor", ReviewStatus.WaitingApproval);
        await using var db = pg.CreateDb();
        var draft = new MemoDraft
        {
            Id = Guid.NewGuid(), GroupReviewId = g.Id, Version = 1, CreatedBy = "agent", OpenQuestionsJson = "[]",
            SentencesJson = """[{"sentence":"해고가 제한된다.","citations":["law:L1:제23조"]}]""",
        };
        db.MemoDrafts.Add(draft);
        if (withFinding)
            db.CriticFindings.Add(new CriticFinding { Id = Guid.NewGuid(), MemoDraftId = draft.Id, Tier = 3, SentenceIndex = 0, RuleId = "OC-01", Message = "단정" });
        await db.SaveChangesAsync();
        return await db.GroupReviews.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    }

    private async Task<uint> VersionAsync(Guid id)
    {
        await using var db = pg.CreateDb();
        return (await db.GroupReviews.AsNoTracking().SingleAsync(x => x.Id == id)).Version;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    private static readonly object ChatOk = new
    {
        answer = "해고 제한이 강화돼요.", citations = new[] { "law:L1:제23조" }, refused = false, provider = AiStub.Provider(),
    };

    // ── 민감정보 가리기와 기록 ──────────────────────────────

    [Fact]
    public async Task Chat_masks_question_and_history_before_ai_and_logs_metadata_only()
    {
        var g = await SeedAsync();
        using var ai = new AiStub();
        ai.Json("/review-chat", 200, ChatOk);
        await using var f = Factory(ai);
        var r = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new
        {
            question = "한빛전자 대표(900101-1234567)에게 제23조가 적용되나요? 연락처 010-1234-5678",
            history = new[]
            {
                new { role = "user", content = "가람바이오 담당자 kim@example.com" },
                new { role = "assistant", content = "근거 안에서 답할게요." },
            },
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("[고객사] 대표([주민등록번호])에게 제23조가 적용되나요? 연락처 [전화번호]", body.GetProperty("maskedQuestion").GetString());
        var masked = body.GetProperty("masked").EnumerateArray()
            .Select(m => (m.GetProperty("kind").GetString(), m.GetProperty("label").GetString(), m.GetProperty("count").GetInt32())).ToList();
        Assert.Equal([("client", "고객사", 1), ("rrn", "주민등록번호", 1), ("phone", "전화번호", 1)], masked);

        // 원문 JSON은 한글을 \u 이스케이프하므로, 문자열 값을 풀어서 본다.
        var req = Assert.Single(ai.Requests("/review-chat")).RootElement;
        var sent = req.GetProperty("question").GetString() + "\n" + string.Join("\n",
            req.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("content").GetString()));
        foreach (var secret in new[] { "한빛전자", "900101", "1234567", "010-1234-5678", "가람바이오", "kim@example.com" })
            Assert.DoesNotContain(secret, sent);
        Assert.Contains("[이메일]", sent);

        await using var db = pg.CreateDb();
        var log = await db.AiUsageLogs.SingleAsync();
        Assert.Equal(("review_chat", "lawyer", "answered", "nvidia", (Guid?)g.Id), (log.Purpose, log.ActorRole, log.Outcome, log.ProviderKind, log.GroupReviewId));
        // 질문분만 센다(기록의 이메일, 고객사는 넣지 않는다).
        Assert.Equal(new Dictionary<string, int> { ["client"] = 1, ["rrn"] = 1, ["phone"] = 1 },
            JsonSerializer.Deserialize<Dictionary<string, int>>(log.MaskedJson));
        Assert.True(log.DurationMs >= 0);
    }

    [Fact]
    public async Task Chat_outcomes_are_logged_and_ai_failure_logs_failed()
    {
        var g = await SeedAsync();
        using var ai = new AiStub();
        ai.Json("/review-chat", 200, new { answer = "x", citations = Array.Empty<string>(), refused = true, provider = AiStub.Provider("fallback", "no-key") },
            partialMatch: new { question = "키 없음 제23조" });
        ai.Json("/review-chat", 200, new { answer = "x", citations = Array.Empty<string>(), refused = true, provider = AiStub.Provider() },
            partialMatch: new { question = "거절 제23조" });
        await using var f = Factory(ai);
        var c = As(f, "lawyer");
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "키 없음 제23조" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "거절 제23조" })).StatusCode);
        Assert.Equal((HttpStatusCode)424, (await c.PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "맞는 응답 없음" })).StatusCode);

        await using var db = pg.CreateDb();
        var logs = await db.AiUsageLogs.OrderBy(l => l.At).Select(l => new { l.Outcome, l.ProviderKind }).ToListAsync();
        Assert.Equal(["unavailable", "refused", "failed"], logs.Select(l => l.Outcome));
        Assert.Null(logs[2].ProviderKind);
    }

    [Fact]
    public async Task Ai_usage_is_admin_only_and_summarizes_last_7_days()
    {
        await using (var db = pg.CreateDb())
        {
            var now = DateTimeOffset.UtcNow;
            db.AiUsageLogs.AddRange(
                new AiUsageLog { Id = Guid.NewGuid(), At = now.AddMinutes(-1), Purpose = "review_chat", ActorRole = "lawyer", Outcome = "answered", ProviderKind = "nvidia", MaskedJson = """{"rrn":1,"client":2}""" },
                new AiUsageLog { Id = Guid.NewGuid(), At = now.AddMinutes(-2), Purpose = "review_chat", ActorRole = "lawyer", Outcome = "failed", MaskedJson = """{"client":1}""" },
                new AiUsageLog { Id = Guid.NewGuid(), At = now.AddDays(-8), Purpose = "review_chat", ActorRole = "lawyer", Outcome = "answered", ProviderKind = "nvidia", MaskedJson = """{"rrn":5}""" });
            await db.SaveChangesAsync();
        }
        await using var f = Factory();
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, "lawyer").GetAsync("/api/governance/ai-usage")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, "partner").GetAsync("/api/governance/ai-usage")).StatusCode);
        var r = await As(f, "admin").GetFromJsonAsync<JsonElement>("/api/governance/ai-usage");
        var s = r.GetProperty("summary");
        Assert.Equal(2, s.GetProperty("total").GetInt32());
        Assert.Equal("""{"answered":1,"refused":0,"unavailable":0,"failed":1}""", s.GetProperty("byOutcome").GetRawText());
        Assert.Equal(1, s.GetProperty("masked").GetProperty("rrn").GetInt32());
        Assert.Equal(3, s.GetProperty("masked").GetProperty("client").GetInt32());
        Assert.Equal("""{"nvidia":1}""", s.GetProperty("byProvider").GetRawText());
        var recent = r.GetProperty("recent").EnumerateArray().ToList();
        Assert.Equal(3, recent.Count);
        Assert.Equal("answered", recent[0].GetProperty("outcome").GetString()); // 최신순
        Assert.DoesNotContain("question", r.GetRawText());
    }

    // ── 파트너 최종 승인 ──────────────────────────────

    [Fact]
    public async Task Acknowledged_findings_wait_for_partner_then_confirm_notifies()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var ok = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 1 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("AwaitingPartner", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        await using (var db = pg.CreateDb())
        {
            Assert.Equal(0, await db.StepRuns.CountAsync(s => s.Step == "notify"));
            var a = await db.Approvals.SingleAsync();
            Assert.Contains("OC-01", a.FindingsJson);
            var run = await db.WorkflowRuns.SingleAsync();
            Assert.Equal(RunStatus.InReview, run.Status);
        }

        // 상세: 파트너가 볼 변호사 승인과 적발 목록, 목록 기본 상태에 포함
        var detail = await As(f, "partner").GetFromJsonAsync<JsonElement>($"/api/reviews/{g.Id}");
        var la = detail.GetProperty("lawyerApproval");
        Assert.Equal(1, la.GetProperty("acknowledgedFindingCount").GetInt32());
        Assert.Equal("OC-01", la.GetProperty("findings")[0].GetProperty("ruleId").GetString());
        var list = await As(f, "partner").GetFromJsonAsync<JsonElement>("/api/reviews");
        Assert.Equal("AwaitingPartner", list[0].GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await As(f, "partner").GetAsync($"/api/runs/{g.RunId}")).StatusCode);

        // 역할 분리
        var v = await VersionAsync(g.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/confirm", new { version = v })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, "admin").PostAsJsonAsync($"/api/reviews/{g.Id}/confirm", new { version = v })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/reject", new { version = v, comment = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await As(f, "partner").PostAsJsonAsync($"/api/reviews/{g.Id}/confirm", new { version = v + 1 })).StatusCode);

        var done = await As(f, "partner").PostAsJsonAsync($"/api/reviews/{g.Id}/confirm", new { version = v, comment = "확인" });
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        await using var chk = pg.CreateDb();
        Assert.Equal(ReviewStatus.Approved, (await chk.GroupReviews.SingleAsync()).Status);
        Assert.Equal(1, await chk.StepRuns.CountAsync(s => s.Step == "notify" && s.GroupReviewId == g.Id));
        var confirm = await chk.Approvals.SingleAsync(a => a.Decision == "confirm");
        Assert.Equal(("partner", "확인"), (confirm.ActorRole, confirm.Comment));
    }

    [Fact]
    public async Task Partner_cannot_decide_lawyer_stage()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, "partner").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, "partner").PostAsJsonAsync($"/api/reviews/{g.Id}/reject", new { version = g.Version, comment = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await As(f, "partner").PostAsJsonAsync($"/api/reviews/{g.Id}/confirm", new { version = g.Version })).StatusCode);
        // 사유 없는 return: 변호사는 403, 파트너는 400
        Assert.Equal(HttpStatusCode.Forbidden, (await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/return", new { version = g.Version })).StatusCode);
        Assert.Equal("COMMENT_REQUIRED", await CodeAsync(await As(f, "partner").PostAsJsonAsync($"/api/reviews/{g.Id}/return", new { version = g.Version })));
    }

    [Fact]
    public async Task Returned_review_goes_back_and_next_approval_needs_partner_even_without_findings()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 1 });
        var ret = await As(f, "partner").PostAsJsonAsync($"/api/reviews/{g.Id}/return", new { version = await VersionAsync(g.Id), comment = "단정 표현을 고쳐 주세요" });
        Assert.Equal(HttpStatusCode.OK, ret.StatusCode);
        await using (var db = pg.CreateDb())
        {
            Assert.Equal(ReviewStatus.NeedsReview, (await db.GroupReviews.SingleAsync()).Status);
            var ret0 = await db.Approvals.SingleAsync(a => a.Decision == "return");
            Assert.Equal(("partner", "단정 표현을 고쳐 주세요"), (ret0.ActorRole, ret0.Comment));
            await db.CriticFindings.ExecuteDeleteAsync(); // 지적받은 적발을 없앤 상태
        }
        var again = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = await VersionAsync(g.Id) });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal("AwaitingPartner", (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        await using var chk = pg.CreateDb();
        Assert.Equal(0, await chk.StepRuns.CountAsync(s => s.Step == "notify"));
    }

    [Fact]
    public async Task Decisions_wait_while_group_steps_are_active()
    {
        var g = await SeedAsync();
        await AiSeed.StepAsync(pg, g.RunId, "critic_judge", g.Id); // Running
        await using var f = Factory();
        var r = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 1 });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("ANALYSIS_IN_PROGRESS", await CodeAsync(r));
        Assert.Equal("ANALYSIS_IN_PROGRESS", await CodeAsync(await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/reject", new { version = g.Version, comment = "x" })));
        await using var db = pg.CreateDb();
        Assert.Equal(0, await db.Approvals.CountAsync());
    }

    [Fact]
    public async Task Notify_after_confirm_mentions_partner_signoff()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 1 });
        await As(f, "partner").PostAsJsonAsync($"/api/reviews/{g.Id}/confirm", new { version = await VersionAsync(g.Id) });
        Guid stepId;
        await using (var db = pg.CreateDb())
        {
            var step = await db.StepRuns.SingleAsync(s => s.Step == "notify");
            step.Status = StepStatus.Running;
            await db.SaveChangesAsync();
            stepId = step.Id;
        }
        await AiSeed.ExecuteAsync(pg, new LexWatch.Api.Workflow.Steps.NotifyStep(), stepId);
        await using var chk = pg.CreateDb();
        var body = (await chk.Notifications.SingleAsync()).Body;
        Assert.Matches(@"\(승인: 변호사 \d{4}-\d{2}-\d{2} \d{2}:\d{2}, 파트너 최종 승인 \d{4}-\d{2}-\d{2} \d{2}:\d{2}\)$", body);
    }
}
