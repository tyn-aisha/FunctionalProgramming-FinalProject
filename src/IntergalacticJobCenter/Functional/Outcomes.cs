namespace IntergalacticJobCenter.Functional;

public static class Outcomes
{
    public static Result<T, AppError> Ok<T>(T value) where T : notnull =>
        Result<T, AppError>.Success(value);

    public static Result<T, AppError> Fail<T>(AppError error) where T : notnull =>
        Result<T, AppError>.Failure(error);

    public static Result<T, AppError> Domain<T>(ErrorCode code, string subject, string message) where T : notnull =>
        Fail<T>(new AppError.Domain(code, subject, message));

    public static Result<T, AppError> Operational<T>(ErrorCode code, string subject, string message) where T : notnull =>
        Fail<T>(new AppError.Operational(code, subject, message));
}
