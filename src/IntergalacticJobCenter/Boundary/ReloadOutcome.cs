using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;

namespace IntergalacticJobCenter.Boundary;

public abstract record ReloadOutcome
{
    private ReloadOutcome() { }

    public sealed record Reloaded(ApplicantRegistry Registry) : ReloadOutcome;

    public sealed record Kept(ApplicantRegistry Registry, AppError Problem) : ReloadOutcome;

    public ApplicantRegistry Current => this switch
    {
        Reloaded reloaded => reloaded.Registry,
        Kept kept => kept.Registry,
        _ => throw new InvalidOperationException($"Unhandled reload outcome {GetType().Name}.")
    };
}
