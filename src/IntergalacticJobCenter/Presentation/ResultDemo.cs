using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using IntergalacticJobCenter.Boundary;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Services;
using IntergalacticJobCenter.Workflow;

namespace IntergalacticJobCenter.Presentation;

public static class ResultDemo
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    public static string Run(string dataDirectory)
    {
        var sb = new StringBuilder();
        var csvPath = Path.Combine(dataDirectory, "applicants_sample.csv");
        var jsonPath = Path.Combine(dataDirectory, "applicants_sample.json");

        sb.AppendLine("=== 1. Boundary: loading data (the only place that touches files) ===");
        var csvLoad = ApplicantFileLoader.Load(csvPath);
        sb.AppendLine(ResultFormatter.Describe(csvLoad, list => $"{csvPath}: {list.Length} applicants"));
        sb.AppendLine(ResultFormatter.Describe(ApplicantFileLoader.Load(jsonPath), list => $"{jsonPath}: {list.Length} applicants"));
        sb.AppendLine();

        if (csvLoad is not Result<ImmutableArray<AlienApplicant>, AppError>.Ok loaded)
        {
            sb.AppendLine("Cannot continue without the sample data.");
            return sb.ToString();
        }

        var registry = ApplicantRegistry.From(loaded.Value);

        sb.AppendLine("=== 2. Lookups: present and absent ===");
        sb.AppendLine("Find(\"Zorp\")              -> " + ResultFormatter.DescribeMaybe(registry.Find("Zorp"), a => $"{a.Name}, aggression {a.Aggression}"));
        sb.AppendLine("Find(\"Nobody\")            -> " + ResultFormatter.DescribeMaybe(registry.Find("Nobody"), a => a.Name));
        sb.AppendLine("FindAggression(\"Mimi\")    -> " + ResultFormatter.DescribeMaybe(registry.FindAggression("Mimi"), v => v.ToString(CultureInfo.InvariantCulture) + " (zero is real data, not absence)"));
        sb.AppendLine("FindAggression(\"Nobody\")  -> " + ResultFormatter.DescribeMaybe(registry.FindAggression("Nobody"), v => v.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine("PolicyCatalog \"office\"     -> " + ResultFormatter.DescribeMaybe(PolicyCatalog.Find("office"), p => p.Name));
        sb.AppendLine("PolicyCatalog \"mars\"       -> " + ResultFormatter.DescribeMaybe(PolicyCatalog.Find("mars"), p => p.Name));
        sb.AppendLine();

        sb.AppendLine("=== 3. Placement chain (today = 2026-10-18) ===");
        var requests = new (string Label, PlacementRequest Request)[]
        {
            ("Normal path: Zorp, office", new PlacementRequest("Zorp", "office", "APP-001", "Officer Kim", Today.AddDays(7), Today)),
            ("Normal path: Blarg, extreme", new PlacementRequest("Blarg", "extreme", "APP-002", "Officer Kim", Today.AddDays(7), Today)),
            ("Domain failure 1 (rule violated): Blarg, office", new PlacementRequest("Blarg", "office", "APP-003", "Officer Kim", Today.AddDays(7), Today)),
            ("Domain failure 2 (missing applicant): Nobody", new PlacementRequest("Nobody", "office", "APP-004", "Officer Kim", Today.AddDays(7), Today)),
            ("Domain failure 2b (missing policy): Zorp, mars", new PlacementRequest("Zorp", "mars", "APP-005", "Officer Kim", Today.AddDays(7), Today)),
            ("Domain failure 3 (transition refused): start date in the past", new PlacementRequest("Zorp", "office", "APP-006", "Officer Kim", Today.AddDays(-1), Today)),
            ("Domain failure 3b (transition refused): blank officer", new PlacementRequest("Zorp", "office", "APP-007", " ", Today.AddDays(7), Today)),
        };

        Result<PlacementReceipt, AppError>? firstReceipt = null;
        foreach (var (label, request) in requests)
        {
            var result = PlacementService.Place(registry, request);
            firstReceipt ??= result;
            sb.AppendLine(label);
            sb.AppendLine("   " + ResultFormatter.Describe(result, DescribeReceipt));
        }

        sb.AppendLine("Domain failure 3c (transition refused): reject an already confirmed application");
        if (firstReceipt is Result<PlacementReceipt, AppError>.Ok first)
        {
            var rejected = WorkflowSteps.Reject(first.Value.State, "Changed our mind");
            sb.AppendLine("   " + ResultFormatter.Describe(rejected, WorkflowFormatter.Describe));
        }

        sb.AppendLine();

        sb.AppendLine("=== 4. Operational failures: the stored registry is never partially updated ===");
        sb.AppendLine($"Registry before: {registry.Count} applicants");

        var files = new (string Label, string Path)[]
        {
            ("File failure 1 (missing file)", Path.Combine(dataDirectory, "does_not_exist.csv")),
            ("File failure 2 (malformed JSON)", Path.Combine(dataDirectory, "applicants_malformed.json")),
            ("File failure 2b (malformed CSV rows)", Path.Combine(dataDirectory, "applicants_invalid.csv")),
            ("Valid JSON file (reload succeeds)", jsonPath),
        };

        foreach (var (label, filePath) in files)
        {
            var outcome = ApplicantFileLoader.Reload(registry, filePath);
            sb.AppendLine(label);
            sb.AppendLine("   " + DescribeReload(outcome));
        }

        sb.AppendLine($"Registry after the failed reloads is still the same object with {registry.Count} applicants.");
        return sb.ToString();
    }

    private static string DescribeReceipt(PlacementReceipt receipt) =>
        $"{receipt.ApplicantName} ({receipt.ApplicationId}) placed as {receipt.Job}, score {receipt.Score}, " +
        $"approved by {receipt.ApprovedBy}, starts {receipt.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    private static string DescribeReload(ReloadOutcome outcome) =>
        outcome switch
        {
            ReloadOutcome.Reloaded reloaded => $"RELOADED  new registry with {reloaded.Registry.Count} applicants",
            ReloadOutcome.Kept kept =>
                $"KEPT     previous registry ({kept.Registry.Count} applicants) - " + ResultFormatter.Describe(kept.Problem),
            _ => throw new InvalidOperationException($"Unhandled reload outcome {outcome.GetType().Name}.")
        };
}
