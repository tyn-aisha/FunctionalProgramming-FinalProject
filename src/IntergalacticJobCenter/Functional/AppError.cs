namespace IntergalacticJobCenter.Functional;

public abstract record AppError
{
    public ErrorCode Code { get; }
    public string Subject { get; }
    public string Message { get; }

    private AppError(ErrorCode code, string subject, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Subject = subject;
        Message = message;
    }

    public sealed record Domain : AppError
    {
        public Domain(ErrorCode code, string subject, string message) : base(code, subject, message) { }
    }

    public sealed record Operational : AppError
    {
        public Operational(ErrorCode code, string subject, string message) : base(code, subject, message) { }
    }
}
