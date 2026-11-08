using IntergalacticJobCenter.Lookup;
using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Tests;

internal static class SampleData
{
    public static AlienApplicant[] Applicants() => new[]
    {
        TestApplicants.Make("Zorp", "Zorgian", 6, 2, 10, 90),
        TestApplicants.Make("Blarg", "Blargonian", 10, 8, 55, 70),
        TestApplicants.Make("Xylo", "Xylosian", 2, 2, 30, 50),
        TestApplicants.Make("Grakk", "Grakkian", 12, 4, 95, 80),
        TestApplicants.Make("Mimi", "Mimikian", 0, 0, 0, 100),
        TestApplicants.Make("Vexa", "Vexari", 4, 6, 20, 15)
    };

    public static ApplicantRegistry Registry() => ApplicantRegistry.From(Applicants());
}
