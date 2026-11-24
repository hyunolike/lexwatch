using System.Text.Json;
using LexWatch.Api.Data.Entities;

namespace LexWatch.Api.Reviews;

/// <summary>
/// 초안이 만들어진 과정의 한 단계(ai-footprints AF-01). actor: ai, rule, human.
/// status: done, needs_review, failed, running, waiting, skipped, not_run.
/// </summary>
public sealed record AiTrailStage(string Key, string Actor, string Status, string? ProviderKind, string? ProviderName,
    Dictionary<string, object?> Detail);

/// <summary>
/// 검토 상세의 "이 초안이 만들어진 과정"을 만든다. DB를 읽지 않는 순수 함수라 실측 출력으로 바로 검사한다(설계 2~3절).
/// - 재시도는 같은 행의 Attempt를 올리고 관리자 다시 실행은 Attempt 0인 새 행을 만든다. 그래서 마지막 기록은 시각으로 고른다.
/// - AI 단계라도 공급자가 fallback(키 없는 규칙 대체)이거나 색인에서 임베딩을 못 받았으면 actor를 rule로 바꾼다.
/// - 규칙 단계에는 공급자를 싣지 않는다(재현 기록은 모든 단계가 replay다).
/// - 검사 단계의 적발 수는 단계 출력이 아니라 최신 초안의 적발이다(수정 승인 때 다시 검사하므로).
/// </summary>
public static class AiTrail
{
    public sealed record StageDef(string Key, string Step, bool RunLevel, string Actor, int? Tier = null);

    public static readonly StageDef[] Stages =
    [
        new("collect", "fetch_articles", true, "rule"),
        new("classify", "classify", true, "ai"),
        new("index", "index_articles", true, "ai"),
        new("analyze", "analyze", false, "ai"),
        new("critic_citation", "critic_citation", false, "rule", 1),
        new("critic_source", "critic_source", false, "rule", 2),
        new("critic_judge", "critic_judge", false, "ai", 3),
    ];

    /// <param name="steps">이 실행(RunId)의 단계 기록 중 그룹 없는 것과 이 그룹 검토의 것.</param>
    /// <param name="findingTiers">최신 초안 적발의 tier 목록.</param>
    public static IReadOnlyList<AiTrailStage> Build(
        GroupReview review, IReadOnlyList<StepRun> steps, IReadOnlyList<Approval> approvals, MemoDraft? latestDraft,
        IReadOnlyList<int> findingTiers)
    {
        var list = new List<AiTrailStage>();
        var lastRows = new Dictionary<string, StepRun?>();
        foreach (var def in Stages)
            lastRows[def.Key] = steps
                .Where(s => s.Step == def.Step && (def.RunLevel ? s.GroupReviewId == null : s.GroupReviewId == review.Id))
                .OrderByDescending(s => s.StartedAt ?? s.NotBefore).ThenByDescending(s => s.Attempt).ThenByDescending(s => s.Id)
                .FirstOrDefault();
        var anyActive = lastRows.Values.Any(s => s is { Status: StepStatus.Pending or StepStatus.Running or StepStatus.RetryScheduled });
        var edited = latestDraft?.CreatedBy == "lawyer";

        var stopped = false;   // 앞 단계가 성공하지 못하고 끝났고 남은 일이 없다
        foreach (var def in Stages)
        {
            var last = lastRows[def.Key];
            if (last is null)
            {
                var status = stopped || review.Status == ReviewStatus.ClosedIrrelevant && !def.RunLevel ? "not_run"
                    // 색인은 분석이 이미 돌았는데 기록이 없을 때만 건너뛴 것이다(재현 예시 등).
                    : def.Key == "index" && lastRows["analyze"] is not null ? "skipped"
                    : "waiting";
                list.Add(new AiTrailStage(def.Key, def.Actor, status, null, null, []));
                continue;
            }
            var st = last.Status switch
            {
                StepStatus.Succeeded => "done",
                StepStatus.NeedsReview => "needs_review",
                StepStatus.Failed => "failed",
                _ => "running",
            };
            var detail = Detail(def, last, review, latestDraft, findingTiers);
            var actor = def.Actor;
            if (actor == "ai" && (last.ProviderKind == "fallback" || def.Key == "index" && detail.GetValueOrDefault("dense") is string d && d != "ok"))
            {
                actor = "rule";
                detail["fallback"] = true;
            }
            var showProvider = actor == "ai" && last.ProviderKind is not ("" or "none");
            list.Add(new AiTrailStage(def.Key, actor, st, showProvider ? last.ProviderKind : null,
                showProvider ? last.ProviderName : null, detail));
            if (st is "failed" or "needs_review" && !anyActive) stopped = true;
        }
        list.Add(Decision(review, approvals, edited));
        return list;
    }

