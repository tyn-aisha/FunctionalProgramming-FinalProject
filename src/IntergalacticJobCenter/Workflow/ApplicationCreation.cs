using System.Collections.Immutable;

namespace IntergalacticJobCenter.Workflow;

public abstract record ApplicationCreation
{
    private ApplicationCreation() { }

    public sealed record Created(ValidatedAlienApplication Application) : ApplicationCreation;

    public sealed record CreationRejected(ImmutableArray<string> Reasons) : ApplicationCreation;
}
