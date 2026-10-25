using System.Collections.Immutable;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Lookup;

public sealed class ApplicantRegistry
{
    private readonly ImmutableDictionary<string, AlienApplicant> _byName;

    public ImmutableArray<AlienApplicant> Applicants { get; }

    public int Count => Applicants.Length;

    private ApplicantRegistry(ImmutableArray<AlienApplicant> applicants)
    {
        Applicants = applicants;
        _byName = applicants.ToImmutableDictionary(a => a.Name.Trim(), a => a, StringComparer.OrdinalIgnoreCase);
    }

    public static ApplicantRegistry Empty { get; } = new(ImmutableArray<AlienApplicant>.Empty);

    public static ApplicantRegistry From(IEnumerable<AlienApplicant> applicants)
    {
        ArgumentNullException.ThrowIfNull(applicants);

        var kept = ImmutableArray.CreateBuilder<AlienApplicant>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var applicant in applicants)
        {
            if (applicant is null)
            {
                throw new ArgumentException("Applicant list must not contain null items.", nameof(applicants));
            }

            if (seen.Add(applicant.Name.Trim()))
            {
                kept.Add(applicant);
            }
        }

        return new ApplicantRegistry(kept.ToImmutable());
    }

    public Maybe<AlienApplicant> Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Maybe<AlienApplicant>.Empty;
        }

        return _byName.TryGetValue(name.Trim(), out var applicant)
            ? Maybe.Some(applicant)
            : Maybe<AlienApplicant>.Empty;
    }

    public Maybe<int> FindAggression(string? name) => Find(name).Map(a => a.Aggression);
}
