using System.Collections.Immutable;
using IntergalacticJobCenter.Input;
using IntergalacticJobCenter.Policies;

namespace IntergalacticJobCenter.Pipeline;

public static class ApplicantIntake
{

    public static IntakeOutcome Process(IEnumerable<string?>? lines, ScoringPolicy? policy)
    {
        var missing = ImmutableArray.CreateBuilder<string>();
        if (lines is null) missing.Add("Input lines are missing.");
        if (policy is null) missing.Add("Policy is missing.");

        if (lines is null || policy is null)
        {
            return new IntakeOutcome.Refused(missing.ToImmutable());
        }

        var parsed = ApplicantParser.ParseLines(lines);
        var report = PlacementPipeline.Run(parsed.Applicants, policy);

        return new IntakeOutcome.Processed(report, parsed.Failures, parsed.Applicants.Length);
    }
}
