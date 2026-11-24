using System.Text;
using System.Text.Json;
using LexWatch.Api.Ai;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Workflow.Steps;

/// <summary>승인된 메모를 데모 알림함에 저장한다. 실제 외부 발송은 하지 않는다. SaveChanges는 워커가 한다.</summary>
public class NotifyStep(TimeProvider? time = null) : IStepHandler
{
    private static readonly TimeZoneInfo Kst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    public string Step => "notify";

    public async Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        if (ctx.Step.GroupReviewId is not { } groupId)
            return new StepResult.Failed("NO_GROUP_REVIEW", "notify 단계에 그룹 검토 ID가 없다");
        var review = await db.GroupReviews.SingleAsync(g => g.Id == groupId, ct);

        // 재시도 시 중복 저장 방지.
        if (!await db.Notifications.AnyAsync(n => n.GroupReviewId == groupId && n.Channel == "demo_inbox", ct))
        {
            var draft = await db.MemoDrafts.Where(d => d.GroupReviewId == groupId).OrderByDescending(d => d.Version)
                .FirstOrDefaultAsync(ct);
            if (draft is null) return new StepResult.Failed("NO_DRAFT", "알릴 초안이 없다");
            var sentences = JsonSerializer.Deserialize<List<MemoSentenceDto>>(draft.SentencesJson, AiMapping.Json) ?? [];
            var group = await db.PracticeGroups.AsNoTracking().SingleOrDefaultAsync(p => p.Code == review.GroupCode, ct);
            var law = await db.LawChanges.AsNoTracking().SingleAsync(l => l.Id == ctx.Run.LawChangeId, ct);
            var decided = await db.Approvals.Where(a => a.GroupReviewId == groupId && a.Decision == "approve")
                .OrderByDescending(a => a.DecidedAt).Select(a => (DateTimeOffset?)a.DecidedAt).FirstOrDefaultAsync(ct)
                ?? (time ?? TimeProvider.System).GetUtcNow();
            var confirmed = await db.Approvals.Where(a => a.GroupReviewId == groupId && a.Decision == "confirm")
                .OrderByDescending(a => a.DecidedAt).Select(a => (DateTimeOffset?)a.DecidedAt).FirstOrDefaultAsync(ct);
            var kst = TimeZoneInfo.ConvertTime(decided, Kst);
            // 교차검토를 거쳤으면 두 결정을 모두 적는다(ai-governance GV-14).
            var signoff = confirmed is { } c
                ? $"\n(승인: 변호사 {kst:yyyy-MM-dd HH:mm}, 파트너 최종 승인 {TimeZoneInfo.ConvertTime(c, Kst):yyyy-MM-dd HH:mm})"
                : $"\n(승인: 변호사, {kst:yyyy-MM-dd HH:mm})";

            var body = new StringBuilder()
                .Append($"[{group?.Name ?? review.GroupCode}] {law.LawName} 개정 안내\n")
                .Append(string.Join("\n", sentences.Select(s => s.Sentence)))
                .Append(signoff)
                .ToString();
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(), GroupReviewId = groupId, Channel = "demo_inbox", Status = "sent", Body = body,
                Attempts = 1, SentAt = (time ?? TimeProvider.System).GetUtcNow(),
            });
        }
        review.Status = ReviewStatus.Notified;
        return new StepResult.Succeeded(new { channel = "demo_inbox" }, [], ctx.Run.IsReplay ? "replay" : "none");
    }
}
