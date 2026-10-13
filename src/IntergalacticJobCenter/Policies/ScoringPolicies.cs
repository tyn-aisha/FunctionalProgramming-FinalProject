using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Policies;

public static class ScoringPolicies
{

    public static readonly ScoringPolicy OfficeSafety = new(
        Name: "Office Safety Policy",
        MaxAggression: 30,
        MinCompatibility: 50,
        MinimumHireScore: 65,
        Score: OfficeSafetyScore);

    public static readonly ScoringPolicy ExtremeConditions = new(
        Name: "Extreme Conditions Policy",
        MaxAggression: 80,
        MinCompatibility: 20,
        MinimumHireScore: 60,
        Score: ExtremeConditionsScore);

    public static int OfficeSafetyScore(AlienApplicant applicant) =>
        (6 * applicant.Compatibility + 4 * (100 - applicant.Aggression)) / 10;

    public static int ExtremeConditionsScore(AlienApplicant applicant) =>
        Math.Min(applicant.Tentacles, 10) * 5
        + Math.Min(applicant.Eyes, 10) * 3
        + applicant.Compatibility / 5;
}
