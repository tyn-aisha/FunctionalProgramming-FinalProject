using IntergalacticJobCenter.Input;
using IntergalacticJobCenter.Native;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Pipeline;
using IntergalacticJobCenter.Policies;
using IntergalacticJobCenter.Presentation;

const string DefaultPath = "data/applicants_sample.csv";

if (args.Length > 0 && args[0].Equals("workflow", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("INTERGALACTIC JOB CENTER - placement & confirmation workflow");
    Console.WriteLine();
    Console.WriteLine(WorkflowDemo.Render());
    return 0;
}

if (args.Length > 0 && args[0].Equals("result", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("INTERGALACTIC JOB CENTER - Result, Maybe, Map, Bind and file boundaries");
    Console.WriteLine();
    Console.WriteLine(ResultDemo.Run(args.Length > 1 ? args[1] : "data"));
    return 0;
}

if (args.Length > 0 && args[0].Equals("reports", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("INTERGALACTIC JOB CENTER - LINQ reports, currying, deferred execution, native C fee");
    Console.WriteLine();
    Console.WriteLine(ReportsDemo.Run(args.Length > 1 ? args[1] : "data", args.Length > 2 ? args[2] : "output"));
    return 0;
}

if (args.Length == 0 || args[0].Equals("app", StringComparison.OrdinalIgnoreCase))
{
    var paths = new AppPaths(
        args.Length > 1 ? args[1] : "data/applicants_sample.json",
        args.Length > 2 ? args[2] : "output/placements.json",
        args.Length > 3 ? args[3] : "output/shortlist.csv");

    return new ConsoleApplication(
        Console.In,
        Console.Out,
        paths,
        DateOnly.FromDateTime(DateTime.Now),
        FeeAdapter.Native).Run();
}

var path = args.Length > 0 ? args[0] : DefaultPath;
var mode = args.Length > 1 ? args[1].ToLowerInvariant() : "both";

ScoringPolicy[] policies = mode switch
{
    "office" => new[] { ScoringPolicies.OfficeSafety },
    "extreme" => new[] { ScoringPolicies.ExtremeConditions },
    "both" => new[] { ScoringPolicies.OfficeSafety, ScoringPolicies.ExtremeConditions },
    _ => Array.Empty<ScoringPolicy>()
};

if (policies.Length == 0)
{
    Console.Error.WriteLine($"Unknown policy '{mode}'. Use: office, extreme or both.");
    return 1;
}

IReadOnlyList<string> lines;
try
{
    lines = ApplicantFileReader.ReadLines(path);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine($"Cannot read input: {ex.Message}");
    return 1;
}

Console.WriteLine("INTERGALACTIC JOB CENTER - alien applicants to Earth");
Console.WriteLine($"Input file: {path}");
Console.WriteLine();

var outcomes = policies
    .Select(policy => ApplicantIntake.Process(lines, policy))
    .ToList();

var reports = new List<PlacementReport>();
var skippedPrinted = false;

foreach (var outcome in outcomes)
{
    switch (outcome)
    {
        case IntakeOutcome.Processed processed:
            if (!skippedPrinted)
            {
                foreach (var failure in processed.Skipped)
                {
                    Console.WriteLine($"REJECTED INPUT  {failure}");
                }

                if (processed.Skipped.Length > 0)
                {
                    Console.WriteLine();
                }

                Console.WriteLine($"Valid applicants parsed: {processed.ParsedCount}");
                Console.WriteLine();
                skippedPrinted = true;
            }

            reports.Add(processed.Report);
            Console.WriteLine(ReportFormatter.Format(processed.Report));
            break;

        case IntakeOutcome.Refused refused:
            Console.Error.WriteLine("Refused: " + string.Join(" ", refused.Reasons));
            return 1;

        default:
            throw new InvalidOperationException($"Unhandled outcome {outcome.GetType().Name}.");
    }
}

if (reports.Count == 2)
{
    var differences = PolicyComparison.Compare(reports[0], reports[1]);
    Console.WriteLine(ReportFormatter.FormatDifferences(reports[0].PolicyName, reports[1].PolicyName, differences));
}

return 0;
