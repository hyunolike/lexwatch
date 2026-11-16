namespace LexWatch.Api.Workflow;

public record NextStep(string Step, Guid? GroupReviewId, object? Input);

public abstract record StepResult
{
    public sealed record Succeeded(
        object? Output,
        IReadOnlyList<NextStep> Next,
        string ProviderKind = "none",
        string? ProviderName = null) : StepResult;

    public sealed record Retry(string ErrorCode, string Message, TimeSpan? After) : StepResult;

    public sealed record NeedsReview(string Reason, object? Output) : StepResult;

    public sealed record Failed(string ErrorCode, string Message) : StepResult;
}
