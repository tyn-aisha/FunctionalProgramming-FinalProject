using System.Collections.Immutable;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;
using IntergalacticJobCenter.Native;
using IntergalacticJobCenter.Queries;

namespace IntergalacticJobCenter.Reports;

public static class ReportWorkflow
{
    public static Result<ReportBundle, AppError> Run(
        ApplicantRegistry registry,
        Func<Models.AlienApplicant, int> score,
        int minScore,
        int minCompatibility,
        FeeAdapter fees)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(fees);

        return ShortlistReport.Build(registry.Applicants, score, minScore)
            .Bind(rows => Price(rows, fees))
            .Bind(priced => RiskReport.Build(registry.Applicants, minCompatibility)
                .Map(risk => new ReportBundle(priced, risk)));
    }

    public static Result<ExportSummary, AppError> RunAndExport(
        ApplicantRegistry registry,
        Func<Models.AlienApplicant, int> score,
        int minScore,
        int minCompatibility,
        FeeAdapter fees,
        string? exportPath)
    {
        return Run(registry, score, minScore, minCompatibility, fees)
            .Bind(bundle => ReportExporter.Export(exportPath, bundle.Shortlist)
                .Map(count => new ExportSummary(bundle, count, exportPath ?? string.Empty)));
    }

    private static Result<ImmutableArray<PricedRow>, AppError> Price(ImmutableArray<ShortlistRow> rows, FeeAdapter fees) =>
        rows.AsEnumerable()
            .Select(row => fees.Calculate(row.Score, row.Tentacles).Map(fee => new PricedRow(row, fee)))
            .Sequence();
}
