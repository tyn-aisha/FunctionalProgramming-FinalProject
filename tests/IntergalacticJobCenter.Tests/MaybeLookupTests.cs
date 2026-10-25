using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;

namespace IntergalacticJobCenter.Tests;

public class MaybeLookupTests
{
    private static ApplicantRegistry Registry() => ApplicantRegistry.From(new[]
    {
        TestApplicants.Make("Zorp", "Zorgian", 6, 2, 10, 90),
        TestApplicants.Make("Mimi", "Mimikian", 0, 0, 0, 100)
    });

    [Fact]
    public void Lookup_PresentAndAbsent_BothWork()
    {
        var registry = Registry();

        var present = registry.Find("  zorp ");
        var absent = registry.Find("Nobody");

        Assert.Equal("Zorp", Assert.IsType<Maybe<IntergalacticJobCenter.Models.AlienApplicant>.Present>(present).Value.Name);
        Assert.IsType<Maybe<IntergalacticJobCenter.Models.AlienApplicant>.Absent>(absent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Lookup_BlankNameIsSimplyAbsent_NotAnException(string? name)
    {
        Assert.False(Registry().Find(name).IsPresent);
    }

    [Fact]
    public void Lookup_ZeroAggressionIsPresent_NotAbsent()
    {
        var registry = Registry();

        var mimi = registry.FindAggression("Mimi");
        var nobody = registry.FindAggression("Nobody");

        Assert.Equal(0, Assert.IsType<Maybe<int>.Present>(mimi).Value);
        Assert.IsType<Maybe<int>.Absent>(nobody);
    }

    [Fact]
    public void Maybe_ZeroFalseAndEmptyStringArePresent_OnlyNullIsAbsent()
    {
        Assert.True(Maybe.Some(0).IsPresent);
        Assert.True(Maybe.Some(false).IsPresent);
        Assert.True(Maybe.Some("").IsPresent);

        Assert.True(Maybe.FromNullable<int>(0).IsPresent);
        Assert.True(Maybe.FromNullable<bool>(false).IsPresent);
        Assert.False(Maybe.FromNullable<int>(null).IsPresent);

        Assert.True(Maybe.FromReference<string>("").IsPresent);
        Assert.False(Maybe.FromReference<string>(null).IsPresent);

        Assert.Throws<ArgumentNullException>(() => Maybe.Some<string>(null!));
    }

    [Fact]
    public void Maybe_MapAndBind_AreSkippedWhenAbsent_AndRunWhenPresent()
    {
        var calls = 0;

        var absentMapped = Maybe<int>.Empty.Map(x => { calls++; return x + 1; });
        var absentBound = Maybe<int>.Empty.Bind(x => { calls++; return Maybe.Some(x + 1); });
        var presentMapped = Maybe.Some(0).Map(x => x + 10);

        Assert.Equal(0, calls);
        Assert.False(absentMapped.IsPresent);
        Assert.False(absentBound.IsPresent);
        Assert.Equal(10, Assert.IsType<Maybe<int>.Present>(presentMapped).Value);
    }

    [Fact]
    public void RequiredLookup_MissingBecomesAFailure_AndTheErrorIsOnlyBuiltWhenNeeded()
    {
        var errorsBuilt = 0;
        AppError MakeError()
        {
            errorsBuilt++;
            return new AppError.Domain(ErrorCode.ApplicantNotFound, "Nobody", "Applicant 'Nobody' is not in the registry.");
        }

        var found = Registry().Find("Zorp").ToResult<AppError>(MakeError);
        var missing = Registry().Find("Nobody").ToResult<AppError>(MakeError);

        Assert.True(found.IsOk);
        Assert.Equal(1, errorsBuilt);
        var error = Assert.IsType<Result<IntergalacticJobCenter.Models.AlienApplicant, AppError>.Err>(missing).Error;
        Assert.Equal(ErrorCode.ApplicantNotFound, error.Code);
        Assert.Equal("Nobody", error.Subject);
    }

    [Fact]
    public void PolicyCatalog_PresentAndAbsent()
    {
        Assert.Equal("Office Safety Policy",
            Assert.IsType<Maybe<IntergalacticJobCenter.Policies.ScoringPolicy>.Present>(PolicyCatalog.Find(" OFFICE ")).Value.Name);
        Assert.False(PolicyCatalog.Find("extreme").Match(_ => false, () => true));
        Assert.False(PolicyCatalog.Find("mars").IsPresent);
        Assert.False(PolicyCatalog.Find(null).IsPresent);
    }

    [Fact]
    public void Registry_IsImmutable_AndFirstDuplicateWins()
    {
        var input = new List<IntergalacticJobCenter.Models.AlienApplicant>
        {
            TestApplicants.Make("Zorp", "Zorgian", 6, 2, 10, 90),
            TestApplicants.Make("ZORP", "Other", 1, 1, 1, 99)
        };

        var registry = ApplicantRegistry.From(input);
        input.Clear();

        Assert.Equal(1, registry.Count);
        Assert.Equal("Zorgian", Assert.IsType<Maybe<IntergalacticJobCenter.Models.AlienApplicant>.Present>(registry.Find("zorp")).Value.Race);
    }
}
