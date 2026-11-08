using System.Collections.Immutable;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Queries;
using IntergalacticJobCenter.Reports;

namespace IntergalacticJobCenter.Tests;

public class ReportAndExtensionTests
{
    private static ImmutableArray<ShortlistRow> Shortlist(
        IEnumerable<AlienApplicant> applicants, Func<AlienApplicant, int> score, int minScore) =>
        Assert.IsType<Result<ImmutableArray<ShortlistRow>, AppError>.Ok>(
            ShortlistReport.Build(applicants, score, minScore)).Value;

    private static ImmutableArray<RiskBandRow> Risk(IEnumerable<AlienApplicant> applicants, int minCompatibility) =>
        Assert.IsType<Result<ImmutableArray<RiskBandRow>, AppError>.Ok>(
            RiskReport.Build(applicants, minCompatibility)).Value;

    private static (int Rank, string Name, int Score)[] Summary(ImmutableArray<ShortlistRow> rows) =>
        rows.Select(r => (r.Rank, r.Name, r.Score)).ToArray();

    [Fact]
    public void Shortlist_NormalResult_FilteredScoredRankedAndNumbered()
    {
        var rows = Shortlist(SampleData.Applicants(), ScoreFunctions.CompatibilityFocused, 60);

        Assert.Equal(
            new[] { (1, "Mimi", 100), (2, "Zorp", 90), (3, "Blarg", 65), (4, "Grakk", 65) },
            Summary(rows));
        Assert.Equal(
            new[] { "Front-Desk Receptionist", "Front-Desk Receptionist", "Multi-Task Operator", "Multi-Task Operator" },
            rows.Select(r => r.Job).ToArray());
    }

    [Fact]
    public void Shortlist_TiedScores_AreOrderedByName_RegardlessOfInputOrder()
    {
        var forward = Shortlist(SampleData.Applicants(), ScoreFunctions.CompatibilityFocused, 60);
        var reversed = Shortlist(Enumerable.Reverse(SampleData.Applicants()), ScoreFunctions.CompatibilityFocused, 60);

        Assert.Equal(Summary(forward), Summary(reversed));
        Assert.Equal(new[] { "Blarg", "Grakk" }, forward.Skip(2).Select(r => r.Name).ToArray());
    }

    [Fact]
    public void Shortlist_EmptyDataset_GivesAnEmptySuccess()
    {
        var rows = Shortlist(Array.Empty<AlienApplicant>(), ScoreFunctions.CompatibilityFocused, 60);

        Assert.Empty(rows);
    }

    [Fact]
    public void Shortlist_NoMatches_GivesAnEmptySuccess()
    {
        var weak = new[]
        {
            TestApplicants.Make("Xylo", "Xylosian", 2, 2, 30, 50),
            TestApplicants.Make("Vexa", "Vexari", 4, 6, 20, 15)
        };

        Assert.Empty(Shortlist(weak, ScoreFunctions.CompatibilityFocused, 60));
    }

