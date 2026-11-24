using LexWatch.Api.Data.Entities;

namespace LexWatch.Api.Ops;

/// <summary>운영 현황 집계의 입력. 엔드포인트가 기간에 맞춰 읽어 넘긴다(재현 실행 제외).</summary>
public sealed record OpsInput(
    IReadOnlyList<WorkflowRun> Runs,
    IReadOnlyList<StepRun> Steps,
    IReadOnlyList<GroupReview> Groups,
    IReadOnlyList<MemoDraft> AgentDrafts,
    IReadOnlyList<CriticFinding> Findings,
    IReadOnlyList<Approval> Approvals,
    IReadOnlyList<AiUsageLog> Chat,
    IReadOnlyList<CollectLog> Collect,
    IReadOnlyList<BillCollectLog> BillLogs,
    int WaitingGroups);

/// <summary>
/// 운영 현황 집계(ops-visibility OV-10~16). DB를 읽지 않는 순수 함수다. 일 단위는 KST 날짜다.
/// 단계 기록 한 행은 자동 재시도마다 Attempt를 올리고, 관리자 다시 실행은 새 행을 만든다(StepQueue, RunEndpoints).
/// </summary>
public static class OpsSummary
{
    public static readonly string[] StepOrder =
        ["fetch_articles", "classify", "index_articles", "analyze", "critic_citation", "critic_source", "critic_judge", "notify"];
    public static readonly string[] AiSteps = ["classify", "index_articles", "analyze", "critic_judge"];
    /// <summary>모델 공급자. fallback(키 없는 규칙 대체)은 AI가 아니라 따로 센다(ai-footprints AF-06).</summary>
    public static readonly string[] Providers = ["nvidia", "cloudflare", "groq"];
    /// <summary>표본이 이보다 적으면 90번째 백분위를 내지 않는다(최댓값과 같아진다).</summary>
    public const int MinSampleForP90 = 5;
    private static readonly TimeZoneInfo Kst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    public static DateOnly KstDay(DateTimeOffset t) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t, Kst).DateTime);

    /// <summary>KST 날짜 자정의 UTC 시각.</summary>
    public static DateTimeOffset KstMidnight(DateOnly d)
    {
        var local = d.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Kst.GetUtcOffset(local)).ToUniversalTime();
    }

    private static bool Finished(StepRun s) => s.Status is StepStatus.Succeeded or StepStatus.NeedsReview or StepStatus.Failed;
    private static DateTimeOffset? EndAt(StepRun s) => s.FinishedAt ?? s.StartedAt;

    /// <summary>nearest-rank 백분위. 빈 목록이면 null.</summary>
    public static int? Percentile(IReadOnlyList<int> sorted, double p)
    {
        if (sorted.Count == 0) return null;
        var rank = (int)Math.Ceiling(p * sorted.Count);
        return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
    }

    private static double? Median(List<double> xs)
    {
        if (xs.Count == 0) return null;
        xs.Sort();
        var m = xs.Count / 2;
        return xs.Count % 2 == 1 ? xs[m] : (xs[m - 1] + xs[m]) / 2;
    }

    public static object Build(OpsInput input, DateOnly fromDay, DateOnly today)
    {
        var dates = Enumerable.Range(0, today.DayNumber - fromDay.DayNumber + 1).Select(i => fromDay.AddDays(i)).ToList();
        var from = KstMidnight(fromDay);
        bool InWindow(DateTimeOffset? t) => t is { } v && v >= from;
        int[] Daily(IEnumerable<DateTimeOffset> times)
        {
            var counts = times.GroupBy(KstDay).ToDictionary(g => g.Key, g => g.Count());
            return dates.Select(d => counts.GetValueOrDefault(d)).ToArray();
        }

        // 처리량(OV-11)
        var runs = input.Runs.Where(r => InWindow(r.CreatedAt)).ToList();
        var runStatuses = new[] { RunStatus.InReview, RunStatus.Completed, RunStatus.ClosedIrrelevant, RunStatus.Failed, RunStatus.Running, RunStatus.Queued };

        // 단계 신뢰성(OV-12). 실패율은 최종 결과 기준이다. 같은 (실행, 단계, 그룹)의 마지막 기록이 실패인 것만 실패로 센다.
        // 관리자 다시 실행이나 상류 재실행으로 새 기록이 생겨도 성공으로 끝났으면 실패가 아니다.
        var finished = input.Steps.Where(s => Finished(s) && InWindow(EndAt(s))).ToList();
        var lastOfChain = input.Steps.GroupBy(s => (s.RunId, s.Step, s.GroupReviewId))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartedAt ?? s.NotBefore).ThenBy(s => s.Attempt).ThenBy(s => s.Id).Last());
        var steps = StepOrder.Select(name =>
        {
            var rows = finished.Where(s => s.Step == name).ToList();
            var outcomes = rows.Select(s => lastOfChain[(s.RunId, s.Step, s.GroupReviewId)]).DistinctBy(s => s.Id)
                .Where(Finished).ToList();
            var ok = rows.Where(s => s.Status == StepStatus.Succeeded && s.DurationMs is not null).Select(s => s.DurationMs!.Value).Order().ToList();
            var failed = outcomes.Count(s => s.Status == StepStatus.Failed);
            return new
            {
                step = name,
                outcomes = outcomes.Count,
                failed,
                failureRate = outcomes.Count == 0 ? (double?)null : Math.Round((double)failed / outcomes.Count, 3),
                autoRetries = rows.Sum(s => Math.Max(s.Attempt - 1, 0)),
                extraRecords = rows.Count - rows.Select(s => (s.RunId, s.GroupReviewId)).Distinct().Count(),
                samples = ok.Count,
                p50Ms = Percentile(ok, 0.5),
                p90Ms = ok.Count >= MinSampleForP90 ? Percentile(ok, 0.9) : null,
            };
        }).ToList();
        var allOutcomes = steps.Sum(x => x.outcomes);
        var allFailed = steps.Sum(x => x.failed);

        // AI 공급자(OV-13)
        // "AI 처리"는 호출 수가 아니라 끝난 기록의 최종 공급자 기준이다(대체 경로의 앞선 시도, 에이전트 내부 호출은 세지 않는다).
        var aiFinished = finished.Where(s => AiSteps.Contains(s.Step)).ToList();
        var aiRows = aiFinished.Where(s => Providers.Contains(s.ProviderKind)).ToList();
        var chat = input.Chat.Where(c => InWindow(c.At)).ToList();
        var chatByModel = chat.Where(c => c.ProviderKind is not null && Providers.Contains(c.ProviderKind)).ToList();
        var aiDaily = Daily(aiRows.Select(s => EndAt(s)!.Value).Concat(chatByModel.Select(c => c.At)));

        // 검수(OV-14): 기간 안에 끝난 analyze 성공 기록의 그룹마다 최신 에이전트 초안
        // 세 검사가 끝나기 전의 초안은 "적발 없음"으로 세지 않도록, 3단 판정이 끝난 그룹만 센다.
        var judged = input.Steps.Where(s => s.Step == "critic_judge" && Finished(s) && s.GroupReviewId is not null)
            .Select(s => s.GroupReviewId!.Value).ToHashSet();
        var analyzedGroups = finished.Where(s => s.Step == "analyze" && s.Status == StepStatus.Succeeded && s.GroupReviewId is not null)
            .Select(s => s.GroupReviewId!.Value).Where(judged.Contains).ToHashSet();
        var drafts = input.AgentDrafts.Where(d => analyzedGroups.Contains(d.GroupReviewId))
            .GroupBy(d => d.GroupReviewId).Select(g => g.MaxBy(d => d.Version)!).ToList();
        var draftIds = drafts.Select(d => d.Id).ToHashSet();
        var findings = input.Findings.Where(f => draftIds.Contains(f.MemoDraftId)).ToList();
        var flagged = findings.Select(f => f.MemoDraftId).Distinct().Count();

        // 사람 결정(OV-15)
        var decisions = input.Approvals.Where(a => InWindow(a.DecidedAt)).ToList();
        // 결정까지 걸린 시간: 그룹의 첫 변호사 승인이나 반려가 기간 안이면, 그 결정 전에 끝난 마지막 검토 준비 단계
        // (3단 판정, 없으면 분석. 근거 부족도 포함)의 종료 시각부터 잰다. 짝수면 가운데 두 값의 평균.
        var ready = input.Steps.Where(s => s.Step is "critic_judge" or "analyze" && s.GroupReviewId is not null && s.FinishedAt is not null
                                           && s.Status is StepStatus.Succeeded or StepStatus.NeedsReview or StepStatus.Failed)
            .GroupBy(s => s.GroupReviewId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var hours = new List<double>();
        foreach (var first in input.Approvals.Where(a => a.ActorRole == "lawyer" && a.Decision is "approve" or "reject")
                     .GroupBy(a => a.GroupReviewId).Select(g => g.MinBy(a => a.DecidedAt)!).Where(a => InWindow(a.DecidedAt)))
        {
            if (!ready.TryGetValue(first.GroupReviewId, out var rs)) continue;
            var before = rs.Where(s => s.FinishedAt <= first.DecidedAt).ToList();
            var start = before.Where(s => s.Step == "critic_judge").Select(s => s.FinishedAt).Max()
                        ?? before.Where(s => s.Step == "analyze").Select(s => s.FinishedAt).Max();
            if (start is { } t) hours.Add((first.DecidedAt - t).TotalHours);
        }

        var lastBillOk = input.BillLogs.Where(l => l.Ok).Select(l => (DateTimeOffset?)l.FinishedAt).Max();

        return new
        {
            days = dates.Count,
            from = fromDay,
            to = today,
            dates,
            runs = new
            {
                total = runs.Count,
                daily = Daily(runs.Select(r => r.CreatedAt)),
                byStatus = runStatuses.ToDictionary(s => s.ToString(), s => runs.Count(r => r.Status == s)),
            },
            collect = dates.Select(d => input.Collect.FirstOrDefault(c => c.Day == d)?.ItemCount).ToArray(),
            stepTotals = new
            {
                outcomes = allOutcomes, failed = allFailed,
                failureRate = allOutcomes == 0 ? (double?)null : Math.Round((double)allFailed / allOutcomes, 3),
            },
            steps,
            ai = new
            {
                total = aiRows.Count + chatByModel.Count,
                byProvider = Providers.ToDictionary(p => p, p => aiRows.Count(s => s.ProviderKind == p)),
                ruleFallback = aiFinished.Count(s => s.ProviderKind == "fallback"),
                failed = aiFinished.Count(s => s.Status == StepStatus.Failed),
                chat = new
                {
                    total = chat.Count,
                    byOutcome = Governance.AiUsage.Outcomes.ToDictionary(o => o, o => chat.Count(c => c.Outcome == o)),
                    byProvider = chat.Where(c => c.ProviderKind is not null).GroupBy(c => c.ProviderKind!).ToDictionary(g => g.Key, g => g.Count()),
                },
                daily = aiDaily,
            },
            critic = new
            {
                drafts = drafts.Count,
                flagged,
                flaggedRate = drafts.Count == 0 ? (double?)null : Math.Round((double)flagged / drafts.Count, 3),
                byTier = new[] { 1, 2, 3 }.ToDictionary(t => t.ToString(), t => findings.Count(f => f.Tier == t)),
            },
            decisions = new
            {
                total = decisions.Count,
                byKind = new[] { "approve", "reject", "confirm", "return" }.ToDictionary(k => k, k => decisions.Count(a => a.Decision == k)),
                firstDecisionSamples = hours.Count,
                medianHoursToFirstDecision = Median(hours) is { } m ? Math.Round(m, 1) : (double?)null,
                waiting = input.WaitingGroups,
            },
            bills = new
            {
                lastSuccessAt = lastBillOk,
                failedRuns = input.BillLogs.Count(l => !l.Ok && InWindow(l.StartedAt)),
            },
        };
    }
}
