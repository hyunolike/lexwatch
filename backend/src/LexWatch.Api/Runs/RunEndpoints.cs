using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Runs;

public static class RunEndpoints
{
    private static IResult Err(int status, string code, string message) =>
        Results.Json(new { code, message }, statusCode: status);

    private sealed record Item(DateTimeOffset At, int Kind, int Seq, object Body);

    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/runs").AddEndpointFilter(async (ctx, next) =>
        {
            var role = ctx.HttpContext.Request.Headers["X-Demo-Role"].ToString();
            if (role is not ("lawyer" or "admin"))
                return Err(403, "FORBIDDEN", "역할 헤더가 없거나 올바르지 않다");
            return await next(ctx);
        });

        api.MapGet("", async (int? limit, LexWatchDb db, CancellationToken ct) =>
        {
            var take = Math.Clamp(limit ?? 50, 1, 200);
            var runs = await db.WorkflowRuns.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync(ct);
            var runIds = runs.Select(r => r.Id).ToList();
            var laws = await db.LawChanges.AsNoTracking().Where(l => runs.Select(r => r.LawChangeId).Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.LawName, ct);
            var groups = (await db.GroupReviews.AsNoTracking().Where(g => runIds.Contains(g.RunId)).ToListAsync(ct))
                .ToLookup(g => g.RunId);
            return Results.Ok(runs.Select(r => new
            {
                id = r.Id, lawName = laws.GetValueOrDefault(r.LawChangeId, ""), status = r.Status.ToString(),
                isReplay = r.IsReplay, createdAt = r.CreatedAt,
                groupStatuses = groups[r.Id].OrderBy(g => g.GroupCode)
                    .Select(g => new { groupCode = g.GroupCode, status = g.Status.ToString() }),
            }));
        });

        api.MapGet("{id:guid}", async (Guid id, LexWatchDb db, CancellationToken ct) =>
        {
            var run = await db.WorkflowRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
            if (run is null) return Err(404, "NOT_FOUND", "실행을 찾을 수 없다");
            var law = await db.LawChanges.AsNoTracking().SingleAsync(l => l.Id == run.LawChangeId, ct);
            var groups = await db.GroupReviews.AsNoTracking().Where(g => g.RunId == id).ToListAsync(ct);
            var groupCode = groups.ToDictionary(g => g.Id, g => g.GroupCode);
            var groupIds = groups.Select(g => g.Id).ToList();
            var steps = await db.StepRuns.AsNoTracking().Where(s => s.RunId == id).ToListAsync(ct);
            var stepIds = steps.Select(s => s.Id).ToList();
            var stepAt = steps.ToDictionary(s => s.Id, s => s.StartedAt ?? s.NotBefore);
            var items = new List<Item>();

            foreach (var s in steps)
                items.Add(new Item(s.StartedAt ?? s.NotBefore, 0, 0, new
                {
                    kind = "step", at = s.StartedAt ?? s.NotBefore, step = s.Step, attempt = s.Attempt,
                    status = s.Status.ToString(), durationMs = s.DurationMs, errorCode = s.ErrorCode,
                    errorMessage = s.ErrorMessage, providerKind = s.ProviderKind, providerName = s.ProviderName,
                    groupCode = s.GroupReviewId is { } gid ? groupCode.GetValueOrDefault(gid) : null,
                    summary = s.OutputJson is null ? null : s.OutputJson[..Math.Min(300, s.OutputJson.Length)],
                }));

            var calls = await db.AgentToolCalls.AsNoTracking().Where(t => stepIds.Contains(t.StepRunId)).ToListAsync(ct);
            foreach (var t in calls)
            {
                var at = stepAt[t.StepRunId];
                var step = steps.Single(s => s.Id == t.StepRunId);
                items.Add(new Item(at, 1, t.Seq, new
                {
                    kind = "tool_call", at, step = step.Step, attempt = step.Attempt, tool = t.Tool,
                    groupCode = step.GroupReviewId is { } gid ? groupCode.GetValueOrDefault(gid) : null,
                    resultSummary = t.ResultSummary, evidenceIds = t.EvidenceIds,
                }));
            }

            // MemoDraft에는 생성 시각이 없다. 초안을 만든 단계(critique > analyze)의 종료 시각을 쓴다.
            var drafts = await db.MemoDrafts.AsNoTracking().Where(d => groupIds.Contains(d.GroupReviewId)).ToListAsync(ct);
            var draftIds = drafts.Select(d => d.Id).ToList();
            var findings = await db.CriticFindings.AsNoTracking().Where(f => draftIds.Contains(f.MemoDraftId)).ToListAsync(ct);
            var idx = 0;
            foreach (var f in findings.OrderBy(f => f.Tier).ThenBy(f => f.RuleId).ThenBy(f => f.SentenceIndex))
            {
                var d = drafts.Single(x => x.Id == f.MemoDraftId);
                var at = DraftTime(steps, d.GroupReviewId, run.CreatedAt);
                items.Add(new Item(at, 2, idx++, new
                {
                    kind = "finding", at, groupCode = groupCode.GetValueOrDefault(d.GroupReviewId),
                    tier = f.Tier, ruleId = f.RuleId, message = f.Message,
                }));
            }

            foreach (var a in await db.Approvals.AsNoTracking().Where(a => groupIds.Contains(a.GroupReviewId)).ToListAsync(ct))
                items.Add(new Item(a.DecidedAt, 3, 0, new
                {
                    kind = "approval", at = a.DecidedAt, groupCode = groupCode.GetValueOrDefault(a.GroupReviewId),
                    decision = a.Decision, actorRole = a.ActorRole, comment = a.Comment,
                }));

            foreach (var n in await db.Notifications.AsNoTracking()
                         .Where(n => n.GroupReviewId != null && groupIds.Contains(n.GroupReviewId.Value)).ToListAsync(ct))
            {
                var at = n.SentAt ?? run.CreatedAt;
                items.Add(new Item(at, 4, 0, new
                {
                    kind = "notification", at, groupCode = groupCode.GetValueOrDefault(n.GroupReviewId!.Value),
                    status = n.Status, message = n.Body,
                }));
            }

            var timeline = items.OrderBy(i => i.At).ThenBy(i => i.Kind).ThenBy(i => i.Seq).Select(i => i.Body).ToList();
            return Results.Ok(new
            {
                run = new { id = run.Id, status = run.Status.ToString(), isReplay = run.IsReplay, traceId = run.TraceId,
                    createdAt = run.CreatedAt, finishedAt = run.FinishedAt },
                lawChange = new { id = law.Id, lawId = law.LawId, lawName = law.LawName,
                    promulgationDate = law.PromulgationDate, effectiveDate = law.EffectiveDate },
                groups = groups.OrderBy(g => g.GroupCode).Select(g => new { groupCode = g.GroupCode, status = g.Status.ToString() }),
                timeline,
            });
        });

        api.MapPost("{id:guid}/retry", async (Guid id, HttpContext http, LexWatchDb db, TimeProvider time, CancellationToken ct) =>
        {
            if (http.Request.Headers["X-Demo-Role"].ToString() != "admin")
                return Err(403, "FORBIDDEN", "재실행은 관리자만 할 수 있다");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // 실행 행을 잠가 동시 재실행 요청을 직렬화한다. 확인과 삽입이 같은 잠금 안에서 일어난다.
            var run = (await db.WorkflowRuns.FromSqlInterpolated($"""SELECT * FROM "WorkflowRuns" WHERE "Id" = {id} FOR UPDATE""")
                .ToListAsync(ct)).SingleOrDefault();
            if (run is null) return Err(404, "NOT_FOUND", "실행을 찾을 수 없다");
            // 재현 실행에 실제 핸들러(AI, 법제처)를 돌리면 재현과 실제 배지가 한 실행에 섞인다.
            if (run.IsReplay) return Err(409, "REPLAY_RUN", "재현 실행은 다시 실행할 수 없다");
            var steps = await db.StepRuns.Where(s => s.RunId == id).ToListAsync(ct);
            var groups = await db.GroupReviews.Where(g => g.RunId == id).ToDictionaryAsync(g => g.Id, ct);
            // 같은 (단계, 그룹)에 실패가 아닌 행이 이미 있으면 한 번 재큐된 것이므로 다시 넣지 않는다.
            // 이미 결정된 그룹은 결정 기록을 바꾸지 않도록 승인된 그룹의 notify만 허용한다.
            var failed = steps.Where(s => s.Status == StepStatus.Failed
                && !steps.Any(o => o.Status != StepStatus.Failed && o.Step == s.Step && o.GroupReviewId == s.GroupReviewId)
                && Retryable(s, groups))
                .GroupBy(s => (s.Step, s.GroupReviewId)).Select(g => g.First()).ToList();
            if (failed.Count == 0) return Err(409, "NOTHING_TO_RETRY", "다시 실행할 실패 단계가 없다");

            var now = time.GetUtcNow();
            foreach (var s in failed)
            {
                db.StepRuns.Add(new StepRun
                {
                    Id = Guid.NewGuid(), RunId = id, GroupReviewId = s.GroupReviewId, Step = s.Step, Attempt = 0,
                    Status = StepStatus.Pending, NotBefore = now, InputJson = s.InputJson, ProviderKind = "none",
                });
                if (s.GroupReviewId is { } gid && groups[gid].Status is not ReviewStatus.Approved)
                    groups[gid].Status = ReviewStatus.Analyzing;
            }
            run.Status = RunStatus.Running;
            run.FinishedAt = null;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new { id, requeued = failed.Select(s => s.Step).ToList() });
        });
    }

    private static bool Retryable(StepRun s, Dictionary<Guid, GroupReview> groups)
    {
        if (s.GroupReviewId is not { } gid || !groups.TryGetValue(gid, out var g)) return true;
        return g.Status switch
        {
            ReviewStatus.Approved => s.Step == "notify",
            ReviewStatus.Rejected or ReviewStatus.Notified or ReviewStatus.ClosedIrrelevant => false,
            _ => true,
        };
    }

    private static DateTimeOffset DraftTime(List<StepRun> steps, Guid groupId, DateTimeOffset fallback)
    {
        foreach (var name in new[] { "critique", "analyze" })
        {
            var t = steps.Where(s => s.GroupReviewId == groupId && s.Step == name)
                .Select(s => s.FinishedAt ?? s.StartedAt).Where(x => x != null).DefaultIfEmpty(null).Max();
            if (t is { } v) return v;
        }
        return fallback;
    }
}
