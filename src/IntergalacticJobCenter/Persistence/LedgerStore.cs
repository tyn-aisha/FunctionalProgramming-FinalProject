using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using IntergalacticJobCenter.Functional;

namespace IntergalacticJobCenter.Persistence;

public sealed record LedgerEntryDto(
    string? ApplicationId,
    string? ApplicantName,
    string? Race,
    string? Job,
    int? Score,
    string? ApprovedBy,
    string? StartDate,
    int? FeeCents);

public sealed record LedgerLoad(PlacementLedger Ledger, bool StartedEmpty);

public static class LedgerStore
{
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static Result<PlacementLedger, AppError> Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail<PlacementLedger>(ErrorCode.InvalidPath, "(none)", "No ledger file path was provided.");
        }

        try
        {
            return Parse(path, File.ReadAllText(path));
        }
        catch (FileNotFoundException ex)
        {
            return Fail<PlacementLedger>(ErrorCode.FileNotFound, path, $"Ledger file not found: '{path}' ({ex.Message})");
        }
        catch (DirectoryNotFoundException ex)
        {
            return Fail<PlacementLedger>(ErrorCode.FileNotFound, path, $"Directory not found for '{path}' ({ex.Message})");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail<PlacementLedger>(ErrorCode.FileUnreadable, path, $"Could not read '{path}' ({ex.Message})");
        }
        catch (JsonException ex)
        {
            return Fail<PlacementLedger>(ErrorCode.MalformedContent, path, $"Malformed JSON in '{path}': {ex.Message}");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Fail<PlacementLedger>(ErrorCode.InvalidPath, path, $"The path '{path}' is not valid ({ex.Message})");
        }
    }

    public static Result<LedgerLoad, AppError> LoadOrStart(string? path) =>
        Load(path).Match(
            ledger => Outcomes.Ok(new LedgerLoad(ledger, false)),
            error => error.Code == ErrorCode.FileNotFound
                ? Outcomes.Ok(new LedgerLoad(PlacementLedger.Empty, true))
                : Outcomes.Fail<LedgerLoad>(error));

    public static Result<int, AppError> Save(string? path, PlacementLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail<int>(ErrorCode.InvalidPath, "(none)", "No ledger file path was provided.");
        }

        var tempPath = path + ".tmp";

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(ledger.Entries.Select(ToDto).ToArray(), WriteOptions);

            using (var writer = new StreamWriter(tempPath, append: false, new UTF8Encoding(false)))
            {
                writer.Write(json);
            }

            File.Move(tempPath, path, overwrite: true);
            return Outcomes.Ok(ledger.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail<int>(ErrorCode.FileWriteFailed, path, $"Could not save the ledger '{path}' ({ex.Message})");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Fail<int>(ErrorCode.InvalidPath, path, $"The path '{path}' is not valid ({ex.Message})");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static Result<PlacementLedger, AppError> Parse(string path, string text)
    {
        var dtos = JsonSerializer.Deserialize<LedgerEntryDto?[]>(text, ReadOptions);

        if (dtos is null)
        {
            return Fail<PlacementLedger>(ErrorCode.MalformedContent, path,
                $"Malformed JSON in '{path}': expected an array of ledger entries but found null.");
        }

        var entries = ImmutableArray.CreateBuilder<LedgerEntry>();
        var problems = new List<string>();

        for (var i = 0; i < dtos.Length; i++)
        {
            var number = i + 1;
            var dto = dtos[i];

            if (dto is null)
            {
                problems.Add($"Entry {number} is null.");
                continue;
            }

            var (entry, problem) = ToEntry(dto);
            if (entry is not null)
            {
                entries.Add(entry);
            }
            else
            {
                problems.Add($"Entry {number}: {problem}");
            }
        }

        if (problems.Count > 0)
        {
            return Fail<PlacementLedger>(ErrorCode.MalformedContent, path,
                $"{problems.Count} invalid entr(y/ies) in '{path}': " + string.Join(" ", problems));
        }

        return PlacementLedger.From(entries.ToImmutable()).Match(
            ledger => Outcomes.Ok(ledger),
            error => Fail<PlacementLedger>(ErrorCode.MalformedContent, path,
                $"Invalid ledger content in '{path}': {error.Message}"));
    }

    private static (LedgerEntry? Entry, string? Problem) ToEntry(LedgerEntryDto dto)
    {
        if (dto is not
            {
                ApplicationId: { } id,
                ApplicantName: { } name,
                Race: { } race,
                Job: { } job,
                Score: { } score,
                ApprovedBy: { } officer,
                StartDate: { } startText,
                FeeCents: { } fee
            }
            || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(race)
            || string.IsNullOrWhiteSpace(job) || string.IsNullOrWhiteSpace(officer) || string.IsNullOrWhiteSpace(startText))
        {
            return (null, $"missing field(s) {string.Join(", ", MissingFields(dto))}.");
        }

        if (!DateOnly.TryParseExact(startText.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
        {
            return (null, $"StartDate '{startText}' is not a valid {DateFormat} date.");
        }

        if (score < 0 || score > 100)
        {
            return (null, $"Score {score} is outside the allowed range 0-100.");
        }

        if (fee < 0)
        {
            return (null, $"FeeCents {fee} must not be negative.");
        }

        return (new LedgerEntry(id.Trim(), name.Trim(), race.Trim(), job.Trim(), score, officer.Trim(), start, fee), null);
    }

    private static List<string> MissingFields(LedgerEntryDto dto)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.ApplicationId)) missing.Add("ApplicationId");
        if (string.IsNullOrWhiteSpace(dto.ApplicantName)) missing.Add("ApplicantName");
        if (string.IsNullOrWhiteSpace(dto.Race)) missing.Add("Race");
        if (string.IsNullOrWhiteSpace(dto.Job)) missing.Add("Job");
        if (dto.Score is null) missing.Add("Score");
        if (string.IsNullOrWhiteSpace(dto.ApprovedBy)) missing.Add("ApprovedBy");
        if (string.IsNullOrWhiteSpace(dto.StartDate)) missing.Add("StartDate");
        if (dto.FeeCents is null) missing.Add("FeeCents");
        return missing;
    }

    private static LedgerEntryDto ToDto(LedgerEntry entry) =>
        new(
            entry.ApplicationId,
            entry.ApplicantName,
            entry.Race,
            entry.Job,
            entry.Score,
            entry.ApprovedBy,
            entry.StartDate.ToString(DateFormat, CultureInfo.InvariantCulture),
            entry.FeeCents);

    private static Result<T, AppError> Fail<T>(ErrorCode code, string subject, string message) where T : notnull =>
        Outcomes.Operational<T>(code, subject, message);
}
