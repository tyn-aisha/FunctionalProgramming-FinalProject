using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Workflow;

namespace IntergalacticJobCenter.Services;

public static class WorkflowSteps
{
    public static Result<ApplicationState, AppError> Approve(ApplicationState state, string? officer) =>
        ToResult(state, ApplicationWorkflow.Apply(state, new WorkflowCommand.Approve(officer)));

    public static Result<ApplicationState, AppError> Reject(ApplicationState state, string? reason) =>
        ToResult(state, ApplicationWorkflow.Apply(state, new WorkflowCommand.Reject(reason)));

    public static Result<ApplicationState.Confirmed, AppError> Confirm(
        ApplicationState state, DateOnly startDate, DateOnly today) =>
        ToResult(state, ApplicationWorkflow.Apply(state, new WorkflowCommand.Confirm(startDate, today)))
            .Bind(next => next switch
            {
                ApplicationState.Confirmed confirmed => Outcomes.Ok(confirmed),
                _ => Outcomes.Domain<ApplicationState.Confirmed>(
                    ErrorCode.TransitionRejected,
                    next.Application.ApplicationId,
                    $"Expected a confirmed application but it is {next.GetType().Name}.")
            });

    private static Result<ApplicationState, AppError> ToResult(ApplicationState state, TransitionResult result) =>
        result switch
        {
            TransitionResult.Transitioned transitioned => Outcomes.Ok(transitioned.Current),
            TransitionResult.AlreadyApplied applied => Outcomes.Ok(applied.Current),
            TransitionResult.Failed failed => Outcomes.Domain<ApplicationState>(
                ErrorCode.TransitionRejected,
                state.Application.ApplicationId,
                $"{failed.Error}: {failed.Message}"),
            _ => throw new InvalidOperationException($"Unhandled transition result {result.GetType().Name}.")
        };
}
