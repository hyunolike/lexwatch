using Pgvector;

namespace LexWatch.Api.Data.Entities;

public class LawChange
{
    public Guid Id { get; set; }
    public string LawId { get; set; } = "";
    public string LawName { get; set; } = "";
    /// <summary>법령일련번호(MST). 신구조문·본문 호출에 쓴다. 비어 있으면 법령ID로 대체한다.</summary>
    public string Mst { get; set; } = "";
    public DateOnly PromulgationDate { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public string RawJson { get; set; } = "";
    public string RawSha256 { get; set; } = "";
    public DateTimeOffset FetchedAt { get; set; }
    public string IdempotencyKey { get; set; } = "";
}

public class WorkflowRun
{
    public Guid Id { get; set; }
    public Guid LawChangeId { get; set; }
    public RunStatus Status { get; set; }
    public string TraceId { get; set; } = "";
    public bool IsReplay { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public class GroupReview
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string GroupCode { get; set; } = "";
    public ReviewStatus Status { get; set; }
    public double? Confidence { get; set; }
    public string? Reason { get; set; }
    /// <summary>낙관적 동시성 토큰 (Postgres xmin).</summary>
    public uint Version { get; set; }
}

public class StepRun
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid? GroupReviewId { get; set; }
    public string Step { get; set; } = "";
    public int Attempt { get; set; }
    public StepStatus Status { get; set; }
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string ProviderKind { get; set; } = "";
    public string? ProviderName { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int? DurationMs { get; set; }
}

public class Evidence
{
    public Guid RunId { get; set; }
    public string EvidenceId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? SourceUrl { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>oldnew 근거의 법제처 공식 변경 구간. {"old":[[s,e],...],"new":[[s,e],...]}. 그 밖에는 null.</summary>
    public string? ChangeMarksJson { get; set; }
}

/// <summary>조문 검색 색인(hybrid-rag). 실행의 law 근거를 조문 구조 단위로 나눈 청크.</summary>
public class ArticleChunk
{
    public Guid RunId { get; set; }
    /// <summary>"{근거 ID}#{n}", n은 1부터.</summary>
    public string ChunkId { get; set; } = "";
    public string EvidenceId { get; set; } = "";
    /// <summary>실행 안 순번(근거 자연 정렬 → n). 검색 동점 정렬에 쓴다.</summary>
    public int Ord { get; set; }
    public string Text { get; set; } = "";
    /// <summary>KoreanTokenizer 결과(중복 포함, BM25 tf용).</summary>
    public string[] Tokens { get; set; } = [];
    public Vector? Embedding { get; set; }
    public string? EmbedModel { get; set; }
}

public class AgentToolCall
{
    public Guid Id { get; set; }
    public Guid StepRunId { get; set; }
    public int Seq { get; set; }
    public string Tool { get; set; } = "";
    public string ArgsJson { get; set; } = "";
    public string ResultSummary { get; set; } = "";
    public string[] EvidenceIds { get; set; } = [];
}

public class MemoDraft
{
    public Guid Id { get; set; }
    public Guid GroupReviewId { get; set; }
    public int Version { get; set; }
    public string SentencesJson { get; set; } = "";
    public string OpenQuestionsJson { get; set; } = "";
    public string CreatedBy { get; set; } = "";
}

public class CriticFinding
{
    public Guid Id { get; set; }
    public Guid MemoDraftId { get; set; }
    public int Tier { get; set; }
    public int SentenceIndex { get; set; }
    public string RuleId { get; set; } = "";
    public string Message { get; set; } = "";
}

public class Approval
{
    public Guid Id { get; set; }
    public Guid GroupReviewId { get; set; }
    public string Decision { get; set; } = "";
    public string ActorRole { get; set; } = "";
    public string? Comment { get; set; }
    public string? DiffJson { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
    /// <summary>승인 때 변호사가 확인한 적발 수(승인 직전 재검사 기준). 반려와 이 열 이전 기록은 null.</summary>
    public int? AcknowledgedFindingCount { get; set; }
    /// <summary>승인 때 재검사가 찾은 적발 목록([{sentenceIndex,tier,ruleId,message}]). 파트너가 무엇을 확인할지 고정한다. 적발 0이면 null.</summary>
    public string? FindingsJson { get; set; }
}

public class Notification
{
    public Guid Id { get; set; }
    /// <summary>수집 이상 같은 관리자 알림(channel=admin)은 null. 알림 발송 실패 관리자 알림은 해당 그룹을 가리킨다.</summary>
    public Guid? GroupReviewId { get; set; }
    public string Channel { get; set; } = "demo_inbox";
    public string Status { get; set; } = "";
    public string Body { get; set; } = "";
    public int Attempts { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}

public class PracticeGroup
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string InterestText { get; set; } = "";
    /// <summary>쓰는 곳이 없는 예약 열.</summary>
    public string[] LawIds { get; set; } = [];
    /// <summary>법안 레이더가 지켜볼 법률 이름(bill-radar BR-01).</summary>
    public string[] WatchLawNames { get; set; } = [];
    public Vector? Embedding { get; set; }
}

public class ChatQuota
{
    public DateOnly Day { get; set; }
    public string IpHash { get; set; } = "";
    public int Count { get; set; }
}

public class CollectLog
{
    public DateOnly Day { get; set; }
    public int ItemCount { get; set; }
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// AI 사용 기록(ai-governance GV-05). 질문, 답변, IP는 두지 않는다. 검토가 지워져도 남도록 외래키는 없다.
/// </summary>
public class AiUsageLog
{
    public Guid Id { get; set; }
    public DateTimeOffset At { get; set; }
    public string Purpose { get; set; } = "";
    public Guid? GroupReviewId { get; set; }
    public string ActorRole { get; set; } = "";
    /// <summary>answered, refused, unavailable, failed</summary>
    public string Outcome { get; set; } = "";
    public string? ProviderKind { get; set; }
    public string? ProviderName { get; set; }
    public int DurationMs { get; set; }
    /// <summary>질문에서 가린 종류별 건수. {"rrn":1}</summary>
    public string MaskedJson { get; set; } = "{}";
}

/// <summary>공포 전 법안(bill-radar). 그룹은 저장하지 않고 LawName과 PracticeGroup.WatchLawNames로 계산한다.</summary>
public class Bill
{
    public Guid Id { get; set; }
    public string Source { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string BillNo { get; set; } = "";
    public string BillName { get; set; } = "";
    public string LawName { get; set; } = "";
    public string Stage { get; set; } = "";
    public DateOnly ProposedOn { get; set; }
    public string Proposer { get; set; } = "";
    public string? LeadProposer { get; set; }
    public string? Committee { get; set; }
    public string? ProcResult { get; set; }
    public DateOnly? ProcOn { get; set; }
    public string DetailUrl { get; set; } = "";
    public string RawJson { get; set; } = "{}";
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastCheckedAt { get; set; }
    public DateTimeOffset LastEventAt { get; set; }
}

public class BillEvent
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public DateTimeOffset At { get; set; }
    /// <summary>proposed, stage_changed</summary>
    public string Kind { get; set; } = "";
    public string? FromStage { get; set; }
    public string ToStage { get; set; } = "";
}

public class BillCollectLog
{
    public Guid Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    /// <summary>성공한 호출이 1건 이상인가.</summary>
    public bool Ok { get; set; }
    public int NewBills { get; set; }
    public int StageChanges { get; set; }
    public int Refreshed { get; set; }
    public int FailureCount { get; set; }
    public string FailuresJson { get; set; } = "[]";
}
