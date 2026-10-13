using System.Collections.Immutable;

namespace IntergalacticJobCenter.Models;

public sealed record ParseFailure(int LineNumber, string Message)
{
    public override string ToString() => $"Line {LineNumber}: {Message}";
}

public sealed record ParseOutcome(
    ImmutableArray<AlienApplicant> Applicants,
    ImmutableArray<ParseFailure> Failures);
