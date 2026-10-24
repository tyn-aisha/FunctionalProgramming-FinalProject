namespace IntergalacticJobCenter.Functional;

public abstract record Result<T, E>
    where T : notnull
    where E : notnull
{
    private Result() { }

    public sealed record Ok : Result<T, E>
    {
        public T Value { get; }

        internal Ok(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Value = value;
        }
    }

    public sealed record Err : Result<T, E>
    {
        public E Error { get; }

        internal Err(E error)
        {
            ArgumentNullException.ThrowIfNull(error);
            Error = error;
        }
    }

    public static Result<T, E> Success(T value) => new Ok(value);

    public static Result<T, E> Failure(E error) => new Err(error);

    public bool IsOk => this is Ok;

    public Result<U, E> Map<U>(Func<T, U> map) where U : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return this switch
        {
            Ok ok => Result<U, E>.Success(map(ok.Value)),
            Err err => Result<U, E>.Failure(err.Error),
            _ => throw new InvalidOperationException($"Unhandled result case {GetType().Name}.")
        };
    }

    public Result<U, E> Bind<U>(Func<T, Result<U, E>> bind) where U : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        return this switch
        {
            Ok ok => bind(ok.Value) ?? throw new InvalidOperationException("The Bind function returned null instead of a Result."),
            Err err => Result<U, E>.Failure(err.Error),
            _ => throw new InvalidOperationException($"Unhandled result case {GetType().Name}.")
        };
    }

    public R Match<R>(Func<T, R> onOk, Func<E, R> onErr)
    {
        ArgumentNullException.ThrowIfNull(onOk);
        ArgumentNullException.ThrowIfNull(onErr);

        return this switch
        {
            Ok ok => onOk(ok.Value),
            Err err => onErr(err.Error),
            _ => throw new InvalidOperationException($"Unhandled result case {GetType().Name}.")
        };
    }
}
