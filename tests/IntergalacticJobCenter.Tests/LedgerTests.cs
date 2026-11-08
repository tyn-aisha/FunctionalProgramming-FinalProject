using System.Collections.Immutable;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Native;
using IntergalacticJobCenter.Persistence;
using IntergalacticJobCenter.Policies;
using IntergalacticJobCenter.Reports;
using IntergalacticJobCenter.Services;

namespace IntergalacticJobCenter.Tests;

public sealed class LedgerTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static LedgerEntry Entry(
        string id, string name, string job = "General Clerk", int fee = 69500, int score = 66, int startDay = 25) =>
        new(id, name, "Testian", job, score, "Officer Kim", new DateOnly(2026, 10, startDay), fee);

    private static PlacementLedger Ledger(params LedgerEntry[] entries) =>
        Assert.IsType<Result<PlacementLedger, AppError>.Ok>(PlacementLedger.From(entries)).Value;

    private static AppError Failure<T>(Result<T, AppError> result) where T : notnull =>
        Assert.IsType<Result<T, AppError>.Err>(result).Error;

    private static T Success<T>(Result<T, AppError> result) where T : notnull =>
        Assert.IsType<Result<T, AppError>.Ok>(result).Value;

    [Fact]
    public void Add_ReturnsANewLedger_AndLeavesTheOriginalUnchanged()
    {
        var original = PlacementLedger.Empty;

        var updated = Success(original.Add(Entry("APP-001", "Zorp")));

        Assert.Equal(0, original.Count);
        Assert.Equal(1, updated.Count);
        Assert.NotSame(original, updated);
    }

    [Fact]
    public void Add_DuplicateApplicationId_IsADomainFailure()
    {
        var ledger = Ledger(Entry("APP-001", "Zorp"));

        var error = Failure(ledger.Add(Entry("app-001", "Mimi")));

        Assert.IsType<AppError.Domain>(error);
        Assert.Equal(ErrorCode.DuplicateApplication, error.Code);
        Assert.Equal("app-001", error.Subject);
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public void Add_ApplicantAlreadyPlaced_IsADomainFailure()
    {
        var ledger = Ledger(Entry("APP-001", "Zorp"));

        var error = Failure(ledger.Add(Entry("APP-002", "ZORP")));

        Assert.Equal(ErrorCode.ApplicantAlreadyPlaced, error.Code);
        Assert.Equal("ZORP", error.Subject);
    }

    [Fact]
    public void From_EmptyIsValid_AndDuplicatesStopTheBuild()
    {
        Assert.Equal(0, Success(PlacementLedger.From(Array.Empty<LedgerEntry>())).Count);

        var duplicated = PlacementLedger.From(new[]
        {
            Entry("APP-001", "Zorp"),
            Entry("APP-001", "Mimi"),
            Entry("APP-003", "Vexa")
        });

        Assert.Equal(ErrorCode.DuplicateApplication, Failure(duplicated).Code);
    }

    [Fact]
    public void JobSummary_GroupsTotalsAndOrdersTiesByJobName()
    {
        var ledger = Ledger(
            Entry("APP-001", "Mimi", "Front-Desk Receptionist", 85000, 100, 20),
            Entry("APP-002", "Zorp", "Front-Desk Receptionist", 91500, 90, 27),
            Entry("APP-003", "Blarg", "Multi-Task Operator", 81250, 65, 22),
            Entry("APP-004", "Xylo", "General Clerk", 69500, 66, 21),
            Entry("APP-005", "Vexa", "Surveillance Analyst", 69500, 67, 23));

        var rows = LedgerReport.ByJob(ledger);

        Assert.Equal(
            new[]
            {
                ("Front-Desk Receptionist", 2, 176500, new DateOnly(2026, 10, 27)),
                ("Multi-Task Operator", 1, 81250, new DateOnly(2026, 10, 22)),
                ("General Clerk", 1, 69500, new DateOnly(2026, 10, 21)),
                ("Surveillance Analyst", 1, 69500, new DateOnly(2026, 10, 23))
            },
            rows.Select(r => (r.Job, r.Count, r.TotalFeeCents, r.LatestStart)).ToArray());
        Assert.Empty(LedgerReport.ByJob(PlacementLedger.Empty));
    }

    [Fact]
    public void Store_SaveThenLoad_RoundTripsTheEntries()
    {
        var path = _temp.File("ledger.json");
        var ledger = Ledger(Entry("APP-001", "Zorp", "Front-Desk Receptionist", 91500, 90), Entry("APP-002", "Mimi"));

        Assert.Equal(2, Success(LedgerStore.Save(path, ledger)));
        var loaded = Success(LedgerStore.Load(path));

        Assert.Equal(ledger.Entries.ToArray(), loaded.Entries.ToArray());
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Store_Save_CreatesAMissingDirectory()
    {
        var path = Path.Combine(_temp.Path, "nested", "deeper", "ledger.json");

        Success(LedgerStore.Save(path, Ledger(Entry("APP-001", "Zorp"))));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Store_MissingFile_IsReported_AndLoadOrStartStartsEmptyVisibly()
    {
        var path = _temp.File("absent.json");

        var error = Failure(LedgerStore.Load(path));
        var started = Success(LedgerStore.LoadOrStart(path));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
        Assert.IsType<AppError.Operational>(error);
        Assert.True(started.StartedEmpty);
        Assert.Equal(0, started.Ledger.Count);
    }

    [Fact]
    public void Store_LoadOrStart_ExistingFileIsLoadedNotReplaced()
    {
        var path = _temp.File("ledger.json");
        Success(LedgerStore.Save(path, Ledger(Entry("APP-001", "Zorp"))));

        var loaded = Success(LedgerStore.LoadOrStart(path));

        Assert.False(loaded.StartedEmpty);
        Assert.Equal(1, loaded.Ledger.Count);
    }

    [Theory]
    [InlineData("[ { \"applicationId\": ")]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("{ \"applicationId\": \"APP-001\" }")]
    [InlineData("null")]
    public void Store_MalformedJson_IsAClearFailure_AndNotTreatedAsMissing(string content)
    {
        var path = _temp.Write("ledger.json", content);

        var error = Failure(LedgerStore.Load(path));
        var startResult = Failure(LedgerStore.LoadOrStart(path));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Equal(path, error.Subject);
        Assert.Equal(ErrorCode.MalformedContent, startResult.Code);
    }

    [Fact]
    public void Store_InvalidEntries_AreNamedByEntry()
    {
        const string content =
            """[{"applicationId":"APP-001","applicantName":"Zorp","race":"Zorgian","job":"General Clerk","score":66,"approvedBy":"Kim","startDate":"2026-10-25","feeCents":1},""" +
            """{"applicationId":"APP-002","applicantName":"Mimi","race":"Mimikian","job":"General Clerk","approvedBy":"Kim","startDate":"2026-10-25","feeCents":1},""" +
            """{"applicationId":"APP-003","applicantName":"Vexa","race":"Vexari","job":"General Clerk","score":66,"approvedBy":"Kim","startDate":"25/10/2026","feeCents":1},""" +
            """{"applicationId":"APP-004","applicantName":"Xylo","race":"Xylosian","job":"General Clerk","score":150,"approvedBy":"Kim","startDate":"2026-10-25","feeCents":1}]""";

        var error = Failure(LedgerStore.Load(_temp.Write("ledger.json", content)));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Contains("Entry 2: missing field(s) Score.", error.Message);
        Assert.Contains("Entry 3: StartDate '25/10/2026' is not a valid yyyy-MM-dd date.", error.Message);
        Assert.Contains("Entry 4: Score 150 is outside the allowed range 0-100.", error.Message);
    }

    [Fact]
    public void Store_DuplicateIdsInTheFile_AreMalformedContent()
    {
        const string content =
            """[{"applicationId":"APP-001","applicantName":"Zorp","race":"Zorgian","job":"General Clerk","score":66,"approvedBy":"Kim","startDate":"2026-10-25","feeCents":1},""" +
            """{"applicationId":"APP-001","applicantName":"Mimi","race":"Mimikian","job":"General Clerk","score":66,"approvedBy":"Kim","startDate":"2026-10-25","feeCents":1}]""";

        var error = Failure(LedgerStore.Load(_temp.Write("ledger.json", content)));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Contains("already recorded", error.Message);
    }

    [Fact]
    public void Store_SaveFailure_PreservesTheExistingFile()
    {
        var path = _temp.Write("ledger.json", "PREVIOUS LEDGER");
        Directory.CreateDirectory(path + ".tmp");

        var error = Failure(LedgerStore.Save(path, Ledger(Entry("APP-001", "Zorp"))));

        Assert.Equal(ErrorCode.FileWriteFailed, error.Code);
        Assert.Equal("PREVIOUS LEDGER", File.ReadAllText(path));
    }

    [Fact]
    public void Store_Save_BlankPathIsRejected()
    {
        Assert.Equal(ErrorCode.InvalidPath, Failure(LedgerStore.Save(" ", PlacementLedger.Empty)).Code);
        Assert.Equal(ErrorCode.InvalidPath, Failure(LedgerStore.Load(null)).Code);
    }

    private static PlacementRequest Request(string applicant, string id) =>
        new(applicant, "office", id, "Officer Kim", Today.AddDays(7), Today);

    [Fact]
    public void Desk_PlaceAndRecord_ReturnsTheEntryWithTheNativeFee_AndANewLedger()
    {
        var ledger = PlacementLedger.Empty;

        var recorded = Success(PlacementDesk.PlaceAndRecord(SampleData.Registry(), Request("Zorp", "APP-001"), FeeAdapter.Native, ledger));

        Assert.Equal(
            new LedgerEntry("APP-001", "Zorp", "Zorgian", "Front-Desk Receptionist", 90, "Officer Kim", new DateOnly(2026, 10, 25), 91500),
            recorded.Entry);
        Assert.Equal(1, recorded.Ledger.Count);
        Assert.Equal(0, ledger.Count);
    }

    [Fact]
    public void Desk_Failures_LeaveTheLedgerUnchanged()
    {
        var registry = SampleData.Registry();
        var ledger = Success(PlacementDesk.PlaceAndRecord(registry, Request("Zorp", "APP-001"), FeeAdapter.Native, PlacementLedger.Empty)).Ledger;
        var brokenNative = new FeeAdapter((_, _) => throw new DllNotFoundException("missing"));

        var duplicateId = Failure(PlacementDesk.PlaceAndRecord(registry, Request("Mimi", "APP-001"), FeeAdapter.Native, ledger));
        var samePerson = Failure(PlacementDesk.PlaceAndRecord(registry, Request("Zorp", "APP-009"), FeeAdapter.Native, ledger));
        var unknown = Failure(PlacementDesk.PlaceAndRecord(registry, Request("Nobody", "APP-010"), FeeAdapter.Native, ledger));
        var nativeDown = Failure(PlacementDesk.PlaceAndRecord(registry, Request("Mimi", "APP-011"), brokenNative, ledger));

        Assert.Equal(ErrorCode.DuplicateApplication, duplicateId.Code);
        Assert.Equal(ErrorCode.ApplicantAlreadyPlaced, samePerson.Code);
        Assert.Equal(ErrorCode.ApplicantNotFound, unknown.Code);
        Assert.Equal(ErrorCode.NativeLibraryUnavailable, nativeDown.Code);
        Assert.Equal(1, ledger.Count);
    }
}
