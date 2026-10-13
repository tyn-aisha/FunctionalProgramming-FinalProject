using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Policies;

namespace IntergalacticJobCenter.Pipeline;

public static class PlacementPipeline
{

    public static PlacementReport Run(IReadOnlyList<AlienApplicant> applicants, ScoringPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(applicants);
        ArgumentNullException.ThrowIfNull(policy);

        var screened = Stages.FilterAndNormalize(applicants, policy.MaxAggression, policy.MinCompatibility);
        var scored = Stages.ScoreApplicants(screened, policy.Score);
        return Stages.BuildReport(scored, policy.Name, policy.MinimumHireScore);
    }
}
