namespace IntergalacticJobCenter.Input;

public static class ApplicantFileReader
{
    public static IReadOnlyList<string> ReadLines(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Input file path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Input file not found: {path}", path);
        }

        return File.ReadAllLines(path);
    }
}
