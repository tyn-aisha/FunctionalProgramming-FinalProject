using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using IntergalacticJobCenter.Functional;

namespace IntergalacticJobCenter.Reports;

public static class ReportExporter
{
    public static Result<int, AppError> Export(string? path, ImmutableArray<PricedRow> rows)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Outcomes.Operational<int>(ErrorCode.InvalidPath, "(none)", "No export file path was provided.");
        }

        var tempPath = path + ".tmp";

        try
        {
            WriteCsv(tempPath, rows);
            File.Move(tempPath, path, overwrite: true);
            return Outcomes.Ok(rows.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcomes.Operational<int>(
                ErrorCode.FileWriteFailed, path, $"Could not write the report '{path}' ({ex.Message})");
        }
        finally
        {

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void WriteCsv(string tempPath, ImmutableArray<PricedRow> rows)
    {
        using var writer = new StreamWriter(tempPath, append: false, new UTF8Encoding(false));

        writer.WriteLine("Rank,Name,Race,Score,Job,FeeCents");
        foreach (var priced in rows)
        {
            var row = priced.Row;
            writer.WriteLine(string.Join(',',
                row.Rank.ToString(CultureInfo.InvariantCulture),
                Escape(row.Name),
                Escape(row.Race),
                row.Score.ToString(CultureInfo.InvariantCulture),
                Escape(row.Job),
                priced.FeeCents.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static string Escape(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
