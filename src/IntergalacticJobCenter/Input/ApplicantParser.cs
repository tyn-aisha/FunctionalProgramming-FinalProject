using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using IntergalacticJobCenter.Models;

namespace IntergalacticJobCenter.Input;

public static class ApplicantParser
{
    public const int FieldCount = 6;

    public static ParseOutcome ParseLines(IEnumerable<string?>? lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var applicants = ImmutableArray.CreateBuilder<AlienApplicant>();
        var failures = ImmutableArray.CreateBuilder<ParseFailure>();
        var lineNumber = 0;
        var firstContentSeen = false;

        foreach (var line in lines)
        {
            lineNumber++;

            if (line is null)
            {
                failures.Add(new ParseFailure(lineNumber, "Line is missing (null)."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!firstContentSeen)
            {
                firstContentSeen = true;
                if (IsHeader(line))
                {
                    continue;
                }
            }

            if (TryParseLine(line, out var applicant, out var error))
            {
                applicants.Add(applicant);
            }
            else
            {
                failures.Add(new ParseFailure(lineNumber, error));
            }
        }

        return new ParseOutcome(applicants.ToImmutable(), failures.ToImmutable());
    }

    public static bool TryParseLine(
        string? line,
        [NotNullWhen(true)] out AlienApplicant? applicant,
        out string error)
    {
        applicant = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Line is empty.";
            return false;
        }

        var parts = line.Split(',');
        if (parts.Length != FieldCount)
        {
            error = $"Expected {FieldCount} comma-separated fields but found {parts.Length}.";
            return false;
        }

        if (!TryParseInt(parts[2], "Tentacles", out var tentacles, out error)) return false;
        if (!TryParseInt(parts[3], "Eyes", out var eyes, out error)) return false;
        if (!TryParseInt(parts[4], "Aggression", out var aggression, out error)) return false;
        if (!TryParseInt(parts[5], "Compatibility", out var compatibility, out error)) return false;

        switch (AlienApplicant.Create(parts[0].Trim(), parts[1].Trim(), tentacles, eyes, aggression, compatibility))
        {
            case ApplicantCreation.Valid valid:
                applicant = valid.Applicant;
                return true;
            case ApplicantCreation.Invalid invalid:
                error = string.Join(" ", invalid.Reasons);
                return false;
            default:
                throw new InvalidOperationException("Unhandled applicant creation outcome.");
        }
    }

    private static bool IsHeader(string line) =>
        line.TrimStart().StartsWith("name,", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseInt(string text, string field, out int value, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            error = $"{field} is missing.";
            return false;
        }

        var trimmed = text.Trim();
        if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = $"{field} '{trimmed}' is not a whole number.";
            return false;
        }

        return true;
    }
}
