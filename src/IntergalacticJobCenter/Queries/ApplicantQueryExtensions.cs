using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Queries;

public static class ApplicantQueryExtensions
{

    public static IEnumerable<ScoredApplicant> AtLeast(this IEnumerable<ScoredApplicant> source, int minScore)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Where(s => s.Score >= minScore);
    }

    public static IEnumerable<ScoredApplicant> RankByScore(this IEnumerable<ScoredApplicant> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Applicant.Name, StringComparer.Ordinal);
    }
}
