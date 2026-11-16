using System.Text.Json;
using LexWatch.Api.Ai;
using LexWatch.Api.Critic;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Workflow.Steps;

/// <summary>결정적 크리틱 공통: 초안을 읽고, 이 단의 이전 발견을 비운 뒤 새로 저장하고, 발견이 있으면 NeedsReview로 표시만 한다.</summary>
public abstract class DeterministicCriticStep : IStepHandler
{
    public abstract string Step { get; }
    protected abstract int Tier { get; }
    protected abstract string NextStep { get; }
    protected abstract IReadOnlyList<CriticFindingDto> Run(
        IReadOnlyList<MemoSentenceDto> memo, IReadOnlyList<Data.Entities.Evidence> evidence, LawChange change);

    public async Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        if (ctx.Step.GroupReviewId is not { } groupId)
            return new StepResult.Failed("NO_GROUP_REVIEW", $"{Step} 단계에 그룹 검토 ID가 없다");
        var review = await db.GroupReviews.SingleAsync(g => g.Id == groupId, ct);
        var draft = await db.MemoDrafts.Where(d => d.GroupReviewId == groupId).OrderByDescending(d => d.Version).FirstOrDefaultAsync(ct);
        if (draft is null) return new StepResult.Failed("NO_DRAFT", "검토할 초안이 없다");
        var memo = JsonSerializer.Deserialize<List<MemoSentenceDto>>(draft.SentencesJson, AiMapping.Json) ?? [];
        var evidence = await db.Evidence.AsNoTracking().Where(e => e.RunId == ctx.Run.Id).ToListAsync(ct);
        var change = await db.LawChanges.AsNoTracking().SingleAsync(l => l.Id == ctx.Run.LawChangeId, ct);

        var findings = Run(memo, evidence, change);

        db.CriticFindings.RemoveRange(await db.CriticFindings.Where(f => f.MemoDraftId == draft.Id && f.Tier == Tier).ToListAsync(ct));
        foreach (var f in findings)
            db.CriticFindings.Add(new CriticFinding
            {
                Id = Guid.NewGuid(), MemoDraftId = draft.Id, Tier = Tier, SentenceIndex = f.SentenceIndex,
                RuleId = f.RuleId, Message = f.Message,
            });
        if (findings.Count > 0) review.Status = ReviewStatus.NeedsReview;

        return new StepResult.Succeeded(new { findings = findings.Count, draftVersion = draft.Version },
            [new NextStep(NextStep, groupId, null)], "none");
    }
}

public class CriticCitationStep : DeterministicCriticStep
{
    public override string Step => "critic_citation";
    protected override int Tier => 1;
    protected override string NextStep => "critic_source";
    protected override IReadOnlyList<CriticFindingDto> Run(
        IReadOnlyList<MemoSentenceDto> memo, IReadOnlyList<Data.Entities.Evidence> evidence, LawChange change) =>
        CitationCritic.Check(memo, evidence.Select(e => e.EvidenceId).ToHashSet());
}

public class CriticSourceStep : DeterministicCriticStep
{
    public override string Step => "critic_source";
    protected override int Tier => 2;
    protected override string NextStep => "critic_judge";
    protected override IReadOnlyList<CriticFindingDto> Run(
        IReadOnlyList<MemoSentenceDto> memo, IReadOnlyList<Data.Entities.Evidence> evidence, LawChange change) =>
        SourceMatchCritic.Check(memo, evidence.ToDictionary(e => e.EvidenceId, e => e.Body), change);
}
