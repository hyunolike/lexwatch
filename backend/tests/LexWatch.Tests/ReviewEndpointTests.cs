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
        Assert.Equal(2, list[0].GetProperty("evidenceCount").GetInt32());

        var d = await c.GetFromJsonAsync<JsonElement>($"/api/reviews/{g.Id}");
        Assert.Equal(g.Version, d.GetProperty("version").GetUInt32());
        Assert.Equal("제23조", d.GetProperty("oldNew")[0].GetProperty("articleNo").GetString());
        Assert.Equal("OC-01", d.GetProperty("draft").GetProperty("sentences")[0].GetProperty("findings")[0].GetProperty("ruleId").GetString());
        Assert.Equal(2, d.GetProperty("evidence").GetArrayLength());
        Assert.Equal("agent", d.GetProperty("draft").GetProperty("createdBy").GetString());
    }

    [Fact]
    public async Task Detail_returns_evidence_excerpt_and_official_change_marks()
    {
        var g = await SeedAsync();
        await using (var db = pg.CreateDb())
        {
            var row = await db.Evidence.SingleAsync(e => e.RunId == g.RunId && e.EvidenceId == "oldnew:L1:제23조");
            row.ChangeMarksJson = """{"old":[[0,3]],"new":[[1,4]]}""";
            await db.SaveChangesAsync();
        }
        await using var f = Factory();
        var d = await As(f, "lawyer").GetFromJsonAsync<JsonElement>($"/api/reviews/{g.Id}");

        var on = d.GetProperty("oldNew")[0];
        Assert.False(on.GetProperty("beforeMissing").GetBoolean());
        Assert.False(on.GetProperty("afterMissing").GetBoolean());
        Assert.Equal(0, on.GetProperty("oldMarks")[0][0].GetInt32());
        Assert.Equal(3, on.GetProperty("oldMarks")[0][1].GetInt32());
        Assert.Equal(4, on.GetProperty("newMarks")[0][1].GetInt32());

        var ev = d.GetProperty("evidence").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!);
        Assert.Equal("사용자는 근로자에게 정당한 이유 없이 해고를 하지 못한다.", ev["law:L1:제23조"].GetProperty("excerpt").GetString());
        // oldnew 근거는 신조문 부분에서 240자로 자르고 말줄임표를 붙인다.
        Assert.Equal(new string('가', 240) + "…", ev["oldnew:L1:제23조"].GetProperty("excerpt").GetString());
    }

    [Fact]
    public async Task Detail_without_change_marks_returns_null_marks()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var d = await As(f, "lawyer").GetFromJsonAsync<JsonElement>($"/api/reviews/{g.Id}");
        Assert.Equal(JsonValueKind.Null, d.GetProperty("oldNew")[0].GetProperty("oldMarks").ValueKind);
    }

    [Fact]
    public async Task Approve_enqueues_notify_and_records_actor()
    {
        var g = await SeedAsync();
        // 적발이 있으면 파트너 최종 승인을 기다린다(ai-governance GV-07, GovernanceTests). 여기서는 적발 없는 승인을 본다.
        await using (var db0 = pg.CreateDb()) await db0.CriticFindings.ExecuteDeleteAsync();
        await using var f = Factory();
        var resp = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, comment = "ok", acknowledgedFindingCount = 1 });
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
    public async Task Draftless_group_cannot_be_approved_or_edited_but_can_be_rejected()
    {
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id, "labor", ReviewStatus.NeedsReview);
        await using var f = Factory();
        var c = As(f, "lawyer");

        var approve = await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version });
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        Assert.Equal("NO_DRAFT", (await approve.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var edit = await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new
        {
            version = g.Version,
            editedSentences = new[] { new { sentence = "새 문장", citations = new[] { "law:L1:제23조" } } },
        });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("NO_DRAFT", (await edit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        await using (var db = pg.CreateDb())
        {
            Assert.Equal(ReviewStatus.NeedsReview, (await db.GroupReviews.SingleAsync()).Status);
            Assert.Empty(await db.StepRuns.Where(s => s.Step == "notify").ToListAsync());
            Assert.Empty(await db.MemoDrafts.ToListAsync());
        }

        var reject = await c.PostAsJsonAsync($"/api/reviews/{g.Id}/reject", new { version = g.Version, comment = "근거 부족" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        await using var db2 = pg.CreateDb();
        Assert.Equal(ReviewStatus.Rejected, (await db2.GroupReviews.SingleAsync()).Status);
    }

    [Fact]
    public async Task Approved_group_whose_notify_failed_cannot_be_approved_again()
    {
        var g = await SeedAsync();
        await using (var db0 = pg.CreateDb()) await db0.CriticFindings.ExecuteDeleteAsync(); // 바로 Approved로 가는 경로
        await using var f = Factory();
        var c = As(f, "lawyer");
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 1 })).StatusCode);
        await using (var db = pg.CreateDb())
        {
            var step = await db.StepRuns.SingleAsync(s => s.Step == "notify");
            step.Status = StepStatus.Failed; // 알림 실패 뒤에도 그룹은 Approved다
            await db.SaveChangesAsync();
        }
        uint version;
        await using (var db = pg.CreateDb()) version = (await db.GroupReviews.SingleAsync()).Version;
        var again = await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version, acknowledgedFindingCount = 1 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        await using var chk = pg.CreateDb();
        Assert.Equal(1, await chk.Approvals.CountAsync());
        Assert.Equal(1, await chk.StepRuns.CountAsync(s => s.Step == "notify"));
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
        Assert.Equal(RunStatus.Completed, (await db.WorkflowRuns.SingleAsync()).Status);
        Assert.NotNull((await db.WorkflowRuns.SingleAsync()).FinishedAt);
    }

    [Fact]
    public async Task Approve_keeps_run_open_until_notified()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 1 });
        await using var db = pg.CreateDb();
        Assert.NotEqual(RunStatus.Completed, (await db.WorkflowRuns.SingleAsync()).Status);
    }

    [Fact]
    public async Task Step_handlers_are_wired()
    {
        await using var f = Factory();
        using var scope = f.Services.CreateScope();
        var steps = scope.ServiceProvider.GetServices<LexWatch.Api.Workflow.IStepHandler>().Select(h => h.Step).ToHashSet();
        foreach (var s in new[] { "fetch_articles", "classify", "analyze", "notify", "critic_judge" })
            Assert.Contains(s, steps);
        Assert.True(steps.Count >= 7, string.Join(",", steps));
    }

    [Fact]
    public async Task Concurrent_approve_and_reject_only_one_wins()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var c = As(f, "lawyer");
        var t1 = c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 1 });
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
            version = g.Version, acknowledgedFindingCount = 1, // 고친 0번 문장의 3단 적발이 "(수정 전 문장)"으로 남는다
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

    private static async Task<JsonElement> UnresolvedAsync(HttpResponseMessage resp)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("UNRESOLVED_FINDINGS", body.GetProperty("code").GetString());
        return body.GetProperty("findings");
    }

    private async Task AssertUndecidedAsync()
    {
        await using var db = pg.CreateDb();
        Assert.Equal(ReviewStatus.WaitingApproval, (await db.GroupReviews.SingleAsync()).Status);
        Assert.Equal(0, await db.Approvals.CountAsync());
        Assert.Equal(0, await db.StepRuns.CountAsync(s => s.Step == "notify"));
        Assert.Equal(1, await db.MemoDrafts.CountAsync());
    }

    [Fact]
    public async Task Approve_with_stored_findings_requires_matching_acknowledgement()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var c = As(f, "lawyer");
        var findings = await UnresolvedAsync(await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version }));
        var x = Assert.Single(findings.EnumerateArray());
        Assert.Equal((0, 3, "OC-01", "단정"), (x.GetProperty("sentenceIndex").GetInt32(), x.GetProperty("tier").GetInt32(),
            x.GetProperty("ruleId").GetString(), x.GetProperty("message").GetString()));
        await AssertUndecidedAsync();

        await UnresolvedAsync(await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 2 }));
        await AssertUndecidedAsync();

        var ok = await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version, acknowledgedFindingCount = 1 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        await using var db = pg.CreateDb();
        Assert.Equal(1, (await db.Approvals.SingleAsync()).AcknowledgedFindingCount);
    }

    [Fact]
    public async Task Approve_without_findings_needs_no_acknowledgement()
    {
        var g = await SeedAsync();
        await using (var db = pg.CreateDb()) await db.CriticFindings.ExecuteDeleteAsync();
        await using var f = Factory();
        var resp = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        await using var chk = pg.CreateDb();
        Assert.Equal(0, (await chk.Approvals.SingleAsync()).AcknowledgedFindingCount);
    }

    [Fact]
    public async Task Unedited_approval_rechecks_tier_2_with_current_rules()
    {
        var g = await SeedAsync();
        await using (var db = pg.CreateDb())
        {
            await db.CriticFindings.ExecuteDeleteAsync(); // 이전 규칙으로 판정돼 저장된 적발이 없다
            var d = await db.MemoDrafts.SingleAsync();
            d.SentencesJson = """[{"sentence":"제24조에 따라 해고가 제한된다.","citations":["law:L1:제23조"]}]""";
            await db.SaveChangesAsync();
        }
        await using var f = Factory();
        var x = Assert.Single((await UnresolvedAsync(
            await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new { version = g.Version }))).EnumerateArray());
        Assert.Equal(("C2-MISMATCH", 2), (x.GetProperty("ruleId").GetString(), x.GetProperty("tier").GetInt32()));
    }

    [Fact]
    public async Task Edited_sentences_are_rechecked_and_findings_saved_on_new_draft()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var c = As(f, "lawyer");
        object Body(int? ack) => new
        {
            version = g.Version, acknowledgedFindingCount = ack,
            editedSentences = new[]
            {
                new { sentence = "제24조에 따라 해고가 제한된다.", citations = new[] { "law:L1:제23조" } },
                new { sentence = "신구조문이 있다.", citations = new[] { "oldnew:L1:제23조" } },
            },
        };
        var findings = (await UnresolvedAsync(await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", Body(null)))).EnumerateArray()
            .Select(x => (x.GetProperty("sentenceIndex").GetInt32(), x.GetProperty("tier").GetInt32(), x.GetProperty("message").GetString()))
            .ToList();
        Assert.Equal([(0, 2, "인용한 근거 본문에 없는 표기: 제24조"), (0, 3, "(수정 전 문장) 단정")], findings);
        await AssertUndecidedAsync();

        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/reviews/{g.Id}/approve", Body(2))).StatusCode);
        await using var db = pg.CreateDb();
        var v2 = await db.MemoDrafts.SingleAsync(d => d.Version == 2);
        var stored = await db.CriticFindings.Where(cf => cf.MemoDraftId == v2.Id).OrderBy(cf => cf.Tier).ToListAsync();
        Assert.Equal(["C2-MISMATCH", "OC-01"], stored.Select(cf => cf.RuleId).ToArray());
        Assert.Equal(2, (await db.Approvals.SingleAsync()).AcknowledgedFindingCount);
    }

    [Fact]
    public async Task Tier_3_finding_follows_its_sentence_when_another_is_inserted()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var resp = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new
        {
            version = g.Version,
            editedSentences = new[]
            {
                new { sentence = "새로 넣은 문장이다.", citations = new[] { "law:L1:제23조" } },
                new { sentence = "해고가 제한된다.", citations = new[] { "law:L1:제23조" } },
                new { sentence = "신구조문이 있다.", citations = new[] { "oldnew:L1:제23조" } },
            },
        });
        var x = Assert.Single((await UnresolvedAsync(resp)).EnumerateArray());
        Assert.Equal(("OC-01", 1, "단정"), (x.GetProperty("ruleId").GetString(), x.GetProperty("sentenceIndex").GetInt32(),
            x.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Deleted_sentence_drops_its_tier_3_finding()
    {
        var g = await SeedAsync();
        await using var f = Factory();
        var resp = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new
        {
            version = g.Version,
            editedSentences = new[] { new { sentence = "신구조문이 있다.", citations = new[] { "oldnew:L1:제23조" } } },
        });
        // 0번 위치에 다른 문장이 왔으므로 "(수정 전 문장)"으로 남는다. 삭제로 위치가 없어지는 경우는 아래에서 본다.
        Assert.Equal("(수정 전 문장) 단정", Assert.Single((await UnresolvedAsync(resp)).EnumerateArray()).GetProperty("message").GetString());

        await using (var db = pg.CreateDb())
        {
            var finding = await db.CriticFindings.SingleAsync();
            finding.SentenceIndex = 1; // 1번 문장의 적발로 바꾼 뒤 1번 문장을 지운다
            await db.SaveChangesAsync();
        }
        var ok = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/approve", new
        {
            version = g.Version,
            editedSentences = new[] { new { sentence = "해고가 제한된다.", citations = new[] { "law:L1:제23조" } } },
        });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    private static readonly object ChatOk = new
    {
        answer = "해고 제한이 강화돼요.", citations = new[] { "law:L1:제23조" }, refused = false, provider = AiStub.Provider(),
    };

    private WebApplicationFactory<Program> ChatFactory(AiStub ai)
    {
        Environment.SetEnvironmentVariable("AI_BASE_URL", ai.Server.Url);
        return Factory().WithWebHostBuilder(b => b.UseSetting("JOB_TOKEN", "secret-token"));
    }

    [Fact]
    public async Task Chat_quota_per_ip_returns_429_after_10()
    {
        var g = await SeedAsync();
        using var ai = new AiStub();
        ai.Json("/review-chat", 200, ChatOk);
        await using var f = ChatFactory(ai);
        var c = As(f, "lawyer");
        for (var i = 0; i < 10; i++)
        {
            var ok = await c.PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "제23조 해고 규정은?" });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("law:L1:제23조", body.GetProperty("citations")[0].GetString());
            Assert.Equal("nvidia", body.GetProperty("provider").GetProperty("kind").GetString());
        }
        var over = await c.PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "제23조 해고 규정은?" });
        Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
        Assert.Equal("CHAT_QUOTA_EXCEEDED", (await over.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(10, ai.Requests("/review-chat").Count);

        await using var db = pg.CreateDb();
        var rows = await db.ChatQuotas.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(10, rows.Single(r => r.IpHash == "__global__").Count);
        Assert.DoesNotContain(rows, r => r.IpHash.Contains("127.0.0.1"));
    }

    [Fact]
    public async Task Chat_global_cap_returns_429_and_does_not_charge_ip()
    {
        var g = await SeedAsync();
        await using (var db0 = pg.CreateDb())
        {
            db0.ChatQuotas.Add(new ChatQuota { Day = DateOnly.FromDateTime(DateTime.UtcNow), IpHash = "__global__", Count = 200 });
            await db0.SaveChangesAsync();
        }
        using var ai = new AiStub();
        ai.Json("/review-chat", 200, ChatOk);
        await using var f = ChatFactory(ai);
        var r = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "제23조 해고 규정은?" });
        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        await using var db = pg.CreateDb();
        Assert.Single(await db.ChatQuotas.ToListAsync());
    }

    [Fact]
    public async Task Chat_ai_failure_is_refunded_and_role_and_input_are_checked()
    {
        var g = await SeedAsync();
        using var ai = new AiStub();
        ai.Json("/review-chat", 500, new { code = "X" });
        await using var f = ChatFactory(ai);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await As(f, "admin").PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "q" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = new string('가', 501) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{Guid.NewGuid()}/chat", new { question = "q" })).StatusCode);
        var r = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "제23조?" });
        Assert.Equal((HttpStatusCode)424, r.StatusCode);
        await using var db = pg.CreateDb();
        Assert.All(await db.ChatQuotas.ToListAsync(), q => Assert.Equal(0, q.Count));
    }

    [Fact]
    public async Task Chat_without_job_token_fails_closed()
    {
        var g = await SeedAsync();
        using var ai = new AiStub();
        ai.Json("/review-chat", 200, ChatOk);
        Environment.SetEnvironmentVariable("AI_BASE_URL", ai.Server.Url);
        await using var f = Factory().WithWebHostBuilder(b => b.UseSetting("JOB_TOKEN", ""));
        var r = await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "제23조?" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.Equal("CHAT_DISABLED", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await using var db = pg.CreateDb();
        Assert.Empty(await db.ChatQuotas.ToListAsync());
        Assert.Empty(ai.Requests("/review-chat"));
    }

    [Fact]
    public async Task Chat_cancelled_between_acquire_and_answer_is_refunded()
    {
        var g = await SeedAsync();
        using var ai = new AiStub();
        ai.Json("/review-chat", 200, ChatOk, delayMs: 5000);
        await using var f = ChatFactory(ai);
        using var cts = new CancellationTokenSource(500);
        try { await As(f, "lawyer").PostAsJsonAsync($"/api/reviews/{g.Id}/chat", new { question = "제23조?" }, cts.Token); }
        catch (OperationCanceledException) { }
        for (var i = 0; i < 50; i++)
        {
            await using var db = pg.CreateDb();
            var rows = await db.ChatQuotas.ToListAsync();
            if (rows.Count > 0 && rows.All(q => q.Count == 0)) return;
            await Task.Delay(100);
        }
        await using var last = pg.CreateDb();
        Assert.All(await last.ChatQuotas.ToListAsync(), q => Assert.Equal(0, q.Count));
        Assert.NotEmpty(await last.ChatQuotas.ToListAsync());
    }

    [Fact]
    public async Task Detail_includes_ai_trail_in_order_with_latest_draft_findings()
    {
        var g = await SeedAsync();
        await using (var db = pg.CreateDb())
        {
            db.StepRuns.Add(new StepRun
            {
                Id = Guid.NewGuid(), RunId = g.RunId, GroupReviewId = g.Id, Step = "analyze", Attempt = 1, Status = StepStatus.Succeeded,
                OutputJson = """{"toolCalls": 3, "retrieval": {"status": "ok", "mode": "Hybrid", "sent": 8, "denseUsed": true}}""",
                ProviderKind = "nvidia", ProviderName = "nemotron", NotBefore = DateTimeOffset.UtcNow, StartedAt = DateTimeOffset.UtcNow,
            });
            db.StepRuns.Add(new StepRun
            {
                Id = Guid.NewGuid(), RunId = g.RunId, GroupReviewId = g.Id, Step = "critic_judge", Attempt = 1, Status = StepStatus.Succeeded,
                OutputJson = """{"findings": 5}""", ProviderKind = "nvidia", ProviderName = "nemotron",
                NotBefore = DateTimeOffset.UtcNow, StartedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        await using var f = Factory();
        var d = await As(f, "lawyer").GetFromJsonAsync<JsonElement>($"/api/reviews/{g.Id}");
        var stages = d.GetProperty("aiTrail").GetProperty("stages").EnumerateArray().ToList();
        Assert.Equal(["collect", "classify", "index", "analyze", "critic_citation", "critic_source", "critic_judge", "decision"],
            stages.Select(x => x.GetProperty("key").GetString()));
        var analyze = stages[3];
        Assert.Equal(("ai", "done", "nvidia"), (analyze.GetProperty("actor").GetString(), analyze.GetProperty("status").GetString(),
            analyze.GetProperty("providerKind").GetString()));
        Assert.Equal(8, analyze.GetProperty("detail").GetProperty("sent").GetInt32());
        Assert.Equal(1, stages[6].GetProperty("detail").GetProperty("findings").GetInt32());   // 단계 출력(5)이 아니라 초안의 3단 적발 1건
        Assert.Equal("human", stages[7].GetProperty("actor").GetString());
    }
}
