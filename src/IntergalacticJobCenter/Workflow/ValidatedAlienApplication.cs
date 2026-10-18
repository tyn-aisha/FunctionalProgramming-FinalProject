using System.Collections.Immutable;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Pipeline;
using IntergalacticJobCenter.Policies;

namespace IntergalacticJobCenter.Workflow;

public sealed record ValidatedAlienApplication
{
    public string ApplicationId { get; }
    public AlienApplicant Applicant { get; }
    public string PolicyName { get; }
    public int Score { get; }
    public string Job { get; }

    private ValidatedAlienApplication(
        string applicationId, AlienApplicant applicant, string policyName, int score, string job)
    {
        ApplicationId = applicationId;
        Applicant = applicant;
        PolicyName = policyName;
        Score = score;
        Job = job;
    }

    public static ApplicationCreation Create(string? applicationId, AlienApplicant? applicant, ScoringPolicy? policy)
    {
        var reasons = ImmutableArray.CreateBuilder<string>();

        reasons.AddRange(ApplicationRules.CheckId(applicationId));

        if (applicant is null)
        {
            reasons.Add("Applicant is required.");
        }

        if (policy is null)
        {
            reasons.Add("Policy is required.");
        }

        var score = 0;
        if (applicant is not null && policy is not null)
        {
            score = Math.Clamp(policy.Score(applicant), 0, 100);
            reasons.AddRange(ApplicationRules.CheckEligibility(applicant, policy, score));
        }

        if (reasons.Count > 0 || applicationId is null || applicant is null || policy is null)
        {
            return new ApplicationCreation.CreationRejected(reasons.ToImmutable());
        }

        var normalised = applicant.Normalize();

        return new ApplicationCreation.Created(new ValidatedAlienApplication(
            applicationId.Trim(), normalised, policy.Name, score, Stages.JobFor(normalised)));
    }
}
