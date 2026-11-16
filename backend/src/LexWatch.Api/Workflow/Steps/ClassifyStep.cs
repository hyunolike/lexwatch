using LexWatch.Api.Ai;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace LexWatch.Api.Workflow.Steps;

/// <summary>개정 요약을 임베딩해 실무 그룹과의 유사도를 구하고 /classify 결과로 그룹 검토를 만든다.</summary>
public class ClassifyStep(AiGateway ai, TimeProvider time) : IStepHandler
{
    public const double ProceedAt = 0.7;
    public const double ReviewAt = 0.4;
    public const int SummaryOldNewChars = 2000;

    public string Step => "classify";

    public async Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var attempt = ctx.Step.Attempt;
        var change = await db.LawChanges.AsNoTracking().SingleAsync(l => l.Id == ctx.Run.LawChangeId, ct);
        var groups = await db.PracticeGroups.OrderBy(g => g.Code).ToListAsync(ct);
        if (groups.Count == 0) return new StepResult.Failed("NO_PRACTICE_GROUPS", "실무 그룹이 없다");

        var summary = await BuildSummaryAsync(db, ctx.Run.Id, change, ct);

        // 1) 질의 벡터
        var q = await ai.EmbedAsync([summary], EmbedRequestInput_type.Query, attempt, ct);
        if (q.Failure is not null) return q.Failure;
        if (q.Value!.Vectors.Count != 1) return new StepResult.Retry("AI_BAD_RESPONSE", "임베딩 개수가 요청과 다르다", null);
        var query = q.Value.Vectors.First().Select(x => (float)x).ToArray();

        // 2) 그룹 벡터. 길이가 질의와 맞는 저장본만 쓰고, 나머지는 지금 만든다.
        var vectors = new Dictionary<string, float[]>();
        var toEmbed = new List<PracticeGroup>();
        foreach (var g in groups)
        {
            if (g.Embedding is { } e && e.ToArray().Length == query.Length) vectors[g.Code] = e.ToArray();
            else toEmbed.Add(g);
        }
        var pendingStore = new List<(PracticeGroup Group, float[] Vector)>();
        if (toEmbed.Count > 0)
        {
            var p = await ai.EmbedAsync(toEmbed.Select(g => g.InterestText), EmbedRequestInput_type.Passage, attempt, ct);
            if (p.Failure is not null) return p.Failure;
            if (p.Value!.Vectors.Count != toEmbed.Count)
                return new StepResult.Retry("AI_BAD_RESPONSE", "임베딩 개수가 요청과 다르다", null);
            for (var i = 0; i < toEmbed.Count; i++)
            {
                var v = p.Value.Vectors.ElementAt(i).Select(x => (float)x).ToArray();
                vectors[toEmbed[i].Code] = v;
                // vector(2048) 열에는 길이가 맞는 것만 저장한다. 대체 임베딩은 이번 호출에서만 쓴다.
                if (v.Length == LexWatchDb.EmbedDim) pendingStore.Add((toEmbed[i], v));
            }
        }
        var candidates = groups.Select(g => new GroupCandidate
        {
            Group_code = g.Code,
            Interest_text = g.InterestText,
            Similarity = Cosine(query, vectors[g.Code]),
        }).ToList();

        // 3) 분류
        var cls = await ai.ClassifyAsync(new ClassifyRequest
        {
            Run_id = ctx.Run.Id.ToString(),
            Law = AiMapping.Summary(change, summary),
            Candidates = candidates,
        }, attempt, ct);
        if (cls.Failure is not null) return cls.Failure;

        // --- 여기부터 변경 (AI 호출이 모두 성공한 뒤에만) ---
        foreach (var (g, v) in pendingStore) g.Embedding = new Vector(v);

        var verdicts = cls.Value!.Groups.GroupBy(v => v.Group_code).ToDictionary(x => x.Key, x => x.First());
        var existing = await db.GroupReviews.Where(r => r.RunId == ctx.Run.Id).ToDictionaryAsync(r => r.GroupCode, ct);
        var next = new List<NextStep>();
        var summaryOut = new List<object>();
        foreach (var g in groups)
        {
            verdicts.TryGetValue(g.Code, out var verdict);
            var confidence = verdict?.Confidence ?? 0.0;
            var reason = verdict?.Reason ?? "분류 응답에 이 그룹이 없다";
            var status = confidence >= ProceedAt ? ReviewStatus.Analyzing
                : confidence >= ReviewAt ? ReviewStatus.NeedsReview
                : ReviewStatus.ClosedIrrelevant;

            if (!existing.TryGetValue(g.Code, out var review))
            {
                review = new GroupReview { Id = Guid.NewGuid(), RunId = ctx.Run.Id, GroupCode = g.Code };
                db.GroupReviews.Add(review);
            }
            review.Status = status;
            review.Confidence = confidence;
            review.Reason = reason;
            summaryOut.Add(new { group = g.Code, confidence, status = status.ToString() });
            if (status != ReviewStatus.ClosedIrrelevant) next.Add(new NextStep("analyze", review.Id, null));
        }

        if (next.Count == 0)
        {
            ctx.Run.Status = RunStatus.ClosedIrrelevant;
            ctx.Run.FinishedAt = time.GetUtcNow();
        }
        else
        {
            ctx.Run.Status = RunStatus.InReview;
        }

        return new StepResult.Succeeded(new { groups = summaryOut }, next,
            AiMapping.ProviderKind(cls.Value.Provider), cls.Value.Provider.Name);
    }

    private static async Task<string> BuildSummaryAsync(LexWatchDb db, Guid runId, LawChange change, CancellationToken ct)
    {
        var rows = await db.Evidence.AsNoTracking().Where(e => e.RunId == runId && e.Kind == "oldnew").ToListAsync(ct);
        if (rows.Count == 0)
            rows = await db.Evidence.AsNoTracking().Where(e => e.RunId == runId && e.Kind == "law").ToListAsync(ct);
        var body = string.Join("\n\n", rows.OrderBy(e => e.EvidenceId, StringComparer.Ordinal).Select(e => e.Body));
        if (body.Length > SummaryOldNewChars) body = body[..SummaryOldNewChars];
        return string.IsNullOrEmpty(body) ? change.LawName : $"{change.LawName}\n{body}";
    }

    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 0;
        var s = dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        return Math.Clamp(s, 0, 1);
    }
}
