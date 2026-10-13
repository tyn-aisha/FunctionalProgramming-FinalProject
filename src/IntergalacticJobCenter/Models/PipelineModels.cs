using System.Collections.Immutable;

namespace IntergalacticJobCenter.Models;

public sealed record Rejection(string Name, string Reason);

public sealed record ScreeningResult(
    ImmutableArray<AlienApplicant> Eligible,
    ImmutableArray<Rejection> Rejected);

public sealed record ScoredApplicant(AlienApplicant Applicant, int Score);

public sealed record ScoredBatch(
    ImmutableArray<ScoredApplicant> Scored,
    ImmutableArray<Rejection> Rejected);

public enum PlacementDecision
{
    Recommended,
    NotRecommended
}

public sealed record Placement(
    int Rank,
    string Name,
    string Race,
    int Score,
    PlacementDecision Decision,
    string Job);

public sealed record PlacementReport(
    string PolicyName,
    ImmutableArray<Placement> Placements,
    ImmutableArray<Rejection> Rejected,
    int RecommendedCount);
