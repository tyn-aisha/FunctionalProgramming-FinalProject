using System.Collections.Immutable;
using System.Runtime.InteropServices;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Native;
using IntergalacticJobCenter.Queries;
using IntergalacticJobCenter.Reports;

namespace IntergalacticJobCenter.Tests;

public sealed class NativeIntegrationTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [DllImport("ijc_native_library_that_does_not_exist", CallingConvention = CallingConvention.Cdecl)]
    private static extern int MissingFee(int score, int tentacles);

    private static int Fee(Result<int, AppError> result) => Assert.IsType<Result<int, AppError>.Ok>(result).Value;

    private static AppError Error<T>(Result<T, AppError> result) where T : notnull =>
        Assert.IsType<Result<T, AppError>.Err>(result).Error;

    [Theory]
    [InlineData(90, 6, 91500)]
    [InlineData(100, 0, 85000)]
    [InlineData(65, 10, 81250)]
    [InlineData(89, 0, 72250)]
    [InlineData(0, 0, 50000)]
    [InlineData(100, 64, 181000)]
    public void NativeCall_ReturnsTheManuallyCalculatedFee(int score, int tentacles, int expectedCents)
    {
        Assert.Equal(expectedCents, Fee(FeeAdapter.Native.Calculate(score, tentacles)));
    }

    [Theory]
    [InlineData(-1, 0, "score")]
    [InlineData(101, 0, "score")]
    [InlineData(50, -1, "tentacles")]
    [InlineData(50, 65, "tentacles")]
    public void Adapter_InvalidDomainInput_IsRejectedBeforeTheNativeCall(int score, int tentacles, string subject)
    {
        var nativeCalls = 0;
        var adapter = new FeeAdapter((s, t) =>
        {
            nativeCalls++;
            return 0;
        });

        var error = Error(adapter.Calculate(score, tentacles));

        Assert.Equal(ErrorCode.InvalidInput, error.Code);
        Assert.IsType<AppError.Domain>(error);
        Assert.Equal(subject, error.Subject);
        Assert.Equal(0, nativeCalls);
    }

    [Fact]
    public void Adapter_RealFreshLoadFailure_BecomesAClearOperationalFailure()
    {
        var error = Error(new FeeAdapter(MissingFee).Calculate(90, 6));

        Assert.IsType<AppError.Operational>(error);
        Assert.Equal(ErrorCode.NativeLibraryUnavailable, error.Code);
        Assert.Equal("ijc_native", error.Subject);
        Assert.Contains("could not be loaded", error.Message);
    }

    [Fact]
    public void Adapter_InjectedBindingProblems_AreTranslated()
    {
        var wrongArchitecture = Error(new FeeAdapter((_, _) => throw new BadImageFormatException("wrong architecture")).Calculate(90, 6));
        var missingExport = Error(new FeeAdapter((_, _) => throw new EntryPointNotFoundException("no such export")).Calculate(90, 6));
        var unexpectedNegative = Error(new FeeAdapter((_, _) => -1).Calculate(90, 6));

        Assert.Equal(ErrorCode.NativeLibraryUnavailable, wrongArchitecture.Code);
        Assert.Equal(ErrorCode.NativeCallFailed, missingExport.Code);
        Assert.Equal(ErrorCode.NativeCallFailed, unexpectedNegative.Code);
        Assert.Contains("returned -1", unexpectedNegative.Message);
    }

    [Fact]
    public void Workflow_NormalPath_ReportsAndRealNativeFees_CompatibilityFocused()
    {
        var bundle = Assert.IsType<Result<ReportBundle, AppError>.Ok>(
            ReportWorkflow.Run(SampleData.Registry(), ScoreFunctions.CompatibilityFocused, 60, 20, FeeAdapter.Native)).Value;

        Assert.Equal(
            new[] { (1, "Mimi", 100, 85000), (2, "Zorp", 90, 91500), (3, "Blarg", 65, 81250), (4, "Grakk", 65, 84250) },
            bundle.Shortlist.Select(p => (p.Row.Rank, p.Row.Name, p.Row.Score, p.FeeCents)).ToArray());
        Assert.Equal(
            new[] { ("Calm", 2), ("Hostile", 2), ("Tense", 1) },
            bundle.Risk.Select(r => (r.Band, r.Count)).ToArray());
    }

    [Fact]
    public void Workflow_NormalPath_CalmnessFocused_GivesADifferentShortlistAndFees()
    {
        var bundle = Assert.IsType<Result<ReportBundle, AppError>.Ok>(
            ReportWorkflow.Run(SampleData.Registry(), ScoreFunctions.CalmnessFocused, 60, 20, FeeAdapter.Native)).Value;

        Assert.Equal(
            new[] { ("Mimi", 85000), ("Zorp", 91500), ("Vexa", 72750), ("Xylo", 69500) },
            bundle.Shortlist.Select(p => (p.Row.Name, p.FeeCents)).ToArray());
    }

    [Fact]
    public void Workflow_NormalPath_ExportsTheShortlistWithNativeFees()
    {
        var path = _temp.File("shortlist.csv");

        var summary = Assert.IsType<Result<ExportSummary, AppError>.Ok>(
            ReportWorkflow.RunAndExport(SampleData.Registry(), ScoreFunctions.CompatibilityFocused, 60, 20, FeeAdapter.Native, path)).Value;

        Assert.Equal(4, summary.RowsExported);
        Assert.Equal(
            new[]
            {
                "Rank,Name,Race,Score,Job,FeeCents",
                "1,Mimi,Mimikian,100,Front-Desk Receptionist,85000",
                "2,Zorp,Zorgian,90,Front-Desk Receptionist,91500",
                "3,Blarg,Blargonian,65,Multi-Task Operator,81250",
                "4,Grakk,Grakkian,65,Multi-Task Operator,84250"
            },
            File.ReadAllLines(path));
    }

    [Fact]
    public void Workflow_NativeFailure_PreservesState_ExportsNothing_AndReportsTheProblem()
    {
        var registry = SampleData.Registry();
        var namesBefore = registry.Applicants.Select(a => a.Name).ToArray();
        var path = _temp.Write("shortlist.csv", "PREVIOUS REPORT");
        var feeCalls = 0;
        var brokenNative = new FeeAdapter((_, _) =>
        {
            feeCalls++;
            throw new DllNotFoundException("simulated missing library");
        });

        var result = ReportWorkflow.RunAndExport(registry, ScoreFunctions.CompatibilityFocused, 60, 20, brokenNative, path);

        var error = Error(result);
        Assert.Equal(ErrorCode.NativeLibraryUnavailable, error.Code);
        Assert.Equal(1, feeCalls);
        Assert.Equal("PREVIOUS REPORT", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(namesBefore, registry.Applicants.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void Workflow_InvalidParameter_FailsBeforeAnyNativeCall()
    {
        var nativeCalls = 0;
        var adapter = new FeeAdapter((_, _) =>
        {
            nativeCalls++;
            return 1;
        });

        var error = Error(ReportWorkflow.Run(SampleData.Registry(), ScoreFunctions.CompatibilityFocused, 150, 20, adapter));

        Assert.Equal(ErrorCode.InvalidInput, error.Code);
        Assert.Equal(0, nativeCalls);
    }
}
