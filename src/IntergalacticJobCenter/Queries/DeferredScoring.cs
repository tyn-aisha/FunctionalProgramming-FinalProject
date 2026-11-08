using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Queries;

public static class DeferredScoring
{

    public static IEnumerable<ScoredApplicant> ScoreWith(
        this IEnumerable<AlienApplicant> applicants, Func<AlienApplicant, int> score)
    {

        ArgumentNullException.ThrowIfNull(applicants);
        ArgumentNullException.ThrowIfNull(score);
        return Iterate(applicants, score);
    }

    private static IEnumerable<ScoredApplicant> Iterate(
        IEnumerable<AlienApplicant> applicants, Func<AlienApplicant, int> score)
    {
        foreach (var applicant in applicants)
        {
            yield return new ScoredApplicant(applicant, Math.Clamp(score(applicant), 0, 100));
        }
    }
}
