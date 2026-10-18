using System.Collections.Immutable;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Policies;

namespace IntergalacticJobCenter.Workflow;

internal static class ApplicationRules
{
    public const int MinIdLength = 5;
    public const int MaxIdLength = 20;

    public static ImmutableArray<string> CheckId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return ImmutableArray.Create("Application id is required.");
        }

        var trimmed = id.Trim();
        var validShape = trimmed.Length >= MinIdLength
                         && trimmed.Length <= MaxIdLength
                         && trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

        return validShape
            ? ImmutableArray<string>.Empty
            : ImmutableArray.Create(
                $"Application id must be {MinIdLength}-{MaxIdLength} characters: letters, digits or hyphens.");
    }

    public static ImmutableArray<string> CheckEligibility(AlienApplicant applicant, ScoringPolicy policy, int score)
    {
        var reasons = ImmutableArray.CreateBuilder<string>();

        if (applicant.Aggression > policy.MaxAggression)
            reasons.Add($"Aggression {applicant.Aggression} exceeds maximum {policy.MaxAggression} of {policy.Name}.");

        if (applicant.Compatibility < policy.MinCompatibility)
            reasons.Add($"Compatibility {applicant.Compatibility} is below minimum {policy.MinCompatibility} of {policy.Name}.");

        if (score < policy.MinimumHireScore)
            reasons.Add($"Score {score} is below the hire threshold {policy.MinimumHireScore} of {policy.Name}.");

        return reasons.ToImmutable();
    }
}
