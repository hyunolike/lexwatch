using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Governance;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Ops;

/// <summary>GET /api/ops/summary(ops-visibility OV-10). 관리자만. 재현 실행의 단계, 그룹, 결정은 뺀다.</summary>
public static class OpsEndpoints
{
    public static readonly int[] AllowedDays = [7, 14, 30];

    public static void MapOpsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/ops").AddEndpointFilter(async (ctx, next) =>
        {
            if (ctx.HttpContext.Request.Headers["X-Demo-Role"].ToString() != DemoRoles.Admin)
                return Results.Json(new { code = "FORBIDDEN", message = "운영 현황은 관리자만 볼 수 있다" }, statusCode: 403);
            return await next(ctx);
        });

        // days는 문자열로 받아 직접 해석한다(숫자가 아니어도 같은 400을 낸다).
        api.MapGet("/summary", async (HttpContext http, LexWatchDb db, TimeProvider time, CancellationToken ct) =>
        {
            var raw = http.Request.Query["days"].ToString();
            var days = raw == "" ? 14 : int.TryParse(raw, out var d) ? d : -1;
            if (!AllowedDays.Contains(days))
                return Results.Json(new { code = "INVALID_DAYS", message = "days는 7, 14, 30 중 하나다" }, statusCode: 400);

            var today = OpsSummary.KstDay(time.GetUtcNow());
            var fromDay = today.AddDays(-(days - 1));
            var from = OpsSummary.KstMidnight(fromDay);
            // 결정까지 걸린 시간과 다시 실행 판정에 기간 전 기록도 필요해 실행 기준으로 넉넉히(기간 + 30일) 읽는다.
            var lookback = from.AddDays(-30);

            var runs = await db.WorkflowRuns.AsNoTracking().Where(r => !r.IsReplay && r.CreatedAt >= lookback).ToListAsync(ct);
            var runIds = runs.Select(r => r.Id).ToList();
            var steps = await db.StepRuns.AsNoTracking().Where(s => runIds.Contains(s.RunId)).ToListAsync(ct);
            var groups = await db.GroupReviews.AsNoTracking().Where(g => runIds.Contains(g.RunId)).ToListAsync(ct);
            var groupIds = groups.Select(g => g.Id).ToList();
            var drafts = await db.MemoDrafts.AsNoTracking().Where(m => groupIds.Contains(m.GroupReviewId) && m.CreatedBy == "agent").ToListAsync(ct);
            var draftIds = drafts.Select(m => m.Id).ToList();
            var findings = await db.CriticFindings.AsNoTracking().Where(f => draftIds.Contains(f.MemoDraftId)).ToListAsync(ct);
            var approvals = await db.Approvals.AsNoTracking().Where(a => groupIds.Contains(a.GroupReviewId)).ToListAsync(ct);
            // 채팅 기록은 외래키가 없어 재현 여부를 가릴 수 없다. 모두 센다.
            var chat = await db.AiUsageLogs.AsNoTracking().Where(l => l.At >= from).ToListAsync(ct);
            var collect = await db.CollectLogs.AsNoTracking().Where(c => c.Day >= fromDay).ToListAsync(ct);
            var bills = await db.BillCollectLogs.AsNoTracking().Where(l => l.StartedAt >= lookback).ToListAsync(ct);
            var waiting = await db.GroupReviews.AsNoTracking()
                .Where(g => g.Status == ReviewStatus.NeedsReview || g.Status == ReviewStatus.WaitingApproval || g.Status == ReviewStatus.AwaitingPartner)
                .Join(db.WorkflowRuns.Where(r => !r.IsReplay), g => g.RunId, r => r.Id, (g, _) => g.Id).CountAsync(ct);

            return Results.Ok(OpsSummary.Build(
                new OpsInput(runs, steps, groups, drafts, findings, approvals, chat, collect, bills, waiting), fromDay, today));
        });
    }
}
