using System.Collections.Immutable;
using IntergalacticJobCenter.Persistence;

namespace IntergalacticJobCenter.Reports;

public sealed record JobSummaryRow(string Job, int Count, int TotalFeeCents, DateOnly LatestStart);

public static class LedgerReport
{
    public static ImmutableArray<JobSummaryRow> ByJob(PlacementLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        return ledger.Entries
            .GroupBy(entry => entry.Job)
            .Select(group => new JobSummaryRow(
                group.Key,
                group.Count(),
                group.Sum(entry => entry.FeeCents),
                group.Aggregate(DateOnly.MinValue, (latest, entry) => entry.StartDate > latest ? entry.StartDate : latest)))
            .OrderByDescending(row => row.TotalFeeCents)
            .ThenBy(row => row.Job, StringComparer.Ordinal)
            .ToImmutableArray();
    }
}
