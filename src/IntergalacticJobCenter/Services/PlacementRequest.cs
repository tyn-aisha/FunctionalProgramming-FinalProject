namespace IntergalacticJobCenter.Services;

public sealed record PlacementRequest(
    string? ApplicantName,
    string? PolicyKey,
    string? ApplicationId,
    string? Officer,
    DateOnly StartDate,
    DateOnly Today);
