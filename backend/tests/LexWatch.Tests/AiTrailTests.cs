using LexWatch.Api.Data.Entities;
using LexWatch.Api.Reviews;

namespace LexWatch.Tests;

/// <summary>ai-footprints AF-01~04. 출력 JSON은 운영 실행 b015c465(2026-11-24)와 재현 시드에서 옮겼다.</summary>
public class AiTrailTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 24, 1, 0, 0, TimeSpan.Zero);

    private static GroupReview Review(ReviewStatus status = ReviewStatus.NeedsReview, double? conf = 0.9234) =>
        new() { Id = Guid.NewGuid(), RunId = Guid.NewGuid(), GroupCode = "finance", Status = status, Confidence = conf };

    private static StepRun Step(GroupReview r, string step, StepStatus status, string? output, int attempt = 1,
        string provider = "cloudflare", string? name = "cloudflare-workers-ai", bool group = false, int minutes = 0, bool started = true) => new()
    {
        Id = Guid.NewGuid(), RunId = r.RunId, GroupReviewId = group ? r.Id : null, Step = step, Attempt = attempt, Status = status,
        OutputJson = output, ProviderKind = provider, ProviderName = name, NotBefore = T.AddMinutes(minutes),
        StartedAt = started ? T.AddMinutes(minutes) : null,
    };

    private static List<StepRun> Real(GroupReview r) =>
    [
        Step(r, "fetch_articles", StepStatus.Succeeded, """{"evidence": 54, "oldnewStatus": {"oldnew": "ok"}}""", provider: "none", name: null),
        // 자동 재시도 다섯 번이 실패한 행(시도 5)과 관리자가 다시 실행한 새 행(시도 1). 새 행이 마지막이다.
        Step(r, "classify", StepStatus.Failed, null, attempt: 5, provider: "none", name: null, minutes: 1),
        Step(r, "classify", StepStatus.Succeeded, """{"groups": [{"group": "finance", "confidence": 0.92}]}""", minutes: 30),
        Step(r, "index_articles", StepStatus.Succeeded, """{"chunks": 54, "embedded": 54, "dense": "ok", "model": "@cf/baai/bge-m3"}""",
            name: "@cf/baai/bge-m3", minutes: 31),
        Step(r, "analyze", StepStatus.Succeeded,
            """{"toolCalls": 2, "newEvidence": 0, "draftVersion": 1, "retrieval": {"status": "ok", "mode": "Hybrid", "sent": 8, "denseUsed": true}}""",
            group: true, minutes: 32),
        Step(r, "critic_citation", StepStatus.Succeeded, """{"findings": 0, "draftVersion": 1}""", provider: "none", name: null, group: true, minutes: 33),
        Step(r, "critic_source", StepStatus.Succeeded, """{"findings": 0, "draftVersion": 1}""", provider: "none", name: null, group: true, minutes: 33),
        Step(r, "critic_judge", StepStatus.Succeeded, """{"findings": 2, "draftVersion": 1}""", group: true, minutes: 34),
    ];

    private static readonly MemoDraft AgentDraft = new() { Id = Guid.NewGuid(), Version = 1, CreatedBy = "agent" };

    [Fact]
    public void Real_run_gives_eight_ordered_stages_with_actor_and_detail()
    {
        var r = Review();
        var t = AiTrail.Build(r, Real(r), [], AgentDraft, [3, 3]);
        Assert.Equal(["collect", "classify", "index", "analyze", "critic_citation", "critic_source", "critic_judge", "decision"],
            t.Select(s => s.Key));
        Assert.Equal(["rule", "ai", "ai", "ai", "rule", "rule", "ai", "human"], t.Select(s => s.Actor));

        Assert.Equal(54, t[0].Detail["evidence"]);
        Assert.Equal("ok", t[0].Detail["oldNew"]);
        Assert.Null(t[0].ProviderKind);

        // 시도 번호가 큰 실패 행이 아니라 나중에 다시 실행한 성공 행을 고른다
        Assert.Equal("done", t[1].Status);
        Assert.Equal(0.92, t[1].Detail["confidence"]);
        Assert.Equal(("cloudflare", "cloudflare-workers-ai"), (t[1].ProviderKind, t[1].ProviderName));

        Assert.Equal(54, t[2].Detail["chunks"]);
        Assert.Equal("hybrid", t[3].Detail["retrievalMode"]);
        Assert.Equal(8, t[3].Detail["sent"]);
        Assert.Equal(true, t[3].Detail["denseUsed"]);
        Assert.Equal(2, t[3].Detail["toolCalls"]);
        Assert.Equal(false, t[3].Detail["insufficient"]);

        // 적발 수는 최신 초안 기준(tier별)
        Assert.Equal(0, t[4].Detail["findings"]);
        Assert.Equal(2, t[6].Detail["findings"]);
        Assert.Equal("waiting", t[7].Status);
        Assert.Equal(false, t[7].Detail["edited"]);
    }

    [Fact]
    public void Retried_row_created_later_wins_even_when_pending()
    {
        var r = Review();
        var steps = Real(r);
        steps.Add(Step(r, "critic_judge", StepStatus.Pending, null, attempt: 0, group: true, minutes: 50, started: false));
        Assert.Equal("running", AiTrail.Build(r, steps, [], AgentDraft, []).Single(s => s.Key == "critic_judge").Status);
    }

    [Fact]
    public void Fallback_providers_and_keyword_only_index_are_rules()
    {
        var r = Review();
        var t = AiTrail.Build(r, [
            Step(r, "index_articles", StepStatus.Succeeded, """{"chunks": 10, "embedded": 0, "dense": "unavailable"}""",
                provider: "fallback", name: "keyword-only"),
            Step(r, "analyze", StepStatus.Succeeded, """{"toolCalls": 0}""", provider: "fallback", name: "extract-fallback", group: true),
        ], [], AgentDraft, []);
        var index = t.Single(s => s.Key == "index");
        Assert.Equal(("rule", true, (string?)null), (index.Actor, index.Detail["fallback"], index.ProviderKind));
        Assert.Equal("rule", t.Single(s => s.Key == "analyze").Actor);
    }

    [Fact]
    public void Replay_rule_steps_hide_provider_but_ai_steps_keep_replay()
    {
        var r = Review(conf: 0.871);
        var t = AiTrail.Build(r, [
            Step(r, "fetch_articles", StepStatus.Succeeded, """{"evidence": 2}""", attempt: 0, provider: "replay", name: "재현 기록"),
            Step(r, "classify", StepStatus.Succeeded, """{"relevant": ["finance"]}""", attempt: 0, provider: "replay", name: "재현 기록"),
            Step(r, "analyze", StepStatus.Succeeded, """{"toolCalls": 3}""", attempt: 0, provider: "replay", name: "재현 기록", group: true),
        ], [], AgentDraft, []);
        Assert.Null(t[0].ProviderKind);
        Assert.Equal("replay", t[1].ProviderKind);
        Assert.Equal(0.87, t[1].Detail["confidence"]);
        Assert.Equal("skipped", t[2].Status);   // 분석은 돌았는데 색인 기록이 없다
        Assert.Equal(3, t[3].Detail["toolCalls"]);
    }

    [Fact]
    public void Insufficient_evidence_stops_later_stages_and_broken_json_gives_empty_detail()
    {
        var r = Review();
        var t = AiTrail.Build(r, [
            Step(r, "fetch_articles", StepStatus.Succeeded, "{not json", provider: "none"),
            Step(r, "analyze", StepStatus.NeedsReview, """{"toolCalls": 0, "newEvidence": 0, "status": "insufficient_evidence"}""", group: true),
        ], [], null, []);
        Assert.Empty(t[0].Detail);
        Assert.Equal(("needs_review", true), (t[3].Status, t[3].Detail["insufficient"]));
        Assert.All(t.Where(s => s.Key.StartsWith("critic_")), s => Assert.Equal("not_run", s.Status));
        Assert.Equal("waiting", t[1].Status);   // 앞 단계(분류)는 멈춤과 무관
    }

    [Fact]
    public void Running_step_keeps_later_stages_waiting()
    {
        var r = Review(ReviewStatus.Analyzing);
        var t = AiTrail.Build(r, [
            Step(r, "classify", StepStatus.Succeeded, "{}"),
            Step(r, "index_articles", StepStatus.RetryScheduled, null),
        ], [], null, []);
        Assert.Equal("running", t[2].Status);
        Assert.Equal("waiting", t[3].Status);
    }

    [Theory]
    [InlineData(ReviewStatus.AwaitingPartner, "running")]
    [InlineData(ReviewStatus.Notified, "done")]
    [InlineData(ReviewStatus.Rejected, "done")]
    [InlineData(ReviewStatus.NeedsReview, "waiting")]
    [InlineData(ReviewStatus.ClosedIrrelevant, "not_run")]
    public void Decision_status_follows_review_status(ReviewStatus status, string expected)
    {
        var r = Review(status);
        var approvals = new List<Approval> { new() { Decision = "approve", ActorRole = "lawyer", DecidedAt = T } };
        if (status == ReviewStatus.NeedsReview)
            approvals.Add(new() { Decision = "return", ActorRole = "partner", DecidedAt = T.AddHours(1) });
        var lawyerDraft = new MemoDraft { Id = Guid.NewGuid(), Version = 2, CreatedBy = "lawyer" };
        var t = AiTrail.Build(r, Real(r), approvals, lawyerDraft, [1]);
        var d = t.Last();
        Assert.Equal(expected, d.Status);
        Assert.Equal(true, d.Detail["edited"]);
        if (status == ReviewStatus.AwaitingPartner) Assert.Equal("partner", d.Detail["awaiting"]);
        if (status == ReviewStatus.NeedsReview) Assert.Equal(("return", "partner"), (d.Detail["decision"], d.Detail["role"]));
        // 수정 승인 때 다시 검사한 초안의 적발
        Assert.Equal((1, true), (t[4].Detail["findings"], t[4].Detail["recheckedOnApprove"]));
    }
}
