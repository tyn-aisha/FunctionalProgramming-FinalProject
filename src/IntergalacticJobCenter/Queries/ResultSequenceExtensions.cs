using System.Collections.Immutable;
using IntergalacticJobCenter.Functional;

namespace IntergalacticJobCenter.Queries;

public static class ResultSequenceExtensions
{

    public static Result<ImmutableArray<T>, E> Sequence<T, E>(this IEnumerable<Result<T, E>> results)
        where T : notnull
        where E : notnull
    {
        ArgumentNullException.ThrowIfNull(results);

        var values = ImmutableArray.CreateBuilder<T>();

        foreach (var result in results)
        {
            switch (result)
            {
                case Result<T, E>.Ok ok:
                    values.Add(ok.Value);
                    break;
                case Result<T, E>.Err err:
                    return Result<ImmutableArray<T>, E>.Failure(err.Error);
                default:
                    throw new InvalidOperationException($"Unhandled result case {result.GetType().Name}.");
            }
        }

        return Result<ImmutableArray<T>, E>.Success(values.ToImmutable());
    }
}
