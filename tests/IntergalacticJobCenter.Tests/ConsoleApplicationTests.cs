using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Native;
using IntergalacticJobCenter.Persistence;
using IntergalacticJobCenter.Presentation;

namespace IntergalacticJobCenter.Tests;

public sealed class ConsoleApplicationTests : IDisposable
{
    private const string DataJson =
        """[{"name":"Zorp","race":"Zorgian","tentacles":6,"eyes":2,"aggression":10,"compatibility":90},""" +
        """{"name":"Blarg","race":"Blargonian","tentacles":10,"eyes":8,"aggression":55,"compatibility":70},""" +
        """{"name":"Xylo","race":"Xylosian","tentacles":2,"eyes":2,"aggression":30,"compatibility":50},""" +
        """{"name":"Grakk","race":"Grakkian","tentacles":12,"eyes":4,"aggression":95,"compatibility":80},""" +
        """{"name":"Mimi","race":"Mimikian","tentacles":0,"eyes":0,"aggression":0,"compatibility":100},""" +
        """{"name":"Vexa","race":"Vexari","tentacles":4,"eyes":6,"aggression":20,"compatibility":15}]""";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private (int Code, string Output, AppPaths Paths) Run(
        string[] script, FeeAdapter? fees = null, bool writeData = true, string? ledgerContent = null)
    {
        var dataFile = writeData ? _temp.Write("data.json", DataJson) : _temp.File("data.json");
        var ledgerFile = ledgerContent is null ? _temp.File("ledger.json") : _temp.Write("ledger.json", ledgerContent);
        var paths = new AppPaths(dataFile, ledgerFile, _temp.File("shortlist.csv"));
        var output = new StringWriter();

        var code = new ConsoleApplication(
            new StringReader(string.Join("\n", script)),
            output,
            paths,
            new DateOnly(2026, 10, 18),
            fees ?? FeeAdapter.Native).Run();

        return (code, output.ToString(), paths);
    }

    [Fact]
    public void CompleteWorkflow_PlaceRecordAndShowTheLedger_WithTheRealNativeFee()
    {
        var (code, output, paths) = Run(new[]
        {
            "3", "Zorp", "office", "APP-001", "Officer Kim", "2026-10-25",
            "6", "Zorp", "office", "APP-001", "Officer Kim", "2026-10-25",
            "8",
            "0"
        });

        Assert.Equal(0, code);
        Assert.Contains("Zorp (APP-001) placed as Front-Desk Receptionist, score 90, approved by Officer Kim, starts 2026-10-25", output);
        Assert.Contains("Placement recorded: Zorp (APP-001) as Front-Desk Receptionist, fee 915.00 credits", output);
        Assert.Contains("by job:", output);

        var saved = Assert.IsType<Result<PlacementLedger, AppError>.Ok>(LedgerStore.Load(paths.LedgerFile)).Value;
        Assert.Equal(91500, Assert.Single(saved.Entries).FeeCents);
    }

    [Fact]
    public void CompleteWorkflow_ScreeningShortlistRiskAndExport()
    {
        var (code, output, paths) = Run(new[]
        {
            "2", "office",
            "4", "compat", "60", "20",
            "5", "20",
            "7", "compat", "", "60", "20",
            "0"
        });

        Assert.Equal(0, code);
        Assert.Contains("=== Office Safety Policy ===", output);
        Assert.Contains("Mimi", output);
        Assert.Contains("850.00 credits", output);
        Assert.Contains("Calm", output);
        Assert.Contains("4 rows written to", output);
        Assert.Equal(5, File.ReadAllLines(paths.ExportFile).Length);
    }

