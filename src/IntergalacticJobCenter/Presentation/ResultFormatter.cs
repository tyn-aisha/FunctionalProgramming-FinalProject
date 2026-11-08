using IntergalacticJobCenter.Functional;

namespace IntergalacticJobCenter.Presentation;

public static class ResultFormatter
{
    public static string Describe(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error switch
        {
            AppError.Domain domain => $"[DOMAIN {domain.Code}] {domain.Subject}: {domain.Message}",
            AppError.Operational operational => $"[OPERATIONAL {operational.Code}] {operational.Subject}: {operational.Message}",
            _ => throw new InvalidOperationException($"Unhandled error {error.GetType().Name}.")
        };
    }

    public static string Describe<T>(Result<T, AppError> result, Func<T, string> describeSuccess)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(describeSuccess);

        return result switch
        {
            Result<T, AppError>.Ok ok => "OK      " + describeSuccess(ok.Value),
            Result<T, AppError>.Err err => "FAILED  " + Describe(err.Error),
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}.")
        };
    }

    public static string DescribeMaybe<T>(Maybe<T> maybe, Func<T, string> describePresent)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(maybe);
        ArgumentNullException.ThrowIfNull(describePresent);

        return maybe switch
        {
            Maybe<T>.Present present => "present: " + describePresent(present.Value),
            Maybe<T>.Absent => "absent",
            _ => throw new InvalidOperationException($"Unhandled maybe {maybe.GetType().Name}.")
        };
    }
}
