using IntergalacticJobCenter.Boundary;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;

namespace IntergalacticJobCenter.Tests;

public sealed class FileBoundaryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ijc-tests-" + Guid.NewGuid().ToString("N"));

    public FileBoundaryTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string Write(string fileName, string content)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static ApplicantRegistry CurrentRegistry() => ApplicantRegistry.From(new[]
    {
        TestApplicants.Make("Zorp", "Zorgian", 6, 2, 10, 90),
        TestApplicants.Make("Mimi", "Mimikian", 0, 0, 0, 100)
    });

    private static AppError.Operational Problem(
        Result<System.Collections.Immutable.ImmutableArray<IntergalacticJobCenter.Models.AlienApplicant>, AppError> result) =>
        Assert.IsType<AppError.Operational>(
            Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<IntergalacticJobCenter.Models.AlienApplicant>, AppError>.Err>(result).Error);

    private const string ValidJson =
        """[{"name":"Ann","race":"Annite","tentacles":0,"eyes":2,"aggression":0,"compatibility":80},""" +
        """{"name":"Bo","race":"Boid","tentacles":4,"eyes":4,"aggression":12,"compatibility":65}]""";

    [Fact]
    public void Load_ValidCsvAndJson_Succeed()
    {
        var csv = Write("ok.csv", "Name,Race,Tentacles,Eyes,Aggression,Compatibility\r\nAnn,Annite,0,2,0,80\r\nBo,Boid,4,4,12,65\r\n");
        var json = Write("ok.json", ValidJson);

        var fromCsv = Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<IntergalacticJobCenter.Models.AlienApplicant>, AppError>.Ok>(ApplicantFileLoader.Load(csv)).Value;
        var fromJson = Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<IntergalacticJobCenter.Models.AlienApplicant>, AppError>.Ok>(ApplicantFileLoader.Load(json)).Value;

        Assert.Equal(new[] { "Ann", "Bo" }, fromCsv.Select(a => a.Name).ToArray());
        Assert.Equal(new[] { "Ann", "Bo" }, fromJson.Select(a => a.Name).ToArray());
        Assert.Equal(0, fromJson[0].Tentacles);
    }

    [Fact]
    public void Load_EmptyDatasets_AreValid_NotFailures()
    {
        var emptyJson = Write("empty.json", "[]");
        var headerOnly = Write("header.csv", "Name,Race,Tentacles,Eyes,Aggression,Compatibility\n");

        Assert.Empty(Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<IntergalacticJobCenter.Models.AlienApplicant>, AppError>.Ok>(ApplicantFileLoader.Load(emptyJson)).Value);
        Assert.Empty(Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<IntergalacticJobCenter.Models.AlienApplicant>, AppError>.Ok>(ApplicantFileLoader.Load(headerOnly)).Value);
    }

    [Fact]
    public void MissingFile_IsReportedWithTheFileName_AndTheRegistryIsPreserved()
    {
        var missing = Path.Combine(_directory, "does_not_exist.csv");
        var registry = CurrentRegistry();

        var problem = Problem(ApplicantFileLoader.Load(missing));
        var outcome = ApplicantFileLoader.Reload(registry, missing);

        Assert.Equal(ErrorCode.FileNotFound, problem.Code);
        Assert.Equal(missing, problem.Subject);
        Assert.Contains("File not found", problem.Message);

        var kept = Assert.IsType<ReloadOutcome.Kept>(outcome);
        Assert.Same(registry, kept.Registry);
        Assert.Same(registry, outcome.Current);
        Assert.Equal(ErrorCode.FileNotFound, kept.Problem.Code);
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void MissingDirectory_BlankPathAndUnsupportedType_AreAlsoClearOutcomes()
    {
        var inMissingDirectory = Path.Combine(_directory, "nope", "data.csv");
        var unsupported = Write("data.xml", "<applicants/>");

        Assert.Equal(ErrorCode.FileNotFound, Problem(ApplicantFileLoader.Load(inMissingDirectory)).Code);
        Assert.Equal(ErrorCode.InvalidPath, Problem(ApplicantFileLoader.Load("  ")).Code);
        Assert.Equal(ErrorCode.InvalidPath, Problem(ApplicantFileLoader.Load(null)).Code);
        Assert.Equal(ErrorCode.UnsupportedFormat, Problem(ApplicantFileLoader.Load(unsupported)).Code);
    }

    [Theory]
    [InlineData("[ { \"name\": \"Zorp\", \"race\": ")]
    [InlineData("this is not json")]
    [InlineData("")]
    [InlineData("{ \"name\": \"Zorp\" }")]
    [InlineData("[{\"name\":\"A\",\"race\":\"B\",\"tentacles\":\"six\",\"eyes\":1,\"aggression\":1,\"compatibility\":50}]")]
    public void MalformedJson_BecomesAClearFailure(string content)
    {
        var path = Write("bad.json", content);

        var problem = Problem(ApplicantFileLoader.Load(path));

        Assert.Equal(ErrorCode.MalformedContent, problem.Code);
        Assert.Equal(path, problem.Subject);
        Assert.Contains(path, problem.Message);
    }

    [Fact]
    public void MalformedJson_NullRoot_IsRejected()
    {
        var problem = Problem(ApplicantFileLoader.Load(Write("null.json", "null")));

        Assert.Equal(ErrorCode.MalformedContent, problem.Code);
    }

    [Fact]
    public void MalformedJson_MissingFieldAndOutOfRangeValue_AreNamedByEntry()
    {
        const string content =
            """[{"name":"A","race":"B","tentacles":1,"eyes":1,"aggression":1,"compatibility":50},""" +
            """{"name":"C","race":"D","eyes":1,"aggression":1,"compatibility":50},""" +
            """{"name":"E","race":"F","tentacles":1,"eyes":1,"aggression":1,"compatibility":150}]""";

        var problem = Problem(ApplicantFileLoader.Load(Write("fields.json", content)));

        Assert.Equal(ErrorCode.MalformedContent, problem.Code);
        Assert.Contains("Entry 2: missing field(s) Tentacles.", problem.Message);
        Assert.Contains("Entry 3: Compatibility 150 is outside the allowed range 0-100.", problem.Message);
    }

    [Fact]
    public void MalformedCsv_IsAllOrNothing_AndTheRegistryIsPreserved()
    {

        var path = Write("mixed.csv",
            "Name,Race,Tentacles,Eyes,Aggression,Compatibility\n" +
            "Ann,Annite,0,2,0,80\n" +
            "Bo,Boid,abc,4,12,65\n");
        var registry = CurrentRegistry();
        var namesBefore = registry.Applicants.Select(a => a.Name).ToArray();

        var problem = Problem(ApplicantFileLoader.Load(path));
        var outcome = ApplicantFileLoader.Reload(registry, path);

        Assert.Equal(ErrorCode.MalformedContent, problem.Code);
        Assert.Contains("Line 3: Tentacles 'abc' is not a whole number.", problem.Message);

        var kept = Assert.IsType<ReloadOutcome.Kept>(outcome);
        Assert.Same(registry, kept.Registry);
        Assert.Equal(namesBefore, kept.Registry.Applicants.Select(a => a.Name).ToArray());
        Assert.False(kept.Registry.Find("Ann").IsPresent);
    }

    [Fact]
    public void Reload_Success_BuildsANewRegistry_AndLeavesTheOldOneUntouched()
    {
        var path = Write("new.json", ValidJson);
        var old = CurrentRegistry();

        var outcome = ApplicantFileLoader.Reload(old, path);

        var reloaded = Assert.IsType<ReloadOutcome.Reloaded>(outcome);
        Assert.NotSame(old, reloaded.Registry);
        Assert.True(reloaded.Registry.Find("Ann").IsPresent);
        Assert.False(old.Find("Ann").IsPresent);
        Assert.Equal(2, old.Count);
    }
}
