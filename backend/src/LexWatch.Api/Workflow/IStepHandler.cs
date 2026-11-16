using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;

namespace LexWatch.Api.Workflow;

public record StepContext(StepRun Step, WorkflowRun Run, LexWatchDb Db);

public interface IStepHandler
{
    string Step { get; }

    Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct);
}
