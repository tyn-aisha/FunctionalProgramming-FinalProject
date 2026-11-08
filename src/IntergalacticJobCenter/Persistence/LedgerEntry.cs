using IntergalacticJobCenter.Services;

namespace IntergalacticJobCenter.Persistence;

public sealed record LedgerEntry(
    string ApplicationId,
    string ApplicantName,
    string Race,
    string Job,
    int Score,
    string ApprovedBy,
    DateOnly StartDate,
    int FeeCents)
{
    public static LedgerEntry From(PlacementReceipt receipt, int feeCents)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return new LedgerEntry(
            receipt.ApplicationId,
            receipt.ApplicantName,
            receipt.State.Application.Applicant.Race,
            receipt.Job,
            receipt.Score,
            receipt.ApprovedBy,
            receipt.StartDate,
            feeCents);
    }
}
