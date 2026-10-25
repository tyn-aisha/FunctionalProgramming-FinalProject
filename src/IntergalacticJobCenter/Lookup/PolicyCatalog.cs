using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Policies;

namespace IntergalacticJobCenter.Lookup;

public static class PolicyCatalog
{
    public static Maybe<ScoringPolicy> Find(string? key) =>
        key?.Trim().ToLowerInvariant() switch
        {
            "office" => Maybe.Some(ScoringPolicies.OfficeSafety),
            "extreme" => Maybe.Some(ScoringPolicies.ExtremeConditions),
            _ => Maybe<ScoringPolicy>.Empty
        };
}
