using System.Text.Json;
using LexWatch.Api.Ai;
using LexWatch.Api.Data;
using LexWatch.Api.Retrieval;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using EvidenceRow = LexWatch.Api.Data.Entities.Evidence;

namespace LexWatch.Api.Workflow.Steps;

/// <summary>
/// 그룹별 영향 분석 에이전트를 부르고 도구 호출·새 근거·초안 v(n+1)을 저장한다.
/// 분석에는 근거 전체 대신 변경 조문(최대 12)과 조문 검색 상위 8건을 보낸다(hybrid-rag RAG-04).
/// </summary>
public class AnalyzeStep(AiGateway ai, TimeProvider time, SearchMode mode = SearchMode.Hybrid) : IStepHandler
{
    public const int MaxOldNew = 12;
    public const int SearchCount = 8;
    public static readonly TimeSpan QueryEmbedTimeout = TimeSpan.FromSeconds(10);

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
        var (send, retrieval) = await SelectEvidenceAsync(db, ctx, group, change, evidence, ct);

        var res = await ai.AnalyzeAsync(new AnalyzeRequest
        {
            Run_id = ctx.Run.Id.ToString(),
            Group_code = group.Code,
            Interest_text = group.InterestText,
            Law = AiMapping.Summary(change, change.LawName),
            Evidence = send,
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
            return new StepResult.NeedsReview("INSUFFICIENT_EVIDENCE", output,
                AiMapping.ProviderKind(r.Provider), r.Provider.Name);

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

        return new StepResult.Succeeded(new { output.toolCalls, output.newEvidence, draftVersion = version, retrieval },
            [new NextStep("critic_citation", groupId, null)],
            AiMapping.ProviderKind(r.Provider), r.Provider.Name);
    }

    /// <summary>
    /// 변경 조문 + 검색 상위를 고른다. 색인이 없거나 둘 다 0건이면 지금처럼 근거 전체를 보낸다.
    /// 출력(retrieval)은 타임라인 300자 요약에 들어가도록 조문 번호만 담는다.
    /// </summary>
    private async Task<(List<LexWatch.Api.Ai.Evidence> Send, object Retrieval)> SelectEvidenceAsync(
        LexWatchDb db, StepContext ctx, PracticeGroup group, LawChange change, List<EvidenceRow> evidence, CancellationToken ct)
    {
        var runId = ctx.Run.Id;
        var chunkModel = await db.ArticleChunks.Where(c => c.RunId == runId)
            .Select(c => new { c.EmbedModel }).FirstOrDefaultAsync(ct);
        if (chunkModel is null)
            return (AiMapping.Evidence(evidence), new { status = "no_index", sent = evidence.Count });

        var allOldNew = evidence.Where(e => e.Kind == "oldnew").OrderBy(e => e.EvidenceId, ArticleOrder.Comparer).ToList();
        var oldNew = allOldNew.Take(MaxOldNew).ToList();
        var query = RetrievalQuery.Build(group.InterestText, change.LawName, oldNew);

        // 질의 임베딩: 청크에 임베딩이 있을 때만, 짧은 제한 시간으로. 실패해도 단계는 계속한다(BM25만).
        float[]? vector = null;
        if (mode != SearchMode.Bm25 && chunkModel.EmbedModel is { } model)
        {
            var q = await ai.CallAsync((c, t) => c.EmbedAsync(new EmbedRequest { Texts = [query.Text], Input_type = EmbedRequestInput_type.Query }, t),
                ctx.Step.Attempt, QueryEmbedTimeout, ct);
            if (q.Ok && q.Value!.Provider.Name == model && q.Value.Provider.Kind != ProviderInfoKind.Fallback && q.Value.Vectors.Count == 1)
                vector = q.Value.Vectors.First().Select(x => (float)x).ToArray();
        }

        var changedArticles = oldNew.Select(e => ArticleNumber(e.EvidenceId)).ToHashSet();
        var hits = await HybridSearch.SearchAsync(db, runId, query.Tokens, vector, chunkModel.EmbedModel, mode,
            SearchCount + oldNew.Count, ct);
        var byId = evidence.ToDictionary(e => e.EvidenceId);
        var searched = hits.Where(h => !changedArticles.Contains(ArticleNumber(h.EvidenceId)) && byId.ContainsKey(h.EvidenceId))
            .Take(SearchCount).Select(h => byId[h.EvidenceId]).ToList();

        if (oldNew.Count == 0 && searched.Count == 0)
            return (AiMapping.Evidence(evidence), new { status = "fallback_all", sent = evidence.Count });

        var send = oldNew.Concat(searched).ToList();
        var denseUsed = vector is { Length: LexWatchDb.ChunkEmbedDim } && hits.Any(h => h.DenseRank is not null);
        return (AiMapping.EvidenceInOrder(send), new
        {
            status = "ok", mode = mode.ToString(), sent = send.Count, oldnew = oldNew.Count, searched = searched.Count,
            oldnewTruncated = allOldNew.Count > oldNew.Count, denseUsed,
            top = searched.Take(5).Select(e => ArticleNumber(e.EvidenceId)).ToArray(),
        });
    }

    private static string ArticleNumber(string evidenceId) => evidenceId[(evidenceId.LastIndexOf(':') + 1)..];
}
