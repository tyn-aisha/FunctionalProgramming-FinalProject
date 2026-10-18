namespace IntergalacticJobCenter.Workflow;

public enum TransitionError
{

    MissingPrerequisite,

    InvalidTransition,

    InvalidInput
}

public abstract record TransitionResult
{
    private TransitionResult() { }

    public sealed record Transitioned(ApplicationState Previous, ApplicationState Current) : TransitionResult;

    public sealed record AlreadyApplied(ApplicationState Current) : TransitionResult;

    public sealed record Failed(ApplicationState Unchanged, TransitionError Error, string Message) : TransitionResult;

    public ApplicationState ResultingState => this switch
    {
        Transitioned transitioned => transitioned.Current,
        AlreadyApplied applied => applied.Current,
        Failed failed => failed.Unchanged,
        _ => throw new InvalidOperationException($"Unhandled result {GetType().Name}.")
    };
}

public sealed record WorkflowRun(
    ApplicationState Final,
    System.Collections.Immutable.ImmutableArray<TransitionResult> Steps);
