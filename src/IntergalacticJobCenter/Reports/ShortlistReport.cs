using System.Collections.Immutable;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Pipeline;
using IntergalacticJobCenter.Queries;

namespace IntergalacticJobCenter.Reports;

public sealed record ShortlistRow(int Rank, string Name, string Race, int Tentacles, int Score, string Job);

public static class ShortlistReport
{
    public static Result<ImmutableArray<ShortlistRow>, AppError> Build(
        IEnumerable<AlienApplicant> applicants, Func<AlienApplicant, int> score, int minScore)
    {
        ArgumentNullException.ThrowIfNull(applicants);
        ArgumentNullException.ThrowIfNull(score);

        if (minScore < 0 || minScore > 100)
        {
            return Outcomes.Domain<ImmutableArray<ShortlistRow>>(
                ErrorCode.InvalidInput, "minScore", $"Minimum score {minScore} is outside the allowed range 0-100.");
        }

        var rows = applicants
            .ScoreWith(score)
            .AtLeast(minScore)
            .RankByScore()
            .Select((scored, index) => new ShortlistRow(
                index + 1,
                scored.Applicant.Name,
                scored.Applicant.Race,
                scored.Applicant.Tentacles,
                scored.Score,
                Stages.JobFor(scored.Applicant)))
            .ToImmutableArray();

        return Outcomes.Ok(rows);
    }
}
