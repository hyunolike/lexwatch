using System.Text.Json;
using LexWatch.Api.Ai;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Workflow.Steps;

/// <summary>크리틱 3단: 과잉해석 판정(/critique). 발견은 Tier 3으로 저장한다.</summary>
public class CritiqueStep(AiGateway ai) : IStepHandler
{
    public string Step => "critic_judge";

    public async Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        if (ctx.Step.GroupReviewId is not { } groupId)
            return new StepResult.Failed("NO_GROUP_REVIEW", "critic_judge 단계에 그룹 검토 ID가 없다");
        var review = await db.GroupReviews.SingleAsync(g => g.Id == groupId, ct);
        var draft = await db.MemoDrafts.Where(d => d.GroupReviewId == groupId).OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(ct);
        if (draft is null) return new StepResult.Failed("NO_DRAFT", "검토할 초안이 없다");
        var memo = JsonSerializer.Deserialize<List<MemoSentenceDto>>(draft.SentencesJson, AiMapping.Json) ?? [];
        var evidence = await db.Evidence.AsNoTracking().Where(e => e.RunId == ctx.Run.Id).ToListAsync(ct);

        var res = await ai.CritiqueAsync(new CritiqueRequest
        {
            Run_id = ctx.Run.Id.ToString(),
            Memo = memo.Select(m => new MemoSentence { Sentence = m.Sentence, Citations = m.Citations.ToList() }).ToList(),
            Evidence = AiMapping.Evidence(evidence),
            Use_rules = true,
        }, ctx.Step.Attempt, ct);
        if (res.Failure is not null) return res.Failure;
        var r = res.Value!;

        // 재시도 시 이 초안의 이전 3단 발견을 비운다.
        db.CriticFindings.RemoveRange(await db.CriticFindings.Where(f => f.MemoDraftId == draft.Id && f.Tier == 3).ToListAsync(ct));
        foreach (var f in r.Findings)
            db.CriticFindings.Add(new CriticFinding
            {
                Id = Guid.NewGuid(), MemoDraftId = draft.Id, Tier = 3, SentenceIndex = f.Sentence_index,
                RuleId = f.Rule_id, Message = f.Message,
            });

        // 앞 단계(분류 신뢰도·1·2단)가 이미 NeedsReview로 표시했다면 3단이 깨끗해도 그 표시는 지우지 않는다.
        review.Status = r.Findings.Count > 0 || review.Status == ReviewStatus.NeedsReview
            ? ReviewStatus.NeedsReview
            : ReviewStatus.WaitingApproval;

        return new StepResult.Succeeded(new { findings = r.Findings.Count, draftVersion = draft.Version }, [],
            AiMapping.ProviderKind(r.Provider), r.Provider.Name);
    }
}
