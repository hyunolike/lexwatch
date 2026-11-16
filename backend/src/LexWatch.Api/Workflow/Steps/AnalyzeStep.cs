using System.Text.Json;
using LexWatch.Api.Ai;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using EvidenceRow = LexWatch.Api.Data.Entities.Evidence;

namespace LexWatch.Api.Workflow.Steps;

/// <summary>그룹별 영향 분석 에이전트를 부르고 도구 호출·새 근거·초안 v(n+1)을 저장한다.</summary>
public class AnalyzeStep(AiGateway ai, TimeProvider time) : IStepHandler
{
    public string Step => "analyze";

    public async Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        if (ctx.Step.GroupReviewId is not { } groupId)
            return new StepResult.Failed("NO_GROUP_REVIEW", "analyze 단계에 그룹 검토 ID가 없다");
        var review = await db.GroupReviews.AsNoTracking().SingleAsync(g => g.Id == groupId, ct);
        var group = await db.PracticeGroups.AsNoTracking().SingleAsync(g => g.Code == review.GroupCode, ct);
        var change = await db.LawChanges.AsNoTracking().SingleAsync(l => l.Id == ctx.Run.LawChangeId, ct);
        var evidence = await db.Evidence.Where(e => e.RunId == ctx.Run.Id).ToListAsync(ct);

        var res = await ai.AnalyzeAsync(new AnalyzeRequest
        {
            Run_id = ctx.Run.Id.ToString(),
            Group_code = group.Code,
            Interest_text = group.InterestText,
            Law = AiMapping.Summary(change, change.LawName),
            Evidence = AiMapping.Evidence(evidence),
        }, ctx.Step.Attempt, ct);
        if (res.Failure is not null) return res.Failure;
        var r = res.Value!;

        // --- 여기부터 변경 (AI 호출 성공 뒤에만) ---
        var now = time.GetUtcNow();

        // 새 근거: 이미 있는 ID는 건드리지 않는다.
        var known = evidence.Select(e => e.EvidenceId).ToHashSet();
        foreach (var e in r.New_evidence)
        {
            if (!known.Add(e.Id)) continue;
            db.Evidence.Add(new EvidenceRow
            {
                RunId = ctx.Run.Id, EvidenceId = e.Id, Kind = e.Kind, Title = e.Title, Body = e.Body, FetchedAt = now,
            });
        }

        // 도구 호출: 재시도는 같은 StepRun을 쓰므로 (StepRunId, Seq) 유니크 충돌을 피하려고
        // 이전 시도의 행을 Seq 기준으로 덮어쓰고 남는 행만 지운다.
        var prior = (await db.AgentToolCalls.Where(c => c.StepRunId == ctx.Step.Id).ToListAsync(ct))
            .ToDictionary(c => c.Seq);
        var seqs = new HashSet<int>();
        foreach (var t in r.Tool_calls)
        {
            seqs.Add(t.Seq);
            if (!prior.TryGetValue(t.Seq, out var row))
            {
                row = new AgentToolCall { Id = Guid.NewGuid(), StepRunId = ctx.Step.Id, Seq = t.Seq };
                db.AgentToolCalls.Add(row);
            }
            row.Tool = t.Tool;
            row.ArgsJson = JsonSerializer.Serialize(t.Args, AiMapping.Json);
            row.ResultSummary = t.Result_summary;
            row.EvidenceIds = t.Evidence_ids.ToArray();
        }
        db.AgentToolCalls.RemoveRange(prior.Where(p => !seqs.Contains(p.Key)).Select(p => p.Value));

        var output = new
        {
            toolCalls = r.Tool_calls.Count,
            newEvidence = r.New_evidence.Count,
            status = r.Status.ToString(),
        };

        if (r.Status == AnalyzeResponseStatus.Insufficient_evidence)
            return new StepResult.NeedsReview("INSUFFICIENT_EVIDENCE", output);

        var version = (await db.MemoDrafts.Where(d => d.GroupReviewId == groupId).MaxAsync(d => (int?)d.Version, ct) ?? 0) + 1;
        db.MemoDrafts.Add(new MemoDraft
        {
            Id = Guid.NewGuid(),
            GroupReviewId = groupId,
            Version = version,
            SentencesJson = JsonSerializer.Serialize(
                r.Memo.Select(m => new MemoSentenceDto(m.Sentence, m.Citations.ToArray())), AiMapping.Json),
            OpenQuestionsJson = JsonSerializer.Serialize(r.Open_questions, AiMapping.Json),
            CreatedBy = "agent",
        });

        return new StepResult.Succeeded(new { output.toolCalls, output.newEvidence, draftVersion = version },
            [new NextStep("critic_citation", groupId, null)],
            AiMapping.ProviderKind(r.Provider), r.Provider.Name);
    }
}
