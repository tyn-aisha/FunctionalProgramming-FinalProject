using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using IntergalacticJobCenter.Persistence;
using IntergalacticJobCenter.Reports;
using IntergalacticJobCenter.Services;

namespace IntergalacticJobCenter.Presentation;

public static class ReportText
{
    public static string Credits(int cents) =>
        $"{(cents / 100).ToString(CultureInfo.InvariantCulture)}.{(cents % 100).ToString("00", CultureInfo.InvariantCulture)} credits";

    public static string Receipt(PlacementReceipt receipt) =>
        $"{receipt.ApplicantName} ({receipt.ApplicationId}) placed as {receipt.Job}, score {receipt.Score}, " +
        $"approved by {receipt.ApprovedBy}, starts {receipt.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    public static string Shortlist(ReportBundle bundle)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"shortlist: {bundle.Shortlist.Length} applicant(s)");
        foreach (var priced in bundle.Shortlist)
        {
            var row = priced.Row;
            sb.AppendLine($"{row.Rank}. {row.Name,-6} {row.Race,-11} score {row.Score,3}  {row.Job,-24} fee {Credits(priced.FeeCents)}");
        }

        return sb.ToString().TrimEnd();
    }

    public static string Risk(ImmutableArray<RiskBandRow> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.AppendLine($"{row.Band,-8} count {row.Count}  tentacles {row.TotalTentacles,3}  max compatibility {row.MaxCompatibility,3}  [{row.Names}]");
        }

        return sb.Length == 0 ? "no matching applicants" : sb.ToString().TrimEnd();
    }

    public static string Bundle(ReportBundle bundle) =>
        Shortlist(bundle) + Environment.NewLine + "risk overview:" + Environment.NewLine + Risk(bundle.Risk);

    public static string Recorded(RecordedPlacement recorded, string path) =>
        $"Placement recorded: {recorded.Entry.ApplicantName} ({recorded.Entry.ApplicationId}) as {recorded.Entry.Job}, " +
        $"fee {Credits(recorded.Entry.FeeCents)}. Ledger now has {recorded.Ledger.Count} entr(y/ies), saved to {path}";

    public static string Ledger(PlacementLedger ledger)
    {
        if (ledger.Count == 0)
        {
            return "The ledger is empty.";
        }

        var sb = new StringBuilder();
        foreach (var entry in ledger.Entries)
        {
            sb.AppendLine($"{entry.ApplicationId,-10} {entry.ApplicantName,-8} {entry.Job,-24} start {entry.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}  fee {Credits(entry.FeeCents)}");
        }

        sb.AppendLine("by job:");
        foreach (var row in LedgerReport.ByJob(ledger))
        {
            sb.AppendLine($"  {row.Job,-24} count {row.Count}  total fee {Credits(row.TotalFeeCents)}  latest start {row.LatestStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }

        return sb.ToString().TrimEnd();
    }
}
