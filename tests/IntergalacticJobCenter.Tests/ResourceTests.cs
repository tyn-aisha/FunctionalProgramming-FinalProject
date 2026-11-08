using System.Collections.Immutable;
using IntergalacticJobCenter.Boundary;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Reports;
using IntergalacticJobCenter.Streaming;

namespace IntergalacticJobCenter.Tests;

public sealed class ResourceTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private sealed class TrackingReader : StringReader
    {
        public bool Disposed { get; private set; }

        public TrackingReader(string text) : base(text) { }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FailingReader : TextReader
    {
        private int _reads;

        public bool Disposed { get; private set; }

        public override string? ReadLine()
        {
            _reads++;
            return _reads == 1 ? "first line" : throw new IOException("simulated disk error");
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void LineStreaming_NothingIsOpenedBeforeEnumeration()
    {
        var opened = 0;
        var lines = LineStreaming.ReadLines(() =>
        {
            opened++;
            return new StringReader("a\nb");
        });

        Assert.Equal(0, opened);

        Assert.Equal(new[] { "a", "b" }, lines.ToArray());
        Assert.Equal(1, opened);
    }

    [Fact]
    public void LineStreaming_ReaderIsDisposed_AfterFullEnumeration()
    {
        var reader = new TrackingReader("a\nb\nc");

        var lines = LineStreaming.ReadLines(() => reader).ToArray();

        Assert.Equal(new[] { "a", "b", "c" }, lines);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void LineStreaming_ReaderIsDisposed_WhenTheConsumerStopsEarly()
    {
        var reader = new TrackingReader("a\nb\nc");

        var first = LineStreaming.ReadLines(() => reader).Take(1).ToArray();

        Assert.Equal(new[] { "a" }, first);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void LineStreaming_ReaderIsDisposed_WhenReadingFails()
    {
        var reader = new FailingReader();

        Assert.Throws<IOException>(() => LineStreaming.ReadLines(() => reader).ToArray());

        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Loader_ReleasesTheCsvFile_AfterSuccessAndAfterMalformedContent()
    {
        var good = _temp.Write("good.csv", "Name,Race,Tentacles,Eyes,Aggression,Compatibility\nAnn,Annite,0,2,0,80\n");
        var bad = _temp.Write("bad.csv", "Name,Race,Tentacles,Eyes,Aggression,Compatibility\nBo,Boid,abc,4,12,65\n");

        Assert.IsType<Result<ImmutableArray<IntergalacticJobCenter.Models.AlienApplicant>, AppError>.Ok>(ApplicantFileLoader.Load(good));
        Assert.IsType<Result<ImmutableArray<IntergalacticJobCenter.Models.AlienApplicant>, AppError>.Err>(ApplicantFileLoader.Load(bad));

        using var goodExclusive = new FileStream(good, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var badExclusive = new FileStream(bad, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Loader_MissingCsv_StillBecomesAFileNotFoundFailure_WithTheStreamingReader()
    {
        var result = ApplicantFileLoader.Load(_temp.File("absent.csv"));

        var error = Assert.IsType<Result<ImmutableArray<IntergalacticJobCenter.Models.AlienApplicant>, AppError>.Err>(result).Error;
        Assert.Equal(ErrorCode.FileNotFound, error.Code);
    }

    private static ImmutableArray<PricedRow> Rows(params PricedRow[] rows) => rows.ToImmutableArray();

    [Fact]
    public void Export_Success_WritesTheCsv_LeavesNoTempFile_AndReleasesTheFile()
    {
        var path = _temp.File("report.csv");
        var rows = Rows(
            new PricedRow(new ShortlistRow(1, "Mimi", "Mimikian", 0, 100, "Front-Desk Receptionist"), 85000),
            new PricedRow(new ShortlistRow(2, "Zorp", "Zorgian", 6, 90, "Front-Desk Receptionist"), 91500));

        var result = ReportExporter.Export(path, rows);

        Assert.Equal(2, Assert.IsType<Result<int, AppError>.Ok>(result).Value);
        Assert.Equal(
            new[]
            {
                "Rank,Name,Race,Score,Job,FeeCents",
                "1,Mimi,Mimikian,100,Front-Desk Receptionist,85000",
                "2,Zorp,Zorgian,90,Front-Desk Receptionist,91500"
            },
            File.ReadAllLines(path));
        Assert.False(File.Exists(path + ".tmp"));
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Export_EscapesCommasAndQuotes()
    {
        var path = _temp.File("escaped.csv");
        var rows = Rows(new PricedRow(new ShortlistRow(1, "A,B", "Say \"hi\"", 1, 90, "Job"), 123));

        Assert.IsType<Result<int, AppError>.Ok>(ReportExporter.Export(path, rows));

        Assert.Equal("1,\"A,B\",\"Say \"\"hi\"\"\",90,Job,123", File.ReadAllLines(path)[1]);
    }

    [Fact]
    public void Export_EmptyShortlist_WritesOnlyTheHeader()
    {
        var path = _temp.File("empty.csv");

        var result = ReportExporter.Export(path, ImmutableArray<PricedRow>.Empty);

        Assert.Equal(0, Assert.IsType<Result<int, AppError>.Ok>(result).Value);
        Assert.Equal(new[] { "Rank,Name,Race,Score,Job,FeeCents" }, File.ReadAllLines(path));
    }

    [Fact]
    public void Export_MissingDirectory_IsAClearFailure_AndNothingIsLeftBehind()
    {
        var path = Path.Combine(_temp.Path, "no-such-directory", "report.csv");

        var result = ReportExporter.Export(path, ImmutableArray<PricedRow>.Empty);

        var error = Assert.IsType<Result<int, AppError>.Err>(result).Error;
        Assert.Equal(ErrorCode.FileWriteFailed, error.Code);
        Assert.Equal(path, error.Subject);
        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Export_Failure_PreservesAnExistingReport()
    {
        var path = _temp.Write("report.csv", "OLD CONTENT");
        Directory.CreateDirectory(path + ".tmp");

        var result = ReportExporter.Export(path, Rows(new PricedRow(new ShortlistRow(1, "Mimi", "Mimikian", 0, 100, "Job"), 1)));

        Assert.Equal(ErrorCode.FileWriteFailed, Assert.IsType<Result<int, AppError>.Err>(result).Error.Code);
        Assert.Equal("OLD CONTENT", File.ReadAllText(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Export_BlankPath_IsRejected(string? path)
    {
        var error = Assert.IsType<Result<int, AppError>.Err>(ReportExporter.Export(path, ImmutableArray<PricedRow>.Empty)).Error;

        Assert.Equal(ErrorCode.InvalidPath, error.Code);
    }
}
