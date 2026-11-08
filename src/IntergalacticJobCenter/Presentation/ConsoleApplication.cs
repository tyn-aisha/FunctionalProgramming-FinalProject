using System.Collections.Immutable;
using IntergalacticJobCenter.Boundary;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Native;
using IntergalacticJobCenter.Persistence;
using IntergalacticJobCenter.Pipeline;
using IntergalacticJobCenter.Queries;
using IntergalacticJobCenter.Reports;
using IntergalacticJobCenter.Services;

namespace IntergalacticJobCenter.Presentation;

public sealed record AppPaths(string DataFile, string LedgerFile, string ExportFile);

public sealed class ConsoleApplication
{
    private sealed record Session(ApplicantRegistry Registry, PlacementLedger Ledger, AppError? LedgerProblem);

    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly AppPaths _paths;
    private readonly DateOnly _today;
    private readonly FeeAdapter _fees;

    public ConsoleApplication(TextReader input, TextWriter output, AppPaths paths, DateOnly today, FeeAdapter fees)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(fees);

        _input = input;
        _output = output;
        _paths = paths;
        _today = today;
        _fees = fees;
    }

    public int Run()
    {
        var session = Start();

        while (true)
        {
            WriteMenu();
            var choice = Ask("Choose a command");

            if (choice is null)
            {
                _output.WriteLine("End of input.");
                return 0;
            }

            switch (choice.Trim())
            {
                case "0":
                    _output.WriteLine("Goodbye.");
                    return 0;
                case "1":
                    session = ReloadApplicants(session);
                    break;
                case "2":
                    ScreenAndScore(session);
                    break;
                case "3":
                    Place(session);
                    break;
                case "4":
                    Shortlist(session);
                    break;
                case "5":
                    RiskOverview(session);
                    break;
                case "6":
                    session = PlaceAndRecord(session);
                    break;
                case "7":
                    ExportShortlist(session);
                    break;
                case "8":
                    ShowLedger(session);
                    break;
                default:
                    _output.WriteLine($"Unknown command '{choice.Trim()}'. Enter a number from 0 to 8.");
                    break;
            }
        }
    }

    private Session Start()
    {
        _output.WriteLine("INTERGALACTIC JOB CENTER");

        var registry = ApplicantFileLoader.Load(_paths.DataFile).Match(
            applicants =>
            {
                _output.WriteLine($"Loaded {applicants.Length} applicants from {_paths.DataFile}.");
                return ApplicantRegistry.From(applicants);
            },
            error =>
            {
                _output.WriteLine("Applicants not loaded: " + ResultFormatter.Describe(error));
                return ApplicantRegistry.Empty;
            });

        return LedgerStore.LoadOrStart(_paths.LedgerFile).Match(
            loaded =>
            {
                _output.WriteLine(loaded.StartedEmpty
                    ? $"No ledger file found at {_paths.LedgerFile}; starting with an empty ledger."
                    : $"Loaded {loaded.Ledger.Count} ledger entr(y/ies) from {_paths.LedgerFile}.");
                return new Session(registry, loaded.Ledger, null);
            },
            error =>
            {
                _output.WriteLine("Ledger not loaded: " + ResultFormatter.Describe(error));
                _output.WriteLine("Recording placements is disabled until the ledger file is fixed or removed.");
                return new Session(registry, PlacementLedger.Empty, error);
            });
    }

    private void WriteMenu()
    {
        _output.WriteLine();
        _output.WriteLine("1) Load applicants file");
        _output.WriteLine("2) Screen and score applicants under a policy");
        _output.WriteLine("3) Place an applicant (approve and confirm)");
        _output.WriteLine("4) Hiring shortlist with native placement fees");
        _output.WriteLine("5) Risk overview by aggression band");
        _output.WriteLine("6) Place an applicant and record it in the ledger");
        _output.WriteLine("7) Export the shortlist to CSV");
        _output.WriteLine("8) Show the ledger");
        _output.WriteLine("0) Exit");
    }

    private Session ReloadApplicants(Session session)
    {
        var path = AskDefault("Applicants file", _paths.DataFile);
        if (path is null)
        {
            return session;
        }

        var outcome = ApplicantFileLoader.Reload(session.Registry, path);

        switch (outcome)
        {
            case ReloadOutcome.Reloaded reloaded:
                _output.WriteLine($"Loaded {reloaded.Registry.Count} applicants from {path}.");
                return session with { Registry = reloaded.Registry };
            case ReloadOutcome.Kept kept:
                _output.WriteLine(ResultFormatter.Describe(kept.Problem));
                _output.WriteLine($"Previous registry kept ({kept.Registry.Count} applicants).");
                return session;
            default:
                throw new InvalidOperationException($"Unhandled reload outcome {outcome.GetType().Name}.");
        }
    }

    private void ScreenAndScore(Session session)
    {
        var answers = AskMany("Policy (office/extreme)");
        if (answers is null)
        {
            return;
        }

        var result = PlacementService.FindPolicy(answers[0])
            .Map(policy => PlacementPipeline.Run(session.Registry.Applicants, policy));

        _output.WriteLine(ResultFormatter.Describe(result, ReportFormatter.Format));
    }

    private void Place(Session session)
    {
        var request = AskPlacementRequest();
        if (request is null)
        {
            return;
        }

        var result = request.Bind(r => PlacementService.Place(session.Registry, r));
        _output.WriteLine(ResultFormatter.Describe(result, ReportText.Receipt));
    }

    private Session PlaceAndRecord(Session session)
    {
        if (session.LedgerProblem is not null)
        {
            _output.WriteLine("Recording is disabled because the ledger file could not be read: "
                + ResultFormatter.Describe(session.LedgerProblem));
            return session;
        }

        var request = AskPlacementRequest();
        if (request is null)
        {
            return session;
        }

        var result = request
            .Bind(r => PlacementDesk.PlaceAndRecord(session.Registry, r, _fees, session.Ledger))
            .Bind(recorded => LedgerStore.Save(_paths.LedgerFile, recorded.Ledger).Map(_ => recorded));

        _output.WriteLine(ResultFormatter.Describe(result, recorded => ReportText.Recorded(recorded, _paths.LedgerFile)));

        return result.Match(
            recorded => session with { Ledger = recorded.Ledger },
            _ => session);
    }

    private void Shortlist(Session session)
    {
        var scoring = ChooseScoring();
        if (scoring is null)
        {
            return;
        }

        var answers = AskMany("Minimum score (0-100)", "Minimum compatibility for the risk overview (0-100)");
        if (answers is null)
        {
            return;
        }

        var result = scoring.Bind(score =>
            InputParsing.ParseInt(answers[0], "Minimum score", 0, 100).Bind(minScore =>
                InputParsing.ParseInt(answers[1], "Minimum compatibility", 0, 100).Bind(minCompatibility =>
                    ReportWorkflow.Run(session.Registry, score, minScore, minCompatibility, _fees))));

        _output.WriteLine(ResultFormatter.Describe(result, ReportText.Bundle));
    }

    private void RiskOverview(Session session)
    {
        var answers = AskMany("Minimum compatibility (0-100)");
        if (answers is null)
        {
            return;
        }

        var result = InputParsing.ParseInt(answers[0], "Minimum compatibility", 0, 100)
            .Bind(minCompatibility => RiskReport.Build(session.Registry.Applicants, minCompatibility));

        _output.WriteLine(ResultFormatter.Describe(result, ReportText.Risk));
    }

    private void ExportShortlist(Session session)
    {
        var scoring = ChooseScoring();
        if (scoring is null)
        {
            return;
        }

        var path = AskDefault("Export file", _paths.ExportFile);
        var answers = path is null
            ? null
            : AskMany("Minimum score (0-100)", "Minimum compatibility for the risk overview (0-100)");
        if (path is null || answers is null)
        {
            return;
        }

        var result = scoring.Bind(score =>
            InputParsing.ParseInt(answers[0], "Minimum score", 0, 100).Bind(minScore =>
                InputParsing.ParseInt(answers[1], "Minimum compatibility", 0, 100).Bind(minCompatibility =>
                    ReportWorkflow.RunAndExport(session.Registry, score, minScore, minCompatibility, _fees, path))));

        _output.WriteLine(ResultFormatter.Describe(result, summary => $"{summary.RowsExported} rows written to {summary.Path}"));
    }

    private void ShowLedger(Session session)
    {
        _output.WriteLine(ReportText.Ledger(session.Ledger));
    }

    private Result<PlacementRequest, AppError>? AskPlacementRequest()
    {
        var answers = AskMany(
            "Applicant name",
            "Policy (office/extreme)",
            "Application id",
            "Officer",
            "Start date (yyyy-MM-dd)");

        return answers is null
            ? null
            : InputParsing.ParseDate(answers[4], "Start date")
                .Map(start => new PlacementRequest(answers[0], answers[1], answers[2], answers[3], start, _today));
    }

    private Result<Func<AlienApplicant, int>, AppError>? ChooseScoring()
    {
        var choice = AskDefault("Scoring (compat/calm/custom)", "compat");
        if (choice is null)
        {
            return null;
        }

        switch (choice.ToLowerInvariant())
        {
            case "compat":
                return Outcomes.Ok(ScoreFunctions.CompatibilityFocused);
            case "calm":
                return Outcomes.Ok(ScoreFunctions.CalmnessFocused);
            case "custom":
                var weights = AskMany("Compatibility weight (0-100)", "Calmness weight (0-100)");
                return weights is null
                    ? null
                    : InputParsing.ParseInt(weights[0], "Compatibility weight", 0, 100)
                        .Bind(compatibility => InputParsing.ParseInt(weights[1], "Calmness weight", 0, 100)
                            .Bind(calmness => compatibility + calmness == 0
                                ? Outcomes.Domain<Func<AlienApplicant, int>>(
                                    ErrorCode.InvalidInput, "weights", "At least one weight must be positive.")
                                : Outcomes.Ok(ScoreFunctions.CreateScore(compatibility)(calmness))));
            default:
                return Outcomes.Domain<Func<AlienApplicant, int>>(
                    ErrorCode.InvalidInput, "scoring", $"Scoring '{choice}' is unknown. Use compat, calm or custom.");
        }
    }

    private string? Ask(string label)
    {
        _output.Write(label + ": ");
        return _input.ReadLine();
    }

    private string? AskDefault(string label, string fallback)
    {
        var answer = Ask($"{label} [{fallback}]");
        return answer is null ? null : string.IsNullOrWhiteSpace(answer) ? fallback : answer.Trim();
    }

    private string[]? AskMany(params string[] labels)
    {
        var answers = new string[labels.Length];

        for (var i = 0; i < labels.Length; i++)
        {
            var answer = Ask(labels[i]);
            if (answer is null)
            {
                return null;
            }

            answers[i] = answer;
        }

        return answers;
    }
}
