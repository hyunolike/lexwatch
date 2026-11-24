namespace LexWatch.Api.Data.Entities;

public enum RunStatus { Queued, Running, InReview, Completed, Failed, ClosedIrrelevant }
/// <summary>AwaitingPartner: 변호사가 적발을 확인하고 승인해 파트너 최종 승인을 기다린다(ai-governance GV-07).</summary>
public enum ReviewStatus { Analyzing, NeedsReview, WaitingApproval, Approved, Rejected, Notified, ClosedIrrelevant, AwaitingPartner }
public enum StepStatus { Pending, Running, Succeeded, RetryScheduled, Failed, NeedsReview }
