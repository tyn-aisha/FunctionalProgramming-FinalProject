using System.Collections.Immutable;

namespace IntergalacticJobCenter.Models;

public abstract record ApplicantCreation
{
    private ApplicantCreation() { }

    public sealed record Valid(AlienApplicant Applicant) : ApplicantCreation;

    public sealed record Invalid(ImmutableArray<string> Reasons) : ApplicantCreation;
}
