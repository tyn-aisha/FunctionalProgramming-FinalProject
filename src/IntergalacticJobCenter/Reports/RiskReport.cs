using System.Collections.Immutable;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Reports;

public sealed record RiskBandRow(string Band, int Count, int TotalTentacles, int MaxCompatibility, string Names);

public static class RiskReport
{
    public static string AggressionBand(int aggression) =>
        aggression <= 20 ? "Calm"
        : aggression <= 50 ? "Tense"
        : "Hostile";

    public static Result<ImmutableArray<RiskBandRow>, AppError> Build(
        IEnumerable<AlienApplicant> applicants, int minCompatibility)
    {
        ArgumentNullException.ThrowIfNull(applicants);

        if (minCompatibility < 0 || minCompatibility > 100)
        {
            return Outcomes.Domain<ImmutableArray<RiskBandRow>>(
                ErrorCode.InvalidInput, "minCompatibility",
                $"Minimum compatibility {minCompatibility} is outside the allowed range 0-100.");
        }

        var rows = applicants
            .Where(a => a.Compatibility >= minCompatibility)
            .GroupBy(a => AggressionBand(a.Aggression))
            .Select(group => new RiskBandRow(
                group.Key,
                group.Count(),
                group.Sum(a => a.Tentacles),
                group.Aggregate(0, (best, a) => Math.Max(best, a.Compatibility)),
                string.Join(", ", group.Select(a => a.Name).OrderBy(name => name, StringComparer.Ordinal))))
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.Band, StringComparer.Ordinal)
            .ToImmutableArray();

        return Outcomes.Ok(rows);
    }
}