    /// <summary>사람 단계. 상태는 그룹 검토 상태로 정하고 승인 기록은 detail에만 쓴다.</summary>
    private static AiTrailStage Decision(GroupReview review, IReadOnlyList<Approval> approvals, bool edited)
    {
        var detail = new Dictionary<string, object?> { ["edited"] = edited };
        var last = approvals.OrderByDescending(a => a.DecidedAt).FirstOrDefault();
        if (last is not null)
        {
            detail["decision"] = last.Decision;
            detail["role"] = last.ActorRole;
            detail["decidedAt"] = last.DecidedAt;
        }
        var status = review.Status switch
        {
            ReviewStatus.AwaitingPartner => "running",
            ReviewStatus.Approved or ReviewStatus.Notified or ReviewStatus.Rejected => "done",
            ReviewStatus.ClosedIrrelevant => "not_run",
            _ => "waiting",
        };
        if (review.Status == ReviewStatus.AwaitingPartner) detail["awaiting"] = "partner";
        return new AiTrailStage("decision", "human", status, null, null, detail);
    }

    private static Dictionary<string, object?> Detail(StageDef def, StepRun s, GroupReview review, MemoDraft? latestDraft,
        IReadOnlyList<int> findingTiers)
    {
        var d = new Dictionary<string, object?>();
        var root = Parse(s.OutputJson);
        switch (def.Key)
        {
            case "collect":
                if (Int(root, "evidence") is { } ev) d["evidence"] = ev;
                if (root is { } r0 && r0.TryGetProperty("oldnewStatus", out var on) && Str(on, "oldnew") is { } o) d["oldNew"] = o;
                break;
            case "classify":
                if (review.Confidence is { } c) d["confidence"] = Math.Round(c, 2);
                break;
            case "index":
                if (Int(root, "chunks") is { } ch) d["chunks"] = ch;
                if (Str(root, "dense") is { } dense) d["dense"] = dense;
                break;
            case "analyze":
                if (Int(root, "toolCalls") is { } tc) d["toolCalls"] = tc;
                if (root is { } r1 && r1.TryGetProperty("retrieval", out var r) && r.ValueKind == JsonValueKind.Object)
                {
                    var st = Str(r, "status");
                    d["retrievalMode"] = st is "no_index" or "fallback_all" ? st : Str(r, "mode")?.ToLowerInvariant();
                    if (Int(r, "sent") is { } sent) d["sent"] = sent;
                    if (r.TryGetProperty("denseUsed", out var du) && du.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        d["denseUsed"] = du.GetBoolean();
                }
                d["insufficient"] = s.Status == StepStatus.NeedsReview;
                break;
            default:
                if (latestDraft is not null)
                {
                    d["findings"] = findingTiers.Count(t => t == def.Tier);
                    // 변호사가 고친 초안은 승인 때 다시 검사한 결과다(approval-guard).
                    if (latestDraft.CreatedBy == "lawyer") d["recheckedOnApprove"] = true;
                }
                break;
        }
        return d;
    }

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string? Str(JsonElement? e, string name) =>
        e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement? e, string name) =>
        e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
            ? i : null;
}
