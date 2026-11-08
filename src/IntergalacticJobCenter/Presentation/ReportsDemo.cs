using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using IntergalacticJobCenter.Boundary;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Native;
using IntergalacticJobCenter.Queries;
using IntergalacticJobCenter.Reports;

namespace IntergalacticJobCenter.Presentation;

public static class ReportsDemo
{
    private const int MinScore = 60;
    private const int MinCompatibility = 20;

    public static string Run(string dataDirectory, string outputDirectory)
    {
        var sb = new StringBuilder();
        var jsonPath = Path.Combine(dataDirectory, "applicants_sample.json");
        var csvPath = Path.Combine(dataDirectory, "applicants_sample.csv");

        sb.AppendLine("=== 1. Data (JSON and streamed CSV) ===");
        sb.AppendLine(ResultFormatter.Describe(ApplicantFileLoader.Load(csvPath), list => $"{csvPath}: {list.Length} applicants"));
        var jsonLoad = ApplicantFileLoader.Load(jsonPath);
        sb.AppendLine(ResultFormatter.Describe(jsonLoad, list => $"{jsonPath}: {list.Length} applicants"));
        sb.AppendLine();

        if (jsonLoad is not Result<ImmutableArray<AlienApplicant>, AppError>.Ok loaded)
        {
            sb.AppendLine("Cannot continue without the sample data.");
            return sb.ToString();
        }

        var registry = ApplicantRegistry.From(loaded.Value);

        sb.AppendLine("=== 2. Deferred execution (the counter lives in this demo, not in the calculation) ===");
        var calls = 0;
        Func<AlienApplicant, int> probe = applicant =>
        {
            calls++;
            return ScoreFunctions.CompatibilityFocused(applicant);
        };

        var lazy = registry.Applicants.ScoreWith(probe);
        sb.AppendLine($"after creating the sequence      : scorer calls = {calls}");
        var snapshot = lazy.ToImmutableArray();
        sb.AppendLine($"after ToImmutableArray (snapshot): scorer calls = {calls}");
        _ = snapshot.Sum(s => s.Score) + snapshot.Max(s => s.Score);
        sb.AppendLine($"after reading the snapshot twice : scorer calls = {calls}   (reuse costs nothing)");
        _ = lazy.Count();
        sb.AppendLine($"after enumerating the lazy again : scorer calls = {calls}   (not materialised -> processed again)");
        sb.AppendLine();

        sb.AppendLine("=== 3. Reports with two partially applied scoring functions (min score 60, min compatibility 20) ===");
        var configs = new (string Label, Func<AlienApplicant, int> Score)[]
        {
            ("CreateScore(80)(20) compatibility-focused", ScoreFunctions.CompatibilityFocused),
            ("CreateScore(20)(80) calmness-focused", ScoreFunctions.CalmnessFocused)
        };

        foreach (var (label, score) in configs)
        {
            sb.AppendLine($"Report 1 - shortlist, {label}");
            sb.AppendLine(Indent(ResultFormatter.Describe(
                ReportWorkflow.Run(registry, score, MinScore, MinCompatibility, FeeAdapter.Native), ReportText.Shortlist)));
        }

        sb.AppendLine("Report 2 - risk overview by aggression band (compatibility >= 20)");
        sb.AppendLine(Indent(ResultFormatter.Describe(
            RiskReport.Build(registry.Applicants, MinCompatibility), ReportText.Risk)));
        sb.AppendLine();

        sb.AppendLine("=== 4. Export (temp file + move, StreamWriter disposed on every path) ===");
        var exportPath = Path.Combine(outputDirectory, "shortlist_compatibility.csv");
        try
        {
            Directory.CreateDirectory(outputDirectory);
            sb.AppendLine(ResultFormatter.Describe(
                ReportWorkflow.RunAndExport(registry, ScoreFunctions.CompatibilityFocused, MinScore, MinCompatibility,
                    FeeAdapter.Native, exportPath),
                summary => $"{summary.RowsExported} rows written to {summary.Path}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            sb.AppendLine($"FAILED  [OPERATIONAL FileWriteFailed] {outputDirectory}: could not create the output directory ({ex.Message})");
        }

        sb.AppendLine();

        sb.AppendLine("=== 5. Failure paths (registry unchanged, nothing exported) ===");
        var brokenNative = new FeeAdapter((_, _) => throw new DllNotFoundException("simulated: library not found"));
        sb.AppendLine("Native library unavailable (simulated adapter failure)");
        sb.AppendLine("   " + ResultFormatter.Describe(
            ReportWorkflow.RunAndExport(registry, ScoreFunctions.CompatibilityFocused, MinScore, MinCompatibility,
                brokenNative, Path.Combine(outputDirectory, "not_written.csv")),
            summary => summary.Path));
        sb.AppendLine("Invalid parameter (minimum score 150)");
        sb.AppendLine("   " + ResultFormatter.Describe(
            ReportWorkflow.Run(registry, ScoreFunctions.CompatibilityFocused, 150, MinCompatibility, FeeAdapter.Native),
            ReportText.Shortlist));
        sb.AppendLine("Missing data file");
        sb.AppendLine("   " + ResultFormatter.Describe(
            ApplicantFileLoader.Load(Path.Combine(dataDirectory, "does_not_exist.csv")), list => list.Length.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine("Malformed data file");
        sb.AppendLine("   " + ResultFormatter.Describe(
            ApplicantFileLoader.Load(Path.Combine(dataDirectory, "applicants_malformed.json")), list => list.Length.ToString(CultureInfo.InvariantCulture)));

        return sb.ToString();
    }

    private static string Indent(string text) =>
        "   " + text.Replace(Environment.NewLine, Environment.NewLine + "   ");
}
