using System.Text.Json;
using LexWatch.Api.Ai;
using LexWatch.Api.Critic;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Governance;
using LexWatch.Api.Workflow;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Reviews;

public record EditedSentence(string Sentence, List<string>? Citations);
public record ApproveRequest(uint Version, List<EditedSentence>? EditedSentences, string? Comment, int? AcknowledgedFindingCount = null);
public record RejectRequest(uint Version, string? Comment);
public record ConfirmRequest(uint Version, string? Comment);
public record ChatTurnInput(string? Role, string? Content);
public record ChatRequest(string? Question, List<ChatTurnInput>? History);

public static class ReviewEndpoints
{
    private static readonly ReviewStatus[] DefaultStatuses = [ReviewStatus.WaitingApproval, ReviewStatus.NeedsReview, ReviewStatus.AwaitingPartner];
    private static readonly JsonSerializerOptions J = AiMapping.Json;

    private static IResult Err(int status, string code, string message) =>
        Results.Json(new { code, message }, statusCode: status);

    private static IResult NoDraft() =>
        Err(409, "NO_DRAFT", "초안이 없어 승인하거나 수정할 수 없다. 반려만 할 수 있다");

    private static IResult Conflict() => Err(409, "REVIEW_ALREADY_DECIDED", "이미 결정됐거나 다른 사용자가 먼저 처리했다");

    private static IResult InProgress() =>
        Err(409, "ANALYSIS_IN_PROGRESS", "이 그룹의 분석이나 검사가 아직 끝나지 않았다. 끝난 뒤에 결정할 수 있다");

    private static readonly StepStatus[] ActiveSteps = [StepStatus.Pending, StepStatus.Running, StepStatus.RetryScheduled];

