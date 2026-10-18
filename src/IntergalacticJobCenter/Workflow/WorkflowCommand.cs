namespace IntergalacticJobCenter.Workflow;

public abstract record WorkflowCommand
{
    private WorkflowCommand() { }

    public sealed record Approve(string? Officer) : WorkflowCommand;

    public sealed record Reject(string? Reason) : WorkflowCommand;

    public sealed record Confirm(DateOnly StartDate, DateOnly Today) : WorkflowCommand;
}
