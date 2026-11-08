using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Tests;

internal static class TestApplicants
{
    public static AlienApplicant Make(
        string name, string race, int tentacles, int eyes, int aggression, int compatibility) =>
        Assert.IsType<ApplicantCreation.Valid>(
            AlienApplicant.Create(name, race, tentacles, eyes, aggression, compatibility)).Applicant;
}