    /// <summary>그룹 단계가 남아 있으면 결정하지 않는다. 단계가 끝나며 상태를 덮어써 결정이 사라지지 않게 한다(GV-13).</summary>
    private static Task<bool> HasActiveStepsAsync(LexWatchDb db, Guid groupId, CancellationToken ct) =>
        db.StepRuns.AnyAsync(s => s.GroupReviewId == groupId && ActiveSteps.Contains(s.Status), ct);

    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").AddEndpointFilter(async (ctx, next) =>
        {
            var role = ctx.HttpContext.Request.Headers["X-Demo-Role"].ToString();
            if (!DemoRoles.IsKnown(role))
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
            summary["evidence"] = evidence.Select(e => new
            {
                id = e.EvidenceId, kind = e.Kind, title = e.Title, sourceUrl = e.SourceUrl, excerpt = Excerpt(e),
            });
            summary["version"] = review.Version;
            summary["lawyerApproval"] = null;
            if (review.Status == ReviewStatus.AwaitingPartner)
            {
                var a = await db.Approvals.AsNoTracking().Where(x => x.GroupReviewId == id && x.Decision == "approve")
                    .OrderByDescending(x => x.DecidedAt).FirstOrDefaultAsync(ct);
                if (a is not null)
                    summary["lawyerApproval"] = new
                    {
                        decidedAt = a.DecidedAt, acknowledgedFindingCount = a.AcknowledgedFindingCount ?? 0,
                        findings = a.FindingsJson is null ? [] : JsonSerializer.Deserialize<List<DecisionFindingDto>>(a.FindingsJson, J) ?? [],
                    };
            }
            // 이 초안이 만들어진 과정(ai-footprints AF-01~04)
            var steps = await db.StepRuns.AsNoTracking()
                .Where(x => x.RunId == review.RunId && (x.GroupReviewId == null || x.GroupReviewId == id)).ToListAsync(ct);
            var approvals = await db.Approvals.AsNoTracking().Where(x => x.GroupReviewId == id).ToListAsync(ct);
            var tiers = draft is null ? []
                : await db.CriticFindings.AsNoTracking().Where(f => f.MemoDraftId == draft.Id).Select(f => f.Tier).ToListAsync(ct);
            summary["aiTrail"] = new
            {
                stages = AiTrail.Build(review, steps, approvals, draft, tiers).Select(x => new
                {
                    key = x.Key, actor = x.Actor, status = x.Status, providerKind = x.ProviderKind, providerName = x.ProviderName,
                    detail = x.Detail,
                }),
            };
            return Results.Ok(summary);
        });

        api.MapPost("/reviews/{id:guid}/approve", async (Guid id, ApproveRequest req, HttpContext http, LexWatchDb db,
            TimeProvider time, CancellationToken ct) =>
        {
            if (Role(http) != "lawyer") return Err(403, "FORBIDDEN", "승인은 변호사만 할 수 있다");
            var review = await db.GroupReviews.SingleOrDefaultAsync(g => g.Id == id, ct);
            if (review is null) return Err(404, "NOT_FOUND", "검토를 찾을 수 없다");
            if (!Decidable(review) || review.Version != req.Version) return Conflict();
            if (await HasActiveStepsAsync(db, id, ct)) return InProgress();
            var current = await LatestDraftAsync(db, id, ct);
            if (current is null) return NoDraft();

            var before = JsonSerializer.Deserialize<List<MemoSentenceDto>>(current.SentencesJson, J) ?? [];
            var evidence = await db.Evidence.AsNoTracking().Where(e => e.RunId == review.RunId).ToListAsync(ct);
            var after = before;
            string? diffJson = null;
            if (req.EditedSentences is { Count: > 0 } edited)
            {
                var evidenceIds = evidence.Select(e => e.EvidenceId).ToHashSet();
                if (edited.Any(e => string.IsNullOrWhiteSpace(e.Sentence) || e.Citations is not { Count: > 0 }
                        || e.Citations.Any(c => !evidenceIds.Contains(c))))
                    return Err(400, "INVALID_CITATION", "수정한 문장마다 근거 저장소에 있는 인용이 하나 이상 필요하다");
                after = edited.Select(e => new MemoSentenceDto(e.Sentence, e.Citations!)).ToList();
                var diff = new List<object>();
                for (var i = 0; i < Math.Max(before.Count, after.Count); i++)
                {
                    var b = i < before.Count ? before[i] : null;
                    var a = i < after.Count ? after[i] : null;
                    if (b is null || a is null || b.Sentence != a.Sentence || !b.Citations.SequenceEqual(a.Citations))
                        diff.Add(new { index = i, before = b, after = a });
                }
                diffJson = JsonSerializer.Serialize(diff, J);
            }

            // 승인 직전 재검사: 1·2단은 다시 돌리고 3단은 옮긴다. 적발이 있으면 변호사가 본 적발 수와 맞아야 승인한다.
            var run = await db.WorkflowRuns.AsNoTracking().SingleAsync(r => r.Id == review.RunId, ct);
            var change = await db.LawChanges.AsNoTracking().SingleAsync(l => l.Id == run.LawChangeId, ct);
            var tier3 = await db.CriticFindings.AsNoTracking().Where(f => f.MemoDraftId == current.Id && f.Tier == 3).ToListAsync(ct);
            var findings = DraftRecheck.Run(before, after, tier3, evidence, change);
            if (findings.Count > 0 && req.AcknowledgedFindingCount != findings.Count)
                return Results.Json(new
                {
                    code = "UNRESOLVED_FINDINGS",
                    message = $"확인이 필요한 적발 {findings.Count}건이 있다. 확인한 적발 수를 함께 보내야 승인할 수 있다",
                    findings = findings.Select(f => new { sentenceIndex = f.SentenceIndex, tier = f.Tier, ruleId = f.RuleId, message = f.Message }),
                }, statusCode: StatusCodes.Status422UnprocessableEntity);

            if (diffJson is not null)
            {
                var draft = new MemoDraft
                {
                    Id = Guid.NewGuid(), GroupReviewId = id, Version = current.Version + 1, CreatedBy = "lawyer",
                    SentencesJson = JsonSerializer.Serialize(after, J),
                    OpenQuestionsJson = current.OpenQuestionsJson,
                };
                db.MemoDrafts.Add(draft);
                foreach (var f in findings)
                    db.CriticFindings.Add(new CriticFinding
                    {
                        Id = Guid.NewGuid(), MemoDraftId = draft.Id, Tier = f.Tier, SentenceIndex = f.SentenceIndex,
                        RuleId = f.RuleId, Message = f.Message,
                    });
            }

            // 교차검토(GV-07): 적발을 확인하고 승인했거나 파트너가 돌려보낸 적이 있으면 파트너 최종 승인을 기다린다.
            var returned = await db.Approvals.AnyAsync(a => a.GroupReviewId == id && a.Decision == "return", ct);
            var needsPartner = findings.Count > 0 || returned;
            var now = time.GetUtcNow();
            review.Status = needsPartner ? ReviewStatus.AwaitingPartner : ReviewStatus.Approved;
            db.Approvals.Add(new Approval
            {
                Id = Guid.NewGuid(), GroupReviewId = id, Decision = "approve", ActorRole = DemoRoles.Lawyer,
                Comment = req.Comment, DiffJson = diffJson, DecidedAt = now, AcknowledgedFindingCount = findings.Count,
                FindingsJson = findings.Count == 0 ? null : JsonSerializer.Serialize(findings.Select(f =>
                    new DecisionFindingDto(f.SentenceIndex, f.Tier, f.RuleId, f.Message)), J),
            });
            if (!needsPartner) db.StepRuns.Add(StepQueue.Create(review.RunId, new NextStep("notify", id, null), now));
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
            if (await HasActiveStepsAsync(db, id, ct)) return InProgress();

            var now = time.GetUtcNow();
            review.Status = ReviewStatus.Rejected;
            db.Approvals.Add(new Approval
            {
                Id = Guid.NewGuid(), GroupReviewId = id, Decision = "reject", ActorRole = "lawyer",
                Comment = req.Comment, DecidedAt = now,
            });
            return await SaveDecisionAsync(db, review, req.Version, now, ct);
        });

        // 파트너 최종 승인(GV-08). 변호사가 적발을 확인하고 승인한 초안만 이 단계를 거친다.
        api.MapPost("/reviews/{id:guid}/confirm", async (Guid id, ConfirmRequest req, HttpContext http, LexWatchDb db,
            TimeProvider time, CancellationToken ct) =>
        {
            if (Role(http) != DemoRoles.Partner) return Err(403, "FORBIDDEN", "최종 승인은 파트너만 할 수 있다");
            var review = await db.GroupReviews.SingleOrDefaultAsync(g => g.Id == id, ct);
            if (review is null) return Err(404, "NOT_FOUND", "검토를 찾을 수 없다");
            if (review.Status != ReviewStatus.AwaitingPartner || review.Version != req.Version) return Conflict();
            if (await HasActiveStepsAsync(db, id, ct)) return InProgress();

            var now = time.GetUtcNow();
            review.Status = ReviewStatus.Approved;
            db.Approvals.Add(new Approval
            {
                Id = Guid.NewGuid(), GroupReviewId = id, Decision = "confirm", ActorRole = DemoRoles.Partner,
                Comment = req.Comment, DecidedAt = now,
            });
            db.StepRuns.Add(StepQueue.Create(review.RunId, new NextStep("notify", id, null), now));
            return await SaveDecisionAsync(db, review, req.Version, now, ct);
        });

        // 파트너가 돌려보낸다(GV-09). 변호사가 다시 결정하고, 그 승인은 다시 파트너를 거친다.
        api.MapPost("/reviews/{id:guid}/return", async (Guid id, RejectRequest req, HttpContext http, LexWatchDb db,
            TimeProvider time, CancellationToken ct) =>
        {
            if (Role(http) != DemoRoles.Partner) return Err(403, "FORBIDDEN", "돌려보내기는 파트너만 할 수 있다");
            if (string.IsNullOrWhiteSpace(req.Comment)) return Err(400, "COMMENT_REQUIRED", "돌려보내는 사유가 필요하다");
            var review = await db.GroupReviews.SingleOrDefaultAsync(g => g.Id == id, ct);
            if (review is null) return Err(404, "NOT_FOUND", "검토를 찾을 수 없다");
            if (review.Status != ReviewStatus.AwaitingPartner || review.Version != req.Version) return Conflict();
            if (await HasActiveStepsAsync(db, id, ct)) return InProgress();

            var now = time.GetUtcNow();
            review.Status = ReviewStatus.NeedsReview;
            db.Approvals.Add(new Approval
            {
                Id = Guid.NewGuid(), GroupReviewId = id, Decision = "return", ActorRole = DemoRoles.Partner,
                Comment = req.Comment, DecidedAt = now,
            });
            return await SaveDecisionAsync(db, review, req.Version, now, ct);
        });

        // 검토 보조 채팅: 저장하지 않는다. IP·질문은 기록하지 않는다(해시만 할당량 표에 남는다).
        // 민감정보는 AI 서비스로 보내기 전에 여기서 가리고, 메타데이터만 ai_usage_logs에 남긴다(ai-governance GV-03, GV-05).
        api.MapPost("/reviews/{id:guid}/chat", async (Guid id, ChatRequest req, HttpContext http, LexWatchDb db,
            AiGateway ai, IConfiguration cfg, TimeProvider time, GovernanceOptions gov, ILoggerFactory logs, CancellationToken ct) =>
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
            // 원문 전체를 가린 뒤 AI 계약 한도로 자른다. 자르고 가리면 경계에 걸린 번호 앞부분이 샌다.
            var (maskedFull, hits) = SensitiveDataMasker.Mask(question, gov.ClientNames);
            var maskedQuestion = SensitiveDataMasker.Clip(maskedFull, 500);
            var usage = new AiUsageLog
            {
                Id = Guid.NewGuid(), At = time.GetUtcNow(), Purpose = "review_chat", GroupReviewId = id,
                ActorRole = DemoRoles.Lawyer, Outcome = AiUsage.Failed, MaskedJson = AiUsage.MaskedJson(hits),
            };
            var started = time.GetTimestamp();
            var answered = false;
            try
            {
                var evidence = await db.Evidence.AsNoTracking().Where(e => e.RunId == review.RunId).ToListAsync(ct);
                // 기록은 이전 질문에서 이미 센 것이므로 가리기만 하고 건수에는 넣지 않는다(GV-04).
                var history = (req.History ?? []).Where(h => (h.Role is "user" or "assistant") && !string.IsNullOrWhiteSpace(h.Content))
                    .TakeLast(6).Select(h => new ChatTurn
                    {
                        Role = h.Role == "user" ? ChatTurnRole.User : ChatTurnRole.Assistant,
                        // 8000자로 먼저 줄이는 것은 정규식 비용 상한일 뿐이고, 결과는 그보다 훨씬 앞인 2000자에서 자른다.
                        Content = SensitiveDataMasker.Clip(h.Role == "user"
                            ? SensitiveDataMasker.Mask(h.Content![..Math.Min(h.Content!.Length, 8000)], gov.ClientNames).Masked
                            : h.Content!, 2000),
                    }).ToList();
                started = time.GetTimestamp();
                var chatReq = new ReviewChatRequest
                {
                    Run_id = review.RunId.ToString(), Question = maskedQuestion,
                    Evidence = AiMapping.Evidence(evidence), History = history,
                };
                // AI 서비스가 잠들어 있으면 깨는 동안 5xx나 연결 실패가 몇 초 만에 돌아온다. 마감 안에서 다시 시도한다.
                var outcome = await ChatRetry.RunAsync(
                    (attempt, timeout) => ai.CallAsync((c, t) => c.ReviewChatAsync(chatReq, t), attempt, timeout, ct),
                    ChatRetry.Options.From(cfg), time, ct);
                if (outcome.Failure is not null)
                    return Err(424, "AI_UNAVAILABLE", "검토 보조를 지금 쓸 수 없다");
                answered = true;
                var r = outcome.Value!;
                usage.ProviderKind = AiMapping.ProviderKind(r.Provider);
                usage.ProviderName = r.Provider.Name;
                usage.Outcome = r.Provider.Name == "no-key" ? AiUsage.Unavailable : r.Refused ? AiUsage.Refused : AiUsage.Answered;
                return Results.Ok(new
                {
                    answer = r.Answer, citations = r.Citations, refused = r.Refused,
                    provider = new { name = r.Provider.Name, kind = AiMapping.ProviderKind(r.Provider) },
                    maskedQuestion,
                    masked = hits.Select(h => new { kind = h.Kind, label = h.Label, count = h.Count }),
                });
            }
            finally
            {
                if (!answered) await ChatQuotaService.RefundAsync(db, day, ipHash);
                usage.DurationMs = (int)time.GetElapsedTime(started).TotalMilliseconds;
                await AiUsage.TryLogAsync(db, usage, logs.CreateLogger("LexWatch.Governance"));
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

    private sealed record DecisionFindingDto(int SentenceIndex, int Tier, string RuleId, string Message);

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
        var (before, after) = SplitOldNew(e.Body);
        int[][]? oldMarks = null, newMarks = null;
        if (e.ChangeMarksJson is { } json)
        {
            var m = JsonSerializer.Deserialize<ChangeMarks>(json, J);
            oldMarks = m?.Old;
            newMarks = m?.New;
        }
        return new
        {
            articleNo, before, after,
            beforeMissing = before == Missing, afterMissing = after == Missing,
            oldMarks, newMarks,
        };
    }

    private const string Missing = "(없음)";
    private const int ExcerptLength = 240;

    private sealed record ChangeMarks(int[][]? Old, int[][]? New);

    /// <summary>oldnew 근거 본문 "[구조문]\n…\n\n[신조문]\n…"을 나눈다. 형식이 아니면 둘 다 빈 문자열.</summary>
    private static (string Before, string After) SplitOldNew(string body)
    {
        var oldMark = body.IndexOf("[구조문]\n", StringComparison.Ordinal);
        var newMark = body.IndexOf("\n\n[신조문]\n", StringComparison.Ordinal);
        if (oldMark < 0 || newMark <= oldMark) return ("", "");
        return (body[(oldMark + "[구조문]\n".Length)..newMark], body[(newMark + "\n\n[신조문]\n".Length)..]);
    }

    /// <summary>근거 본문 앞부분. oldnew는 신조문에서, 신조문이 없으면(삭제 조문) 구조문에서 자른다.</summary>
    private static string Excerpt(LexWatch.Api.Data.Entities.Evidence e)
    {
        var text = e.Body;
        if (e.Kind == "oldnew")
        {
            var (before, after) = SplitOldNew(e.Body);
            if (before != "" || after != "") text = after != Missing ? after : before;
        }
        text = text.Trim();
        return text.Length <= ExcerptLength ? text : text[..ExcerptLength] + "…";
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
