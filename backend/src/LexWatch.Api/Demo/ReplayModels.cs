namespace LexWatch.Api.Demo;

/// <summary>fixtures/demo/replays/*.json 한 건. 재현 실행의 단계·도구 호출·발견·상태를 담는다.</summary>
public record ReplayFile(string Slug, string Note, int AgoMinutes, ReplayLaw Law, string RunStatus,
    List<ReplayEvidence> Evidence, List<ReplayStep> Steps, List<ReplayGroup> Groups);

public record ReplayLaw(string LawId, string LawName, string Mst, DateOnly PromulgationDate, DateOnly? EffectiveDate);

public record ReplayEvidence(string Id, string Kind, string Title, string Body);

public record ReplayToolCall(string Tool, System.Text.Json.JsonElement Args, string ResultSummary, List<string> EvidenceIds);

public record ReplayStep(string Step, string? Group, int Attempt, string Status, int At, int DurationMs,
    System.Text.Json.JsonElement? Output, string? ErrorCode, string? ErrorMessage, List<ReplayToolCall>? ToolCalls);

public record ReplayFinding(int Tier, int SentenceIndex, string RuleId, string Message);

public record ReplaySentence(string Sentence, List<string> Citations);

public record ReplayDraft(string CreatedBy, List<ReplaySentence> Sentences, List<string> OpenQuestions,
    List<ReplayFinding> Findings);

public record ReplayApproval(string Decision, string ActorRole, string? Comment, int AtOffset);

public record ReplayGroup(string Code, string Status, double? Confidence, string? Reason, ReplayDraft? Draft,
    ReplayApproval? Approval);
