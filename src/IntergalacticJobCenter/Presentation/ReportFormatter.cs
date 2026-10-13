using System.Collections.Immutable;
using System.Text;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Pipeline;

namespace IntergalacticJobCenter.Presentation;

public static class ReportFormatter
{
    public static string Format(PlacementReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        sb.AppendLine($"=== {report.PolicyName} ===");

        if (report.Placements.Length == 0)
        {
            sb.AppendLine("No eligible applicants.");
        }
        else
        {
            sb.AppendLine($"{"Rank",-5}{"Name",-10}{"Race",-12}{"Score",-7}{"Decision",-17}Job");
            foreach (var p in report.Placements)
            {
                var decision = p.Decision == PlacementDecision.Recommended ? "Recommended" : "Not recommended";
                sb.AppendLine($"{p.Rank,-5}{p.Name,-10}{p.Race,-12}{p.Score,-7}{decision,-17}{p.Job}");
            }
        }

        if (report.Rejected.Length > 0)
        {
            sb.AppendLine("Rejected:");
            foreach (var r in report.Rejected)
            {
                sb.AppendLine($"  - {r.Name}: {r.Reason}");
            }
        }

        sb.AppendLine($"Recommended: {report.RecommendedCount} of {report.Placements.Length} screened applicants");
        return sb.ToString();
    }

    public static string FormatDifferences(
        string firstPolicy, string secondPolicy, ImmutableArray<DecisionDifference> differences)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Same data, different policy: {firstPolicy} vs {secondPolicy} ===");

        if (differences.Length == 0)
        {
            sb.AppendLine("No applicant outcome differs.");
        }
        else
        {
            foreach (var d in differences)
            {
                sb.AppendLine($"  {d.Name}: {d.UnderFirst} -> {d.UnderSecond}");
            }
        }

        return sb.ToString();
    }
}
