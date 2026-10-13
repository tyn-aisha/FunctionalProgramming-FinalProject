using System.Collections.Immutable;
using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Pipeline;

public sealed record DecisionDifference(string Name, string UnderFirst, string UnderSecond);

public static class PolicyComparison
{

    public static ImmutableArray<DecisionDifference> Compare(PlacementReport first, PlacementReport second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var firstStatus = StatusByName(first);
        var secondStatus = StatusByName(second);

        return firstStatus.Keys
            .Union(secondStatus.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new DecisionDifference(
                name,
                firstStatus.GetValueOrDefault(name, "Absent"),
                secondStatus.GetValueOrDefault(name, "Absent")))
            .Where(d => d.UnderFirst != d.UnderSecond)
            .ToImmutableArray();
    }

    private static Dictionary<string, string> StatusByName(PlacementReport report)
    {
        var status = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var placement in report.Placements)
        {
            status[placement.Name] = placement.Decision == PlacementDecision.Recommended
                ? "Recommended"
                : "Not recommended";
        }

        foreach (var rejection in report.Rejected)
        {
            status.TryAdd(rejection.Name, "Rejected");
        }

        return status;
    }
}
