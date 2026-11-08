using IntergalacticJobCenter.Workflow;

namespace IntergalacticJobCenter.Services;

public sealed record PlacementReceipt(
    string ApplicationId,
    string ApplicantName,
    string Job,
    int Score,
    string ApprovedBy,
    DateOnly StartDate,
    ApplicationState.Confirmed State)
{
    public static PlacementReceipt From(ApplicationState.Confirmed confirmed) =>
        new(
            confirmed.Application.ApplicationId,
            confirmed.Application.Applicant.Name,
            confirmed.Application.Job,
            confirmed.Application.Score,
            confirmed.ApprovedBy,
            confirmed.StartDate,
            confirmed);
}
