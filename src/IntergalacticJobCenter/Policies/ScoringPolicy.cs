using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Policies;

public sealed record ScoringPolicy(
    string Name,
    int MaxAggression,
    int MinCompatibility,
    int MinimumHireScore,
    Func<AlienApplicant, int> Score);
