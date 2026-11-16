using System.Text.Json;
using LexWatch.Api.Ai;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Reviews;

public record EditedSentence(string Sentence, List<string>? Citations);
public record ApproveRequest(uint Version, List<EditedSentence>? EditedSentences, string? Comment);
public record RejectRequest(uint Version, string? Comment);
public record ChatTurnInput(string? Role, string? Content);
public record ChatRequest(string? Question, List<ChatTurnInput>? History);

public static class ReviewEndpoints
{
    private static readonly ReviewStatus[] DefaultStatuses = [ReviewStatus.WaitingApproval, ReviewStatus.NeedsReview];
    private static readonly JsonSerializerOptions J = AiMapping.Json;

    private static IResult Err(int status, string code, string message) =>
        Results.Json(new { code, message }, statusCode: status);

    private static IResult NoDraft() =>
        Err(409, "NO_DRAFT", "초안이 없어 승인하거나 수정할 수 없다. 반려만 할 수 있다");

    private static IResult Conflict() => Err(409, "REVIEW_ALREADY_DECIDED", "이미 결정됐거나 다른 사용자가 먼저 처리했다");

    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").AddEndpointFilter(async (ctx, next) =>
        {
            var role = ctx.HttpContext.Request.Headers["X-Demo-Role"].ToString();
            if (role is not ("lawyer" or "admin"))
                return Err(403, "FORBIDDEN", "역할 헤더가 없거나 올바르지 않다");
            return await next(ctx);
        });

        api.MapGet("/reviews", async (string? status, LexWatchDb db, CancellationToken ct) =>
        {
            var statuses = DefaultStatuses;
            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<ReviewStatus>(status, true, out var s))
                    return Err(400, "INVALID_STATUS", "알 수 없는 상태");
                statuses = [s];
            }
            var ids = await db.GroupReviews.AsNoTracking().Where(g => statuses.Contains(g.Status))
                .Select(g => g.Id).ToListAsync(ct);
            var list = new List<object>();
            foreach (var id in ids) list.Add((await SummaryAsync(db, id, ct))!);
            return Results.Ok(list);
        });

        api.MapGet("/reviews/{id:guid}", async (Guid id, LexWatchDb db, CancellationToken ct) =>
        {
            var review = await db.GroupReviews.AsNoTracking().SingleOrDefaultAsync(g => g.Id == id, ct);
            if (review is null) return Err(404, "NOT_FOUND", "검토를 찾을 수 없다");
            var summary = (await SummaryAsync(db, id, ct))!;
            var evidence = await db.Evidence.AsNoTracking().Where(e => e.RunId == review.RunId)
                .OrderBy(e => e.EvidenceId).ToListAsync(ct);
            var oldNew = evidence.Where(e => e.Kind == "oldnew").Select(e => ToOldNew(e)).ToList();
            var draft = await LatestDraftAsync(db, id, ct);
            object? draftDto = null;
            if (draft is not null)
            {
                var sentences = JsonSerializer.Deserialize<List<MemoSentenceDto>>(draft.SentencesJson, J) ?? [];
                var findings = await db.CriticFindings.AsNoTracking().Where(f => f.MemoDraftId == draft.Id)
                    .OrderBy(f => f.Tier).ThenBy(f => f.RuleId).ToListAsync(ct);
                draftDto = new
                {
                    version = draft.Version, createdBy = draft.CreatedBy,
                    sentences = sentences.Select((s, i) => new
                    {
                        sentence = s.Sentence, citations = s.Citations,
                        findings = findings.Where(f => f.SentenceIndex == i)
                            .Select(f => new { tier = f.Tier, ruleId = f.RuleId, message = f.Message }),
                    }),
                    openQuestions = JsonSerializer.Deserialize<List<string>>(draft.OpenQuestionsJson, J) ?? [],
                };
            }
            summary["oldNew"] = oldNew;
            summary["draft"] = draftDto;
            summary["evidence"] = evidence.Select(e => new { id = e.EvidenceId, kind = e.Kind, title = e.Title, sourceUrl = e.SourceUrl });
            summary["version"] = review.Version;
            return Results.Ok(summary);
        });

        api.MapPost("/reviews/{id:guid}/approve", async (Guid id, ApproveRequest req, HttpContext http, LexWatchDb db,
            TimeProvider time, CancellationToken ct) =>
        {
            if (Role(http) != "lawyer") return Err(403, "FORBIDDEN", "승인은 변호사만 할 수 있다");
            var review = await db.GroupReviews.SingleOrDefaultAsync(g => g.Id == id, ct);
            if (review is null) return Err(404, "NOT_FOUND", "검토를 찾을 수 없다");
            if (!Decidable(review) || review.Version != req.Version) return Conflict();
            var current = await LatestDraftAsync(db, id, ct);
            if (current is null) return NoDraft();

            string? diffJson = null;
            if (req.EditedSentences is { Count: > 0 } edited)
            {
                var evidenceIds = (await db.Evidence.Where(e => e.RunId == review.RunId).Select(e => e.EvidenceId)
                    .ToListAsync(ct)).ToHashSet();
                if (edited.Any(e => string.IsNullOrWhiteSpace(e.Sentence) || e.Citations is not { Count: > 0 }
                        || e.Citations.Any(c => !evidenceIds.Contains(c))))
                    return Err(400, "INVALID_CITATION", "수정한 문장마다 근거 저장소에 있는 인용이 하나 이상 필요하다");
                var before = JsonSerializer.Deserialize<List<MemoSentenceDto>>(current.SentencesJson, J) ?? [];
                var after = edited.Select(e => new MemoSentenceDto(e.Sentence, e.Citations!)).ToList();
                var diff = new List<object>();
                for (var i = 0; i < Math.Max(before.Count, after.Count); i++)
                {
                    var b = i < before.Count ? before[i] : null;
                    var a = i < after.Count ? after[i] : null;
                    if (b is null || a is null || b.Sentence != a.Sentence || !b.Citations.SequenceEqual(a.Citations))
                        diff.Add(new { index = i, before = b, after = a });
                }
                diffJson = JsonSerializer.Serialize(diff, J);
                db.MemoDrafts.Add(new MemoDraft
                {
                    Id = Guid.NewGuid(), GroupReviewId = id, Version = current.Version + 1, CreatedBy = "lawyer",
                    SentencesJson = JsonSerializer.Serialize(after, J),
                    OpenQuestionsJson = current.OpenQuestionsJson,
                });
            }

            var now = time.GetUtcNow();
            review.Status = ReviewStatus.Approved;
            db.Approvals.Add(new Approval
            {
                Id = Guid.NewGuid(), GroupReviewId = id, Decision = "approve", ActorRole = "lawyer",
                Comment = req.Comment, DiffJson = diffJson, DecidedAt = now,
            });
            db.StepRuns.Add(StepQueue.Create(review.RunId, new NextStep("notify", id, null), now));
            return await SaveDecisionAsync(db, review, req.Version, now, ct);
        });

        api.MapPost("/reviews/{id:guid}/reject", async (Guid id, RejectRequest req, HttpContext http, LexWatchDb db,
            TimeProvider time, CancellationToken ct) =>
        {
            if (Role(http) != "lawyer") return Err(403, "FORBIDDEN", "반려는 변호사만 할 수 있다");
            if (string.IsNullOrWhiteSpace(req.Comment)) return Err(400, "COMMENT_REQUIRED", "반려 사유가 필요하다");
            var review = await db.GroupReviews.SingleOrDefaultAsync(g => g.Id == id, ct);
            if (review is null) return Err(404, "NOT_FOUND", "검토를 찾을 수 없다");
            if (!Decidable(review) || review.Version != req.Version) return Conflict();

            var now = time.GetUtcNow();
            review.Status = ReviewStatus.Rejected;
            db.Approvals.Add(new Approval
            {
                Id = Guid.NewGuid(), GroupReviewId = id, Decision = "reject", ActorRole = "lawyer",
                Comment = req.Comment, DecidedAt = now,
            });
            return await SaveDecisionAsync(db, review, req.Version, now, ct);
        });

        // 검토 보조 채팅: 저장하지 않는다. IP·질문은 기록하지 않는다(해시만 할당량 표에 남는다).
        api.MapPost("/reviews/{id:guid}/chat", async (Guid id, ChatRequest req, HttpContext http, LexWatchDb db,
            AiGateway ai, IConfiguration cfg, TimeProvider time, CancellationToken ct) =>
        {
            if (Role(http) != "lawyer") return Err(403, "FORBIDDEN", "검토 보조는 변호사만 쓸 수 있다");
            var question = req.Question?.Trim();
            if (string.IsNullOrEmpty(question) || question.Length > 500)
                return Err(400, "INVALID_QUESTION", "질문은 1~500자여야 한다");
            var review = await db.GroupReviews.AsNoTracking().SingleOrDefaultAsync(g => g.Id == id, ct);
            if (review is null) return Err(404, "NOT_FOUND", "검토를 찾을 수 없다");

            var jobToken = cfg["JOB_TOKEN"];
            if (string.IsNullOrEmpty(jobToken)) // 빈 솔트로 IP를 해시하지 않는다
                return Err(503, "CHAT_DISABLED", "검토 보조가 이 배포에서 설정되지 않았다");
            var day = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            var ipHash = ChatQuotaService.HashIp(ClientIp(http, cfg), day, jobToken);
            if (!await ChatQuotaService.TryAcquireAsync(db, day, ipHash, ct))
                return Err(429, "CHAT_QUOTA_EXCEEDED", "오늘 사용 한도를 다 썼다");

            // 한도를 올린 뒤 답을 받지 못하면(실패·취소·예외) 어떤 경로든 되돌린다. 답을 받은 거절은 되돌리지 않는다.
            var answered = false;
            try
            {
                var evidence = await db.Evidence.AsNoTracking().Where(e => e.RunId == review.RunId).ToListAsync(ct);
                var history = (req.History ?? []).Where(h => (h.Role is "user" or "assistant") && !string.IsNullOrWhiteSpace(h.Content))
                    .TakeLast(6).Select(h => new ChatTurn
                    {
                        Role = h.Role == "user" ? ChatTurnRole.User : ChatTurnRole.Assistant,
                        Content = (h.Content ?? "")[..Math.Min((h.Content ?? "").Length, 2000)],
                    }).ToList();
                var outcome = await ai.CallAsync((c, t) => c.ReviewChatAsync(new ReviewChatRequest
                {
                    Run_id = review.RunId.ToString(), Question = question,
                    Evidence = AiMapping.Evidence(evidence), History = history,
                }, t), 1, TimeSpan.FromSeconds(40), ct);
                if (outcome.Failure is not null)
                    return Err(424, "AI_UNAVAILABLE", "검토 보조를 지금 쓸 수 없다");
                answered = true;
                var r = outcome.Value!;
                return Results.Ok(new
                {
                    answer = r.Answer, citations = r.Citations, refused = r.Refused,
                    provider = new { name = r.Provider.Name, kind = AiMapping.ProviderKind(r.Provider) },
                });
            }
            finally
            {
                if (!answered) await ChatQuotaService.RefundAsync(db, day, ipHash);
            }
        });

        api.MapGet("/inbox", async (LexWatchDb db, CancellationToken ct) =>
        {
            var rows = await db.Notifications.AsNoTracking().Where(n => n.Channel == "demo_inbox")
                .OrderByDescending(n => n.SentAt).Take(50).ToListAsync(ct);
            var gids = rows.Where(n => n.GroupReviewId != null).Select(n => n.GroupReviewId!.Value).ToList();
            var replayGroups = (await db.GroupReviews.AsNoTracking()
                .Where(g => gids.Contains(g.Id) && db.WorkflowRuns.Any(r => r.Id == g.RunId && r.IsReplay))
                .Select(g => g.Id).ToListAsync(ct)).ToHashSet();
            return Results.Ok(rows.Select(n => new
            {
                id = n.Id, groupReviewId = n.GroupReviewId, channel = n.Channel, status = n.Status, body = n.Body, sentAt = n.SentAt,
                isReplay = n.GroupReviewId is { } g && replayGroups.Contains(g),
            }));
        });
    }

    /// <summary>
    /// 할당량용 클라이언트 주소. 헤더는 위조할 수 있으므로 설정으로 켠 경우에만 믿는다.
    /// 1) CLIENT_IP_HEADER(예: True-Client-IP, CF-Connecting-IP)가 있으면 그 헤더 하나만 읽는다.
    ///    값이 IP로 읽히지 않으면 원격 IP로 내려간다.
    /// 2) 없고 TRUST_FORWARDED_FOR=true면 X-Forwarded-For의 마지막 항목(신뢰하는 프록시가 덧붙인 값)을 쓴다.
    /// 3) 그 밖에는 연결의 원격 IP. 값은 로그로 남기지 않는다(해시만 할당량 표에 남는다).
    /// </summary>
    public static string ClientIp(HttpContext http, IConfiguration cfg)
    {
        var remote = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var header = cfg["CLIENT_IP_HEADER"]?.Trim();
        if (!string.IsNullOrEmpty(header))
        {
            var raw = http.Request.Headers[header].ToString().Trim();
            return System.Net.IPAddress.TryParse(raw, out var ip) && !raw.Contains(',') ? ip.ToString() : remote;
        }
        if (cfg.GetValue<bool>("TRUST_FORWARDED_FOR"))
        {
            var last = http.Request.Headers["X-Forwarded-For"].ToString().Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (!string.IsNullOrEmpty(last)) return last;
        }
        return remote;
    }

    private static string Role(HttpContext http) => http.Request.Headers["X-Demo-Role"].ToString();

    private static bool Decidable(GroupReview r) => r.Status is ReviewStatus.WaitingApproval or ReviewStatus.NeedsReview;

    /// <summary>결정 저장과 실행 종결 판정을 한 트랜잭션으로 묶는다. xmin 충돌이면 전부 되돌린다.</summary>
    private static async Task<IResult> SaveDecisionAsync(LexWatchDb db, GroupReview review, uint version,
        DateTimeOffset now, CancellationToken ct)
    {
        db.Entry(review).Property(x => x.Version).OriginalValue = version;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            var run = await db.WorkflowRuns.SingleAsync(r => r.Id == review.RunId, ct);
            await WorkflowWorker.UpdateRunStatusAsync(db, run, now, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new { id = review.Id, status = review.Status.ToString() });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
    }

    private static Task<MemoDraft?> LatestDraftAsync(LexWatchDb db, Guid id, CancellationToken ct) =>
        db.MemoDrafts.AsNoTracking().Where(d => d.GroupReviewId == id).OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(ct);

    private static object ToOldNew(LexWatch.Api.Data.Entities.Evidence e)
    {
        var parts = e.EvidenceId.Split(':', 3);
        var articleNo = parts.Length == 3 ? parts[2] : e.Title;
        string before = "", after = "";
        var oldMark = e.Body.IndexOf("[구조문]\n", StringComparison.Ordinal);
        var newMark = e.Body.IndexOf("\n\n[신조문]\n", StringComparison.Ordinal);
        if (oldMark >= 0 && newMark > oldMark)
        {
            before = e.Body[(oldMark + "[구조문]\n".Length)..newMark];
            after = e.Body[(newMark + "\n\n[신조문]\n".Length)..];
        }
        return new { articleNo, before, after };
    }

    private static async Task<Dictionary<string, object?>?> SummaryAsync(LexWatchDb db, Guid id, CancellationToken ct)
    {
        var g = await db.GroupReviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return null;
        var run = await db.WorkflowRuns.AsNoTracking().SingleAsync(r => r.Id == g.RunId, ct);
        var law = await db.LawChanges.AsNoTracking().SingleAsync(l => l.Id == run.LawChangeId, ct);
        var groupName = await db.PracticeGroups.AsNoTracking().Where(p => p.Code == g.GroupCode)
            .Select(p => p.Name).FirstOrDefaultAsync(ct) ?? g.GroupCode;
        var draft = await LatestDraftAsync(db, id, ct);
        var findingCount = draft is null ? 0 : await db.CriticFindings.CountAsync(f => f.MemoDraftId == draft.Id, ct);
        var evidenceCount = await db.Evidence.AsNoTracking().CountAsync(e => e.RunId == g.RunId, ct);
        var kinds = await db.StepRuns.AsNoTracking()
            .Where(s => s.RunId == g.RunId && (s.GroupReviewId == id || s.GroupReviewId == null) && s.ProviderKind != "none")
            .Select(s => s.ProviderKind).Distinct().ToListAsync(ct);
        return new Dictionary<string, object?>
        {
            ["id"] = g.Id, ["runId"] = g.RunId, ["groupCode"] = g.GroupCode, ["groupName"] = groupName,
            ["lawName"] = law.LawName, ["promulgationDate"] = law.PromulgationDate, ["status"] = g.Status.ToString(),
            ["confidence"] = g.Confidence, ["findingCount"] = findingCount, ["evidenceCount"] = evidenceCount, ["providerKinds"] = kinds,
            ["isReplay"] = run.IsReplay,
        };
    }
}
