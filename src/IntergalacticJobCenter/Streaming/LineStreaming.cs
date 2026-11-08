namespace IntergalacticJobCenter.Streaming;

public static class LineStreaming
{
    public static IEnumerable<string> ReadLines(Func<TextReader> openReader)
    {
        ArgumentNullException.ThrowIfNull(openReader);
        return Iterate(openReader);
    }

    public static IEnumerable<string> ReadFileLines(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ReadLines(() => new StreamReader(path));
    }

    private static IEnumerable<string> Iterate(Func<TextReader> openReader)
    {
        using var reader = openReader();

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
