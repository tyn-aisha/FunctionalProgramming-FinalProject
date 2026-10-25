namespace IntergalacticJobCenter.Boundary;

public sealed record ApplicantDto(
    string? Name,
    string? Race,
    int? Tentacles,
    int? Eyes,
    int? Aggression,
    int? Compatibility);