    [Fact]
    public void FailurePath_ErrorsAreReported_AndNothingIsChanged()
    {
        var brokenNative = new FeeAdapter((_, _) => throw new DllNotFoundException("missing"));
        var missingData = _temp.File("missing.json");

        var (code, output, paths) = Run(new[]
        {
            "3", "Nobody", "office", "APP-002", "Officer Kim", "2026-10-25",
            "3", "Zorp", "office", "APP-003", "Officer Kim", "tomorrow",
            "2", "mars",
            "1", missingData,
            "4", "custom", "0", "0", "60", "20",
            "6", "Zorp", "office", "APP-004", "Officer Kim", "2026-10-25",
            "8",
            "9",
            "0"
        }, brokenNative);

        Assert.Equal(0, code);
        Assert.Contains("ApplicantNotFound", output);
        Assert.Contains("not a valid yyyy-MM-dd date", output);
        Assert.Contains("PolicyNotFound", output);
        Assert.Contains("Previous registry kept (6 applicants).", output);
        Assert.Contains("At least one weight must be positive.", output);
        Assert.Contains("NativeLibraryUnavailable", output);
        Assert.Contains("The ledger is empty.", output);
        Assert.Contains("Unknown command '9'", output);
        Assert.False(File.Exists(paths.LedgerFile));
    }

    [Fact]
    public void DuplicateRecording_IsRefused_AndTheLedgerKeepsOneEntry()
    {
        var (_, output, paths) = Run(new[]
        {
            "6", "Zorp", "office", "APP-001", "Officer Kim", "2026-10-25",
            "6", "Mimi", "office", "APP-001", "Officer Kim", "2026-10-25",
            "0"
        });

        Assert.Contains("DuplicateApplication", output);
        var saved = Assert.IsType<Result<PlacementLedger, AppError>.Ok>(LedgerStore.Load(paths.LedgerFile)).Value;
        Assert.Single(saved.Entries);
    }

    [Fact]
    public void DamagedLedger_DisablesRecording_AndTheFileIsNotOverwritten()
    {
        var (_, output, paths) = Run(new[] { "6", "0" }, ledgerContent: "not json");

        Assert.Contains("Recording placements is disabled", output);
        Assert.Contains("Recording is disabled because the ledger file could not be read", output);
        Assert.Equal("not json", File.ReadAllText(paths.LedgerFile));
    }

    [Fact]
    public void MissingApplicantsFile_StartsEmptyVisibly()
    {
        var (_, output, _) = Run(new[] { "2", "office", "0" }, writeData: false);

        Assert.Contains("Applicants not loaded", output);
        Assert.Contains("FileNotFound", output);
        Assert.Contains("No eligible applicants.", output);
    }

    [Fact]
    public void EndOfInput_EndsGracefully_AlsoInTheMiddleOfACommand()
    {
        var (codeEmpty, outputEmpty, _) = Run(new string[0]);
        var (codeCut, outputCut, _) = Run(new[] { "3", "Zorp" });

        Assert.Equal(0, codeEmpty);
        Assert.Contains("End of input.", outputEmpty);
        Assert.Equal(0, codeCut);
        Assert.Contains("End of input.", outputCut);
    }

    [Theory]
    [InlineData("0", 0, 100, true)]
    [InlineData("100", 0, 100, true)]
    [InlineData("-1", 0, 100, false)]
    [InlineData("101", 0, 100, false)]
    [InlineData("abc", 0, 100, false)]
    [InlineData("", 0, 100, false)]
    [InlineData(null, 0, 100, false)]
    public void InputParsing_ParseInt_Boundaries(string? text, int min, int max, bool valid)
    {
        var result = InputParsing.ParseInt(text, "Value", min, max);

        Assert.Equal(valid, result.IsOk);
    }

    [Theory]
    [InlineData("2026-10-25", true)]
    [InlineData(" 2026-10-25 ", true)]
    [InlineData("25/10/2026", false)]
    [InlineData("2026-13-01", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void InputParsing_ParseDate(string? text, bool valid)
    {
        Assert.Equal(valid, InputParsing.ParseDate(text, "Start date").IsOk);
    }
}
