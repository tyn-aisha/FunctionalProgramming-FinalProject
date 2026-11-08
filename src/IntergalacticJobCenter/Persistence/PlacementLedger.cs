using System.Collections.Immutable;
using IntergalacticJobCenter.Functional;

namespace IntergalacticJobCenter.Persistence;

public sealed class PlacementLedger
{
    public ImmutableArray<LedgerEntry> Entries { get; }

    public int Count => Entries.Length;

    private PlacementLedger(ImmutableArray<LedgerEntry> entries)
    {
        Entries = entries;
    }

    public static PlacementLedger Empty { get; } = new(ImmutableArray<LedgerEntry>.Empty);

    public static Result<PlacementLedger, AppError> From(IEnumerable<LedgerEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Aggregate(
            Outcomes.Ok(Empty),
            (ledger, entry) => ledger.Bind(current => current.Add(entry)));
    }

    public Result<PlacementLedger, AppError> Add(LedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (Entries.Any(e => string.Equals(e.ApplicationId, entry.ApplicationId, StringComparison.OrdinalIgnoreCase)))
        {
            return Outcomes.Domain<PlacementLedger>(
                ErrorCode.DuplicateApplication,
                entry.ApplicationId,
                $"Application '{entry.ApplicationId}' is already recorded in the ledger.");
        }

        if (Entries.Any(e => string.Equals(e.ApplicantName, entry.ApplicantName, StringComparison.OrdinalIgnoreCase)))
        {
            return Outcomes.Domain<PlacementLedger>(
                ErrorCode.ApplicantAlreadyPlaced,
                entry.ApplicantName,
                $"Applicant '{entry.ApplicantName}' already has a recorded placement.");
        }

        return Outcomes.Ok(new PlacementLedger(Entries.Add(entry)));
    }
}
