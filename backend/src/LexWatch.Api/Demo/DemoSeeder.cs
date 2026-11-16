using System.Reflection;
using System.Text.Json;
using LexWatch.Api.Ai;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Demo;

/// <summary>
/// 데모 데이터: 실습 그룹 복원, 재현 실행 시드, 일일 초기화.
/// 임베딩은 다루지 않는다(ClassifyStep이 필요할 때 채운다). AI 서비스가 꺼져 있어도 초기화가 성공해야 한다.
/// </summary>
public class DemoSeeder(LexWatchDb db, TimeProvider time)
{
    public const string KeyPrefix = "demo-replay:";
    private const long LockKey = 7_461_001;
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    private static readonly (string Code, string Name, string Interest)[] Groups =
    [
        ("labor", "노동", "근로기준·임금·근로시간·산업안전 관련 법령 개정에 관심이 있다."),
        ("privacy", "개인정보", "개인정보 처리·정보통신망·위치정보 관련 법령 개정에 관심이 있다."),
        ("finance", "금융", "금융소비자 보호·자본시장·전자금융 관련 법령 개정에 관심이 있다."),
    ];

    private static readonly ReviewStatus[] Decided = [ReviewStatus.Approved, ReviewStatus.Rejected, ReviewStatus.Notified];

    /// <summary>결정·알림·채팅 할당량·재현 실행을 지우고 재현 실행을 다시 넣는다. 실제 실행 이력은 지우지 않는다.</summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);

        await DeleteReplaysAsync(ct);
        await db.Notifications.ExecuteDeleteAsync(ct);
        await db.Approvals.ExecuteDeleteAsync(ct);
        await db.ChatQuotas.ExecuteDeleteAsync(ct);
        // 변호사가 수정해 만든 초안 버전은 결정의 일부다.
        var edited = db.MemoDrafts.Where(d => d.CreatedBy == "lawyer").Select(d => d.Id);
        await db.CriticFindings.Where(f => edited.Contains(f.MemoDraftId)).ExecuteDeleteAsync(ct);
        await db.MemoDrafts.Where(d => d.CreatedBy == "lawyer").ExecuteDeleteAsync(ct);

