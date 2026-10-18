using System.Collections.Immutable;
using System.Globalization;

namespace IntergalacticJobCenter.Workflow;

public static class ApplicationWorkflow
{
    public static ApplicationState Start(ValidatedAlienApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return new ApplicationState.Pending(application);
    }

    public static TransitionResult Apply(ApplicationState state, WorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        return state switch
        {
            ApplicationState.Pending pending => FromPending(pending, command),
            ApplicationState.Approved approved => FromApproved(approved, command),
            ApplicationState.Rejected rejected => FromRejected(rejected, command),
            ApplicationState.Confirmed confirmed => FromConfirmed(confirmed, command),
            _ => throw new InvalidOperationException($"Unhandled state {state.GetType().Name}.")
        };
    }

    public static WorkflowRun Replay(ApplicationState initial, IEnumerable<WorkflowCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(commands);

        var current = initial;
        var steps = ImmutableArray.CreateBuilder<TransitionResult>();

        foreach (var command in commands)
        {
            var result = Apply(current, command);
            steps.Add(result);
            current = result.ResultingState;
        }

        return new WorkflowRun(current, steps.ToImmutable());
    }

    private static TransitionResult FromPending(ApplicationState.Pending pending, WorkflowCommand command) =>
        command switch
        {
            WorkflowCommand.Approve approve => ApprovePending(pending, approve.Officer),
            WorkflowCommand.Reject reject => RejectPending(pending, reject.Reason),
            WorkflowCommand.Confirm => Fail(pending, TransitionError.MissingPrerequisite,
                "Cannot confirm a pending application: it must be approved first."),
            _ => throw Unhandled(command)
        };

    private static TransitionResult ApprovePending(ApplicationState.Pending pending, string? officer)
    {
        if (string.IsNullOrWhiteSpace(officer))
        {
            return Fail(pending, TransitionError.InvalidInput, "Approving officer is required.");
        }

        var next = new ApplicationState.Approved(pending.Application, officer.Trim());
        return new TransitionResult.Transitioned(pending, next);
    }

    private static TransitionResult RejectPending(ApplicationState.Pending pending, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Fail(pending, TransitionError.InvalidInput, "A rejection reason is required.");
        }

        var next = new ApplicationState.Rejected(pending.Application, reason.Trim());
        return new TransitionResult.Transitioned(pending, next);
    }

    private static TransitionResult FromApproved(ApplicationState.Approved approved, WorkflowCommand command) =>
        command switch
        {
            WorkflowCommand.Approve approve => SameText(approve.Officer, approved.ApprovedBy)
                ? new TransitionResult.AlreadyApplied(approved)
                : Fail(approved, TransitionError.InvalidTransition,
                    $"Application was already approved by {approved.ApprovedBy}."),
            WorkflowCommand.Reject => Fail(approved, TransitionError.InvalidTransition,
                "An approved application can no longer be rejected."),
            WorkflowCommand.Confirm confirm => ConfirmApproved(approved, confirm),
            _ => throw Unhandled(command)
        };

    private static TransitionResult ConfirmApproved(ApplicationState.Approved approved, WorkflowCommand.Confirm confirm)
    {
        if (confirm.StartDate < confirm.Today)
        {
            return Fail(approved, TransitionError.InvalidInput,
                $"Start date {Format(confirm.StartDate)} is before today ({Format(confirm.Today)}).");
        }

        var next = new ApplicationState.Confirmed(approved.Application, approved.ApprovedBy, confirm.StartDate);
        return new TransitionResult.Transitioned(approved, next);
    }

    private static TransitionResult FromRejected(ApplicationState.Rejected rejected, WorkflowCommand command) =>
        command switch
        {
            WorkflowCommand.Approve => Fail(rejected, TransitionError.InvalidTransition,
                "A rejected application is final and cannot be approved."),
            WorkflowCommand.Reject reject => SameText(reject.Reason, rejected.Reason)
                ? new TransitionResult.AlreadyApplied(rejected)
                : Fail(rejected, TransitionError.InvalidTransition,
                    $"Application was already rejected: {rejected.Reason}."),
            WorkflowCommand.Confirm => Fail(rejected, TransitionError.MissingPrerequisite,
                "Cannot confirm a rejected application: it was never approved."),
            _ => throw Unhandled(command)
        };

    private static TransitionResult FromConfirmed(ApplicationState.Confirmed confirmed, WorkflowCommand command) =>
        command switch
        {
            WorkflowCommand.Approve => Fail(confirmed, TransitionError.InvalidTransition,
                "A confirmed application is final and cannot be approved again."),
            WorkflowCommand.Reject => Fail(confirmed, TransitionError.InvalidTransition,
                "A confirmed application is final and cannot be rejected."),
            WorkflowCommand.Confirm confirm => confirm.StartDate == confirmed.StartDate
                ? new TransitionResult.AlreadyApplied(confirmed)
                : Fail(confirmed, TransitionError.InvalidTransition,
                    $"Application is already confirmed for {Format(confirmed.StartDate)}."),
            _ => throw Unhandled(command)
        };

    private static TransitionResult Fail(ApplicationState unchanged, TransitionError error, string message) =>
        new TransitionResult.Failed(unchanged, error, message);

    private static bool SameText(string? candidate, string stored) =>
        candidate is not null && string.Equals(candidate.Trim(), stored, StringComparison.Ordinal);

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static InvalidOperationException Unhandled(WorkflowCommand command) =>
        new($"Unhandled command {command.GetType().Name}.");
}
