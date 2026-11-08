using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Queries;

public static class ScoreFunctions
{

    public static readonly Func<int, Func<int, Func<AlienApplicant, int>>> CreateScore =
        compatibilityWeight => calmnessWeight =>
        {
            if (compatibilityWeight < 0 || calmnessWeight < 0 || compatibilityWeight + calmnessWeight == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(compatibilityWeight), "Weights must be non-negative and at least one must be positive.");
            }

            var totalWeight = compatibilityWeight + calmnessWeight;

            return applicant =>
                (compatibilityWeight * applicant.Compatibility + calmnessWeight * (100 - applicant.Aggression)) / totalWeight;
        };

    public static readonly Func<AlienApplicant, int> CompatibilityFocused = CreateScore(80)(20);

    public static readonly Func<AlienApplicant, int> CalmnessFocused = CreateScore(20)(80);
}