        await ReopenRealReviewsAsync(ct);
        await EnsureGroupsAsync(ct);
        await InsertReplaysAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>재현 실행이 하나도 없을 때만 넣는다(새 배포 직후). 승인 등 다른 데이터는 건드리지 않는다.</summary>
    public async Task<bool> SeedIfEmptyAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        if (await db.WorkflowRuns.AnyAsync(r => r.IsReplay, ct)) return false;
        await EnsureGroupsAsync(ct);
        await InsertReplaysAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private Task LockAsync(CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({LockKey})", ct);

    private async Task DeleteReplaysAsync(CancellationToken ct)
    {
        var runs = db.WorkflowRuns.Where(r => r.IsReplay).Select(r => r.Id);
        var groups = db.GroupReviews.Where(g => runs.Contains(g.RunId)).Select(g => g.Id);
        var drafts = db.MemoDrafts.Where(d => groups.Contains(d.GroupReviewId)).Select(d => d.Id);
        var steps = db.StepRuns.Where(s => runs.Contains(s.RunId)).Select(s => s.Id);
        var laws = db.WorkflowRuns.Where(r => r.IsReplay).Select(r => r.LawChangeId);

        await db.CriticFindings.Where(f => drafts.Contains(f.MemoDraftId)).ExecuteDeleteAsync(ct);
        await db.MemoDrafts.Where(d => groups.Contains(d.GroupReviewId)).ExecuteDeleteAsync(ct);
        await db.Approvals.Where(a => groups.Contains(a.GroupReviewId)).ExecuteDeleteAsync(ct);
        await db.Notifications.Where(n => n.GroupReviewId != null && groups.Contains(n.GroupReviewId.Value)).ExecuteDeleteAsync(ct);
        await db.AgentToolCalls.Where(t => steps.Contains(t.StepRunId)).ExecuteDeleteAsync(ct);
        await db.StepRuns.Where(s => runs.Contains(s.RunId)).ExecuteDeleteAsync(ct);
        await db.Evidence.Where(e => runs.Contains(e.RunId)).ExecuteDeleteAsync(ct);
        await db.GroupReviews.Where(g => runs.Contains(g.RunId)).ExecuteDeleteAsync(ct);
        var lawIds = await laws.ToListAsync(ct);
        await db.WorkflowRuns.Where(r => r.IsReplay).ExecuteDeleteAsync(ct);
        await db.LawChanges.Where(l => lawIds.Contains(l.Id) && l.IdempotencyKey.StartsWith(KeyPrefix)).ExecuteDeleteAsync(ct);
    }

    /// <summary>결정된 실제 그룹을 초기 대기 상태로 되돌리고, 그 결정이 만든 대기 중 알림 단계를 치운다.</summary>
    private async Task ReopenRealReviewsAsync(CancellationToken ct)
    {
        var real = db.WorkflowRuns.Where(r => !r.IsReplay).Select(r => r.Id);
        var decided = db.GroupReviews.Where(g => real.Contains(g.RunId) && Decided.Contains(g.Status));
        var decidedIds = decided.Select(g => g.Id);
        var decidedRuns = await decided.Select(g => g.RunId).Distinct().ToListAsync(ct);

        // 이력(성공·실패 단계)은 남기고 아직 끝나지 않은 알림 단계(대기·재시도·실행 중)만 치운다. 실행 중인 행을 지우면 워커의 결과 쓰기가 소유권 확인에서 걸러져 되돌려진다. 안 치우면 초기화 직후 알림이 다시 나간다.
        await db.StepRuns.Where(s => s.Step == "notify" && s.GroupReviewId != null && decidedIds.Contains(s.GroupReviewId.Value)
            && (s.Status == StepStatus.Pending || s.Status == StepStatus.RetryScheduled || s.Status == StepStatus.Running))
            .ExecuteDeleteAsync(ct);
        await decided.ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, ReviewStatus.WaitingApproval), ct);
        await db.WorkflowRuns.Where(r => decidedRuns.Contains(r.Id) && r.Status == RunStatus.Completed)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RunStatus.InReview)
                .SetProperty(r => r.FinishedAt, (DateTimeOffset?)null), ct);
    }

    private async Task EnsureGroupsAsync(CancellationToken ct)
    {
        var existing = await db.PracticeGroups.ToDictionaryAsync(p => p.Code, ct);
        foreach (var (code, name, interest) in Groups)
        {
            if (!existing.TryGetValue(code, out var g))
                db.PracticeGroups.Add(new PracticeGroup { Code = code, Name = name, InterestText = interest });
            else if (string.IsNullOrWhiteSpace(g.InterestText))
                g.InterestText = interest;
        }
        await db.SaveChangesAsync(ct);
    }

    public static IReadOnlyList<ReplayFile> LoadReplays()
    {
        var asm = typeof(DemoSeeder).Assembly;
        return asm.GetManifestResourceNames().Where(n => n.StartsWith("replay/", StringComparison.Ordinal)).Order()
            .Select(n =>
            {
                using var s = asm.GetManifestResourceStream(n)!;
                return JsonSerializer.Deserialize<ReplayFile>(s, J)!;
            }).ToList();
    }

    private async Task InsertReplaysAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var f in LoadReplays())
        {
            var start = now - TimeSpan.FromMinutes(f.AgoMinutes);
            var law = new LawChange
            {
                Id = Guid.NewGuid(), LawId = f.Law.LawId, LawName = f.Law.LawName, Mst = f.Law.Mst ?? "",
                // 실제 공포일이 아니다. 열이 null을 허용하지 않아 시드 날짜를 넣고, 화면은 재현이면 날짜 대신 안내를 보여준다.
                PromulgationDate = DateOnly.FromDateTime(start.UtcDateTime), EffectiveDate = null,
                RawJson = JsonSerializer.Serialize(new { demo = true, replay = f.Slug, note = f.Note }, J),
                RawSha256 = "replay-" + f.Slug, FetchedAt = start, IdempotencyKey = KeyPrefix + f.Slug,
            };
            var run = new WorkflowRun
            {
                Id = Guid.NewGuid(), LawChangeId = law.Id, Status = Enum.Parse<RunStatus>(f.RunStatus),
                TraceId = "replay-" + f.Slug, IsReplay = true, CreatedAt = start,
                FinishedAt = f.RunStatus == nameof(RunStatus.Completed) ? start + TimeSpan.FromMinutes(6) : null,
            };
            db.AddRange(law, run);
            await db.SaveChangesAsync(ct);   // 외래키 순서를 보장한다.

            foreach (var e in f.Evidence)
                db.Evidence.Add(new Data.Entities.Evidence { RunId = run.Id, EvidenceId = e.Id, Kind = e.Kind, Title = e.Title, Body = e.Body, FetchedAt = start });

            var reviews = new Dictionary<string, GroupReview>();
            foreach (var g in f.Groups)
            {
                var review = new GroupReview
                {
                    Id = Guid.NewGuid(), RunId = run.Id, GroupCode = g.Code,
                    Status = Enum.Parse<ReviewStatus>(g.Status), Confidence = g.Confidence, Reason = g.Reason,
                };
                reviews[g.Code] = review;
                db.GroupReviews.Add(review);
            }
            await db.SaveChangesAsync(ct);

            foreach (var g in f.Groups)
            {
                var review = reviews[g.Code];
                if (g.Draft is { } d)
                {
                    var draft = new MemoDraft
                    {
                        Id = Guid.NewGuid(), GroupReviewId = review.Id, Version = 1, CreatedBy = d.CreatedBy,
                        SentencesJson = JsonSerializer.Serialize(d.Sentences.Select(s => new MemoSentenceDto(s.Sentence, s.Citations)), AiMapping.Json),
                        OpenQuestionsJson = JsonSerializer.Serialize(d.OpenQuestions, AiMapping.Json),
                    };
                    db.MemoDrafts.Add(draft);
                    await db.SaveChangesAsync(ct);
                    foreach (var x in d.Findings)
                        db.CriticFindings.Add(new CriticFinding
                        {
                            Id = Guid.NewGuid(), MemoDraftId = draft.Id, Tier = x.Tier, SentenceIndex = x.SentenceIndex,
                            RuleId = x.RuleId, Message = x.Message,
                        });
                }
                if (g.Approval is { } a)
                    db.Approvals.Add(new Approval
                    {
                        Id = Guid.NewGuid(), GroupReviewId = review.Id, Decision = a.Decision, ActorRole = a.ActorRole,
                        Comment = a.Comment, DecidedAt = start + TimeSpan.FromSeconds(a.AtOffset),
                    });
            }

            foreach (var s in f.Steps)
            {
                var at = start + TimeSpan.FromSeconds(s.At);
                var step = new StepRun
                {
                    Id = Guid.NewGuid(), RunId = run.Id, GroupReviewId = s.Group is null ? null : reviews[s.Group].Id,
                    Step = s.Step, Attempt = s.Attempt, Status = Enum.Parse<StepStatus>(s.Status), NotBefore = at,
                    StartedAt = at, FinishedAt = at + TimeSpan.FromMilliseconds(s.DurationMs), DurationMs = s.DurationMs,
                    OutputJson = s.Output is { ValueKind: not JsonValueKind.Null } o ? o.GetRawText() : null,
                    ErrorCode = s.ErrorCode, ErrorMessage = s.ErrorMessage, ProviderKind = "replay", ProviderName = "재현 기록",
                };
                db.StepRuns.Add(step);
                var seq = 0;
                foreach (var t in s.ToolCalls ?? [])
                    db.AgentToolCalls.Add(new AgentToolCall
                    {
                        Id = Guid.NewGuid(), StepRunId = step.Id, Seq = seq++, Tool = t.Tool, ArgsJson = t.Args.GetRawText(),
                        ResultSummary = t.ResultSummary, EvidenceIds = [.. t.EvidenceIds],
                    });
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
