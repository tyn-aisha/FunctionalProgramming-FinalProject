namespace IntergalacticJobCenter.Functional;

public abstract record Maybe<T> where T : notnull
{
    private Maybe() { }

    public sealed record Present : Maybe<T>
    {
        public T Value { get; }

        internal Present(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Value = value;
        }
    }

    public sealed record Absent : Maybe<T>
    {
        internal Absent() { }
    }

    public static Maybe<T> Empty { get; } = new Absent();

    public static Maybe<T> Of(T value) => new Present(value);

    public bool IsPresent => this is Present;

    public Maybe<U> Map<U>(Func<T, U> map) where U : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return this switch
        {
            Present present => Maybe<U>.Of(map(present.Value)),
            Absent => Maybe<U>.Empty,
            _ => throw new InvalidOperationException($"Unhandled maybe case {GetType().Name}.")
        };
    }

    public Maybe<U> Bind<U>(Func<T, Maybe<U>> bind) where U : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        return this switch
        {
            Present present => bind(present.Value) ?? throw new InvalidOperationException("The Bind function returned null instead of a Maybe."),
            Absent => Maybe<U>.Empty,
            _ => throw new InvalidOperationException($"Unhandled maybe case {GetType().Name}.")
        };
    }

    public Result<T, E> ToResult<E>(Func<E> onAbsent) where E : notnull
    {
        ArgumentNullException.ThrowIfNull(onAbsent);

        return this switch
        {
            Present present => Result<T, E>.Success(present.Value),
            Absent => Result<T, E>.Failure(onAbsent()),
            _ => throw new InvalidOperationException($"Unhandled maybe case {GetType().Name}.")
        };
    }

    public R Match<R>(Func<T, R> onPresent, Func<R> onAbsent)
    {
        ArgumentNullException.ThrowIfNull(onPresent);
        ArgumentNullException.ThrowIfNull(onAbsent);

        return this switch
        {
            Present present => onPresent(present.Value),
            Absent => onAbsent(),
            _ => throw new InvalidOperationException($"Unhandled maybe case {GetType().Name}.")
        };
    }
}

public static class Maybe
{
    public static Maybe<T> Some<T>(T value) where T : notnull => Maybe<T>.Of(value);

    public static Maybe<T> None<T>() where T : notnull => Maybe<T>.Empty;

    public static Maybe<T> FromReference<T>(T? value) where T : class =>
        value is null ? Maybe<T>.Empty : Maybe<T>.Of(value);

    public static Maybe<T> FromNullable<T>(T? value) where T : struct =>
        value.HasValue ? Maybe<T>.Of(value.Value) : Maybe<T>.Empty;
}
