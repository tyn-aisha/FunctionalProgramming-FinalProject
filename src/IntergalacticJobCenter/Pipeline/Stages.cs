using System.Collections.Immutable;
using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Pipeline;

public static class Stages
{

    public static ScreeningResult FilterAndNormalize(
        IReadOnlyList<AlienApplicant> applicants,
        int maxAggression,
        int minCompatibility)
    {
        ArgumentNullException.ThrowIfNull(applicants);

        var eligible = ImmutableArray.CreateBuilder<AlienApplicant>();
        var rejected = ImmutableArray.CreateBuilder<Rejection>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in applicants)
        {
            if (raw is null)
            {
                throw new ArgumentException("Applicant list must not contain null items.", nameof(applicants));
            }

            var applicant = raw.Normalize();

            if (!seenNames.Add(applicant.Name))
            {
                rejected.Add(new Rejection(applicant.Name, "Duplicate applicant name"));
            }
            else if (applicant.Aggression > maxAggression)
            {
                rejected.Add(new Rejection(
                    applicant.Name, $"Aggression {applicant.Aggression} exceeds maximum {maxAggression}"));
            }
            else if (applicant.Compatibility < minCompatibility)
            {
                rejected.Add(new Rejection(
                    applicant.Name, $"Compatibility {applicant.Compatibility} is below minimum {minCompatibility}"));
            }
            else
            {
                eligible.Add(applicant);
            }
        }

        return new ScreeningResult(eligible.ToImmutable(), rejected.ToImmutable());
    }

    public static ScoredBatch ScoreApplicants(
        ScreeningResult screening,
        Func<AlienApplicant, int> scoreRule)
    {
        ArgumentNullException.ThrowIfNull(screening);
        ArgumentNullException.ThrowIfNull(scoreRule);

        var scored = screening.Eligible
            .Select(applicant => new ScoredApplicant(applicant, Math.Clamp(scoreRule(applicant), 0, 100)))
            .ToImmutableArray();

        return new ScoredBatch(scored, screening.Rejected);
    }

    public static PlacementReport BuildReport(
        ScoredBatch batch,
        string policyName,
        int minimumHireScore)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        var placements = batch.Scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Applicant.Name, StringComparer.Ordinal)
            .Select((s, index) =>
            {
                var recommended = s.Score >= minimumHireScore;
                return new Placement(
                    Rank: index + 1,
                    Name: s.Applicant.Name,
                    Race: s.Applicant.Race,
                    Score: s.Score,
                    Decision: recommended ? PlacementDecision.Recommended : PlacementDecision.NotRecommended,
                    Job: recommended ? JobFor(s.Applicant) : "None");
            })
            .ToImmutableArray();

        var recommendedCount = placements.Count(p => p.Decision == PlacementDecision.Recommended);

        return new PlacementReport(policyName, placements, batch.Rejected, recommendedCount);
    }

    public static string JobFor(AlienApplicant applicant) =>
        applicant.Tentacles >= 8 ? "Multi-Task Operator"
        : applicant.Eyes >= 6 ? "Surveillance Analyst"
        : applicant.Compatibility >= 90 ? "Front-Desk Receptionist"
        : "General Clerk";
}
