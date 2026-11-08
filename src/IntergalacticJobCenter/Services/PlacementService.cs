using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Policies;
using IntergalacticJobCenter.Workflow;

namespace IntergalacticJobCenter.Services;

public static class PlacementService
{
    private sealed record Candidate(AlienApplicant Applicant, ScoringPolicy Policy);

    public static Result<PlacementReceipt, AppError> Place(ApplicantRegistry registry, PlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(request);

        return FindApplicant(registry, request.ApplicantName)
            .Bind(applicant => FindPolicy(request.PolicyKey)
                .Map(policy => new Candidate(applicant, policy)))
            .Bind(candidate => CreateApplication(request.ApplicationId, candidate))
            .Map(ApplicationWorkflow.Start)
            .Bind(state => WorkflowSteps.Approve(state, request.Officer))
            .Bind(state => WorkflowSteps.Confirm(state, request.StartDate, request.Today))
            .Map(PlacementReceipt.From);
    }

    private static Result<AlienApplicant, AppError> FindApplicant(ApplicantRegistry registry, string? name) =>
        registry.Find(name).ToResult<AppError>(() => new AppError.Domain(
            ErrorCode.ApplicantNotFound,
            Describe(name),
            $"Applicant '{Describe(name)}' is not in the registry."));

    internal static Result<ScoringPolicy, AppError> FindPolicy(string? key) =>
        PolicyCatalog.Find(key).ToResult<AppError>(() => new AppError.Domain(
            ErrorCode.PolicyNotFound,
            Describe(key),
            $"Policy '{Describe(key)}' does not exist. Use 'office' or 'extreme'."));

    private static Result<ValidatedAlienApplication, AppError> CreateApplication(string? id, Candidate candidate) =>
        ValidatedAlienApplication.Create(id, candidate.Applicant, candidate.Policy) switch
        {
            ApplicationCreation.Created created => Outcomes.Ok(created.Application),
            ApplicationCreation.CreationRejected rejected => Outcomes.Domain<ValidatedAlienApplication>(
                ErrorCode.ApplicationRejected,
                candidate.Applicant.Name,
                string.Join(" ", rejected.Reasons)),
            _ => throw new InvalidOperationException("Unhandled application creation outcome.")
        };

    private static string Describe(string? text) => string.IsNullOrWhiteSpace(text) ? "(blank)" : text.Trim();
}
