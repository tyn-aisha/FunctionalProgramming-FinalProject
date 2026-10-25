using System.Collections.Immutable;
using System.Text.Json;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Input;
using IntergalacticJobCenter.Lookup;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Streaming;

namespace IntergalacticJobCenter.Boundary;

public static class ApplicantFileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static Result<ImmutableArray<AlienApplicant>, AppError> Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail(ErrorCode.InvalidPath, "(none)", "No input file path was provided.");
        }

        try
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is not (".csv" or ".json"))
            {
                return Fail(ErrorCode.UnsupportedFormat, path,
                    $"Unsupported file type '{extension}' for '{path}'. Use .csv or .json.");
            }

            return extension == ".csv"
                ? ParseCsv(path, LineStreaming.ReadFileLines(path))
                : ParseJson(path, File.ReadAllText(path));
        }
        catch (FileNotFoundException ex)
        {
            return Fail(ErrorCode.FileNotFound, path, $"File not found: '{path}' ({ex.Message})");
        }
        catch (DirectoryNotFoundException ex)
        {
            return Fail(ErrorCode.FileNotFound, path, $"Directory not found for '{path}' ({ex.Message})");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Fail(ErrorCode.FileUnreadable, path, $"Access to '{path}' was denied ({ex.Message})");
        }
        catch (IOException ex)
        {
            return Fail(ErrorCode.FileUnreadable, path, $"Could not read '{path}' ({ex.Message})");
        }
        catch (JsonException ex)
        {
            return Fail(ErrorCode.MalformedContent, path, $"Malformed JSON in '{path}': {ex.Message}");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Fail(ErrorCode.InvalidPath, path, $"The path '{path}' is not valid ({ex.Message})");
        }
    }

    public static ReloadOutcome Reload(ApplicantRegistry current, string? path)
    {
        ArgumentNullException.ThrowIfNull(current);

        return Load(path).Match<ReloadOutcome>(
            applicants => new ReloadOutcome.Reloaded(ApplicantRegistry.From(applicants)),
            error => new ReloadOutcome.Kept(current, error));
    }

    private static Result<ImmutableArray<AlienApplicant>, AppError> ParseCsv(string path, IEnumerable<string> lines)
    {
        var parsed = ApplicantParser.ParseLines(lines);

        return parsed.Failures.IsEmpty
            ? Outcomes.Ok(parsed.Applicants)
            : Fail(ErrorCode.MalformedContent, path,
                $"{parsed.Failures.Length} invalid row(s) in '{path}': " + string.Join(" ", parsed.Failures));
    }

    private static Result<ImmutableArray<AlienApplicant>, AppError> ParseJson(string path, string text)
    {

        var entries = JsonSerializer.Deserialize<ApplicantDto?[]>(text, JsonOptions);

        if (entries is null)
        {
            return Fail(ErrorCode.MalformedContent, path,
                $"Malformed JSON in '{path}': expected an array of applicants but found null.");
        }

        var applicants = ImmutableArray.CreateBuilder<AlienApplicant>();
        var problems = new List<string>();

        for (var i = 0; i < entries.Length; i++)
        {
            var number = i + 1;
            var entry = entries[i];

            if (entry is null)
            {
                problems.Add($"Entry {number} is null.");
            }
            else if (entry is { Tentacles: { } tentacles, Eyes: { } eyes, Aggression: { } aggression, Compatibility: { } compatibility })
            {
                switch (AlienApplicant.Create(entry.Name?.Trim(), entry.Race?.Trim(), tentacles, eyes, aggression, compatibility))
                {
                    case ApplicantCreation.Valid valid:
                        applicants.Add(valid.Applicant);
                        break;
                    case ApplicantCreation.Invalid invalid:
                        problems.Add($"Entry {number}: {string.Join(" ", invalid.Reasons)}");
                        break;
                    default:
                        throw new InvalidOperationException("Unhandled applicant creation outcome.");
                }
            }
            else
            {
                problems.Add($"Entry {number}: missing field(s) {string.Join(", ", MissingFields(entry))}.");
            }
        }

        return problems.Count == 0
            ? Outcomes.Ok(applicants.ToImmutable())
            : Fail(ErrorCode.MalformedContent, path,
                $"{problems.Count} invalid entr(y/ies) in '{path}': " + string.Join(" ", problems));
    }

    private static List<string> MissingFields(ApplicantDto entry)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(entry.Name)) missing.Add("Name");
        if (string.IsNullOrWhiteSpace(entry.Race)) missing.Add("Race");
        if (entry.Tentacles is null) missing.Add("Tentacles");
        if (entry.Eyes is null) missing.Add("Eyes");
        if (entry.Aggression is null) missing.Add("Aggression");
        if (entry.Compatibility is null) missing.Add("Compatibility");
        return missing;
    }

    private static Result<ImmutableArray<AlienApplicant>, AppError> Fail(ErrorCode code, string subject, string message) =>
        Outcomes.Operational<ImmutableArray<AlienApplicant>>(code, subject, message);
}
