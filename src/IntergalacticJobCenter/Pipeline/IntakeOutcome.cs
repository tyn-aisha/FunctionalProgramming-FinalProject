using System.Collections.Immutable;
using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Pipeline;

public abstract record IntakeOutcome
{
    private IntakeOutcome() { }

    public sealed record Processed(
        PlacementReport Report,
        ImmutableArray<ParseFailure> Skipped,
        int ParsedCount) : IntakeOutcome;

    public sealed record Refused(ImmutableArray<string> Reasons) : IntakeOutcome;
}
