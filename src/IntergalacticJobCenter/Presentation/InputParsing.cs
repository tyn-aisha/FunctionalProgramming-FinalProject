using System.Globalization;
using IntergalacticJobCenter.Functional;

namespace IntergalacticJobCenter.Presentation;

public static class InputParsing
{
    public static Result<int, AppError> ParseInt(string? text, string field, int min, int max)
    {
        if (!int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return Outcomes.Domain<int>(ErrorCode.InvalidInput, field, $"{field} '{text?.Trim()}' is not a whole number.");
        }

        return value < min || value > max
            ? Outcomes.Domain<int>(ErrorCode.InvalidInput, field, $"{field} {value} is outside the allowed range {min}-{max}.")
            : Outcomes.Ok(value);
    }

    public static Result<DateOnly, AppError> ParseDate(string? text, string field) =>
        DateOnly.TryParseExact(text?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? Outcomes.Ok(date)
            : Outcomes.Domain<DateOnly>(ErrorCode.InvalidInput, field, $"{field} '{text?.Trim()}' is not a valid yyyy-MM-dd date.");
}