    [Fact]
    public void Shortlist_ThresholdIsInclusive()
    {
        var atThreshold = Shortlist(SampleData.Applicants(), ScoreFunctions.CompatibilityFocused, 65);
        var aboveThreshold = Shortlist(SampleData.Applicants(), ScoreFunctions.CompatibilityFocused, 66);

        Assert.Equal(new[] { "Mimi", "Zorp", "Blarg", "Grakk" }, atThreshold.Select(r => r.Name).ToArray());
        Assert.Equal(new[] { "Mimi", "Zorp" }, aboveThreshold.Select(r => r.Name).ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Shortlist_InvalidThreshold_IsADomainFailure(int minScore)
    {
        var result = ShortlistReport.Build(SampleData.Applicants(), ScoreFunctions.CompatibilityFocused, minScore);

        var error = Assert.IsType<Result<ImmutableArray<ShortlistRow>, AppError>.Err>(result).Error;
        Assert.Equal(ErrorCode.InvalidInput, error.Code);
        Assert.Equal("minScore", error.Subject);
    }

    [Fact]
    public void Risk_NormalResult_GroupsCountsSumsAndAggregates()
    {
        var rows = Risk(SampleData.Applicants(), 20);

        Assert.Equal(
            new[]
            {
                ("Calm", 2, 6, 100, "Mimi, Zorp"),
                ("Hostile", 2, 22, 80, "Blarg, Grakk"),
                ("Tense", 1, 2, 50, "Xylo")
            },
            rows.Select(r => (r.Band, r.Count, r.TotalTentacles, r.MaxCompatibility, r.Names)).ToArray());
    }

    [Fact]
    public void Risk_TiedCounts_AreOrderedByBandName()
    {

        var bands = Risk(Enumerable.Reverse(SampleData.Applicants()), 20).Select(r => r.Band).ToArray();

        Assert.Equal(new[] { "Calm", "Hostile", "Tense" }, bands);
    }

    [Fact]
    public void Risk_WithoutFilter_IncludesLowCompatibilityApplicants()
    {
        var calm = Risk(SampleData.Applicants(), 0).Single(r => r.Band == "Calm");

        Assert.Equal((3, 10, 100, "Mimi, Vexa, Zorp"), (calm.Count, calm.TotalTentacles, calm.MaxCompatibility, calm.Names));
    }

    [Fact]
    public void Risk_EmptyDatasetAndNoMatches_GiveEmptySuccesses()
    {
        Assert.Empty(Risk(Array.Empty<AlienApplicant>(), 20));
        Assert.Empty(Risk(new[] { TestApplicants.Make("Zorp", "Zorgian", 6, 2, 10, 90) }, 100));
    }

    [Theory]
    [InlineData(0, "Calm")]
    [InlineData(20, "Calm")]
    [InlineData(21, "Tense")]
    [InlineData(50, "Tense")]
    [InlineData(51, "Hostile")]
    [InlineData(100, "Hostile")]
    public void Risk_BandBoundaries(int aggression, string expectedBand)
    {
        Assert.Equal(expectedBand, RiskReport.AggressionBand(aggression));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Risk_InvalidMinimumCompatibility_IsADomainFailure(int minCompatibility)
    {
        var error = Assert.IsType<Result<ImmutableArray<RiskBandRow>, AppError>.Err>(
            RiskReport.Build(SampleData.Applicants(), minCompatibility)).Error;

        Assert.Equal(ErrorCode.InvalidInput, error.Code);
        Assert.Equal("minCompatibility", error.Subject);
    }

    [Fact]
    public void Reports_DoNotModifyTheirInput()
    {
        var input = SampleData.Applicants();
        var snapshot = input.ToArray();

        _ = ShortlistReport.Build(input, ScoreFunctions.CompatibilityFocused, 60);
        _ = RiskReport.Build(input, 20);

        Assert.Equal(snapshot, input);
    }

    [Fact]
    public void Extension_AtLeast_IsInclusive_AndLeavesTheSourceUntouched()
    {
        var zorp = TestApplicants.Make("Zorp", "Zorgian", 6, 2, 10, 90);
        var source = new[] { new ScoredApplicant(zorp, 64), new ScoredApplicant(zorp, 65), new ScoredApplicant(zorp, 66) };

        var kept = source.AtLeast(65).Select(s => s.Score).ToArray();

        Assert.Equal(new[] { 65, 66 }, kept);
        Assert.Equal(3, source.Length);
    }

    [Fact]
    public void Extension_RankByScore_HighestFirst_TiesByOrdinalName()
    {
        var a = TestApplicants.Make("Beta", "R", 1, 1, 1, 50);
        var b = TestApplicants.Make("alpha", "R", 1, 1, 1, 50);
        var c = TestApplicants.Make("Alpha", "R", 1, 1, 1, 50);
        var d = TestApplicants.Make("Top", "R", 1, 1, 1, 50);
        var source = new[] { new ScoredApplicant(a, 70), new ScoredApplicant(b, 70), new ScoredApplicant(c, 70), new ScoredApplicant(d, 90) };

        var ranked = source.RankByScore().Select(s => s.Applicant.Name).ToArray();

        Assert.Equal(new[] { "Top", "Alpha", "Beta", "alpha" }, ranked);
    }

    [Fact]
    public void Extension_Sequence_AllSuccessful_KeepsValuesInOrder()
    {
        var results = new[] { Result<int, string>.Success(1), Result<int, string>.Success(2), Result<int, string>.Success(3) };

        var combined = results.Sequence();

        Assert.Equal(new[] { 1, 2, 3 }, Assert.IsType<Result<ImmutableArray<int>, string>.Ok>(combined).Value.ToArray());
        Assert.Empty(Assert.IsType<Result<ImmutableArray<int>, string>.Ok>(
            Array.Empty<Result<int, string>>().Sequence()).Value);
    }

    [Fact]
    public void Extension_Sequence_StopsAtTheFirstFailure_AndDoesNotEvaluateTheRest()
    {
        var evaluated = new List<int>();

        IEnumerable<Result<int, string>> Source()
        {
            foreach (var i in new[] { 1, 2, 3, 4 })
            {
                evaluated.Add(i);
                yield return i == 3 ? Result<int, string>.Failure("three failed") : Result<int, string>.Success(i);
            }
        }

        var combined = Source().Sequence();

        Assert.Equal(new[] { 1, 2, 3 }, evaluated);
        Assert.Equal("three failed", Assert.IsType<Result<ImmutableArray<int>, string>.Err>(combined).Error);
    }
}
