using System.Text.Json;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using LexWatch.Api.Workflow.Steps;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

[Collection("pg")]
public class AnalyzeStepTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly FakeTime Time = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));

    private static void StubAnalyze(AiStub ai, string status = "ok", string kind = "nvidia", string name = "nim") =>
        ai.Json("/analyze", 200, new
        {
            status,
            memo = status == "ok"
                ? new[] { new { sentence = "해고 사유 제한이 강화된다.", citations = new[] { "oldnew:L1:제23조" } } }
                : new object[0],
            open_questions = new[] { "시행 시기 확인 필요" },
            tool_calls = new object[]
            {
                new { seq = 1, tool = "get_article", args = new { law_id = "L1", article = "제23조" }, result_summary = "조문 조회", evidence_ids = new[] { "law:L1:제24조" } },
                new { seq = 2, tool = "search", args = new { q = "해고" }, result_summary = "검색", evidence_ids = new string[0] },
            },
            new_evidence = new[] { new { id = "law:L1:제24조", kind = "law", title = "근로기준법 제24조", body = "경영상 해고" } },
            provider = AiStub.Provider(kind, name),
        });

    private static AnalyzeStep Step(AiStub ai) => new(ai.Gateway(), Time);

    [Fact]
    public async Task Stores_tool_calls_new_evidence_and_draft()
    {
        using var ai = new AiStub();
        StubAnalyze(ai);
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id);
        var step = await AiSeed.StepAsync(pg, run.Id, "analyze", g.Id);

        var ok = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, Step(ai), step.Id));

        var next = Assert.Single(ok.Next);
        Assert.Equal("critic_citation", next.Step);
        Assert.Equal(g.Id, next.GroupReviewId);

        await using var v = pg.CreateDb();
        var calls = await v.AgentToolCalls.Where(c => c.StepRunId == step.Id).OrderBy(c => c.Seq).ToListAsync();
        Assert.Equal([1, 2], calls.Select(c => c.Seq));
        Assert.Equal("get_article", calls[0].Tool);
        Assert.Equal(["law:L1:제24조"], calls[0].EvidenceIds);
        Assert.Equal("L1", JsonDocument.Parse(calls[0].ArgsJson).RootElement.GetProperty("law_id").GetString());

        var ev = await v.Evidence.SingleAsync(e => e.RunId == run.Id && e.EvidenceId == "law:L1:제24조");
        Assert.Equal("경영상 해고", ev.Body);

        var draft = await v.MemoDrafts.SingleAsync(d => d.GroupReviewId == g.Id);
        Assert.Equal(1, draft.Version);
        Assert.Equal("agent", draft.CreatedBy);
        var sentences = JsonDocument.Parse(draft.SentencesJson).RootElement;
        Assert.Equal("해고 사유 제한이 강화된다.", sentences[0].GetProperty("sentence").GetString());
        Assert.Equal("oldnew:L1:제23조", sentences[0].GetProperty("citations")[0].GetString());
        Assert.Contains("시행 시기 확인 필요", draft.OpenQuestionsJson);

        // 요청에는 이 실행의 근거가 실렸다
        var req = ai.Requests("/analyze").Single().RootElement;
        Assert.Equal(2, req.GetProperty("evidence").GetArrayLength());
        Assert.Equal("labor", req.GetProperty("group_code").GetString());
        Assert.Equal("100003", req.GetProperty("law").GetProperty("mst").GetString());
    }

    [Fact]
    public async Task Provider_kind_is_copied_from_response()
    {
        foreach (var (kind, name) in new[] { ("nvidia", "nemotron"), ("fallback", "mock") })
        {
            await pg.ResetAsync();
            using var ai = new AiStub();
            StubAnalyze(ai, kind: kind, name: name);
            var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
            var g = await AiSeed.GroupAsync(pg, run.Id);
            var step = await AiSeed.StepAsync(pg, run.Id, "analyze", g.Id);

            var ok = Assert.IsType<StepResult.Succeeded>(await AiSeed.ExecuteAsync(pg, Step(ai), step.Id));

            Assert.Equal(kind, ok.ProviderKind);
            Assert.Equal(name, ok.ProviderName);
        }
    }

    [Fact]
    public async Task Retried_step_replaces_tool_calls_and_bumps_draft_version()
    {
        using var ai = new AiStub();
        StubAnalyze(ai);
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id);
        var step = await AiSeed.StepAsync(pg, run.Id, "analyze", g.Id);

        await AiSeed.ExecuteAsync(pg, Step(ai), step.Id);
        await AiSeed.ExecuteAsync(pg, Step(ai), step.Id); // 같은 StepRun 재시도

        await using var v = pg.CreateDb();
        Assert.Equal(2, await v.AgentToolCalls.CountAsync(c => c.StepRunId == step.Id));
        Assert.Equal([1, 2], (await v.MemoDrafts.Where(d => d.GroupReviewId == g.Id).OrderBy(d => d.Version).ToListAsync()).Select(d => d.Version));
        Assert.Equal(1, await v.Evidence.CountAsync(e => e.EvidenceId == "law:L1:제24조"));
    }

    [Fact]
    public async Task Insufficient_evidence_needs_review_and_stores_no_draft()
    {
        using var ai = new AiStub();
        StubAnalyze(ai, status: "insufficient_evidence");
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id);
        var step = await AiSeed.StepAsync(pg, run.Id, "analyze", g.Id);

        var result = await AiSeed.ExecuteAsync(pg, Step(ai), step.Id);

        var nr = Assert.IsType<StepResult.NeedsReview>(result);
        Assert.Equal("INSUFFICIENT_EVIDENCE", nr.Reason);
        Assert.Equal(("nvidia", "nim"), (nr.ProviderKind, nr.ProviderName));
        await using var v = pg.CreateDb();
        Assert.Empty(await v.MemoDrafts.ToListAsync());
        Assert.Equal(2, await v.AgentToolCalls.CountAsync());
    }

    [Fact]
    public async Task Rate_limit_returns_retry_with_after_and_writes_nothing()
    {
        using var ai = new AiStub();
        ai.Json("/analyze", 429, new { code = "LLM_RATE_LIMITED", retry_after_s = 7 });
        var (run, _) = await AiSeed.RunWithEvidenceAsync(pg);
        var g = await AiSeed.GroupAsync(pg, run.Id);
        var step = await AiSeed.StepAsync(pg, run.Id, "analyze", g.Id);

        var retry = Assert.IsType<StepResult.Retry>(await AiSeed.ExecuteAsync(pg, Step(ai), step.Id));
        Assert.Equal(TimeSpan.FromSeconds(7), retry.After);
        await using var v = pg.CreateDb();
        Assert.Empty(await v.AgentToolCalls.ToListAsync());
        Assert.Empty(await v.MemoDrafts.ToListAsync());
    }
}
