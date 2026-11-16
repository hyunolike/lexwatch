namespace LexWatch.Api.Data.Entities;

public enum RunStatus { Queued, Running, InReview, Completed, Failed, ClosedIrrelevant }
public enum ReviewStatus { Analyzing, NeedsReview, WaitingApproval, Approved, Rejected, Notified, ClosedIrrelevant }
public enum StepStatus { Pending, Running, Succeeded, RetryScheduled, Failed, NeedsReview }
