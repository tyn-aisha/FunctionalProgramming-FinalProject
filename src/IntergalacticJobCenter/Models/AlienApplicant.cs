using System.Collections.Immutable;

namespace IntergalacticJobCenter.Models;

public sealed record AlienApplicant
{
    public const int MaxBodyParts = 64;
    public const int MaxScale = 100;

    public string Name { get; }
    public string Race { get; }
    public int Tentacles { get; }
    public int Eyes { get; }
    public int Aggression { get; }
    public int Compatibility { get; }

    private AlienApplicant(string name, string race, int tentacles, int eyes, int aggression, int compatibility)
    {
        Name = name;
        Race = race;
        Tentacles = tentacles;
        Eyes = eyes;
        Aggression = aggression;
        Compatibility = compatibility;
    }

    public static ApplicantCreation Create(
        string? name, string? race, int tentacles, int eyes, int aggression, int compatibility)
    {
        var reasons = ImmutableArray.CreateBuilder<string>();

        if (string.IsNullOrWhiteSpace(name)) reasons.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(race)) reasons.Add("Race is required.");

        CheckRange(reasons, "Tentacles", tentacles, MaxBodyParts);
        CheckRange(reasons, "Eyes", eyes, MaxBodyParts);
        CheckRange(reasons, "Aggression", aggression, MaxScale);
        CheckRange(reasons, "Compatibility", compatibility, MaxScale);

        if (reasons.Count > 0 || name is null || race is null)
        {
            return new ApplicantCreation.Invalid(reasons.ToImmutable());
        }

        return new ApplicantCreation.Valid(
            new AlienApplicant(name, race, tentacles, eyes, aggression, compatibility));
    }

    public AlienApplicant Normalize() =>
        new(CollapseSpaces(Name), Capitalize(CollapseSpaces(Race)), Tentacles, Eyes, Aggression, Compatibility);

    private static void CheckRange(ImmutableArray<string>.Builder reasons, string field, int value, int max)
    {
        if (value < 0 || value > max)
        {
            reasons.Add($"{field} {value} is outside the allowed range 0-{max}.");
        }
    }

    private static string CollapseSpaces(string text) =>
        string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();
}
