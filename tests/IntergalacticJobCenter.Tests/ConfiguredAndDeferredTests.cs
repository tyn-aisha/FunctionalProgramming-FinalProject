using System.Collections.Immutable;
using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Queries;
using IntergalacticJobCenter.Reports;

namespace IntergalacticJobCenter.Tests;

public class ConfiguredAndDeferredTests
{
    private static AlienApplicant Applicant(string name) => SampleData.Applicants().Single(a => a.Name == name);

    [Theory]
    [InlineData("Zorp", 90, 90)]
    [InlineData("Blarg", 65, 50)]
    [InlineData("Xylo", 54, 66)]
    [InlineData("Grakk", 65, 20)]
    [InlineData("Mimi", 100, 100)]
    [InlineData("Vexa", 28, 67)]
    public void PartialApplications_ScoreTheSameApplicantDifferently(string name, int compatibilityFocused, int calmnessFocused)
    {
        var applicant = Applicant(name);

        Assert.Equal(compatibilityFocused, ScoreFunctions.CompatibilityFocused(applicant));
        Assert.Equal(calmnessFocused, ScoreFunctions.CalmnessFocused(applicant));
    }

    [Fact]
    public void CurriedFunction_TakesArgumentsOneAtATime_AndPartialApplicationFixesThem()
    {
        var zorp = Applicant("Zorp");

        var afterFirst = ScoreFunctions.CreateScore(80);
        var afterSecond = afterFirst(20);

        Assert.Equal(90, afterSecond(zorp));
        Assert.Equal(ScoreFunctions.CompatibilityFocused(zorp), ScoreFunctions.CreateScore(80)(20)(zorp));
        Assert.Equal(90, ScoreFunctions.CreateScore(50)(50)(zorp));
    }

    [Fact]
    public void ConfiguredFunctions_GiveDifferentShortlistsOnTheSameDataset()
    {
        var applicants = SampleData.Applicants();

        var compat = ShortlistReport.Build(applicants, ScoreFunctions.CompatibilityFocused, 60);
        var calm = ShortlistReport.Build(applicants, ScoreFunctions.CalmnessFocused, 60);

        var compatNames = Assert.IsType<Result<ImmutableArray<ShortlistRow>, AppError>.Ok>(compat).Value.Select(r => r.Name).ToArray();
        var calmNames = Assert.IsType<Result<ImmutableArray<ShortlistRow>, AppError>.Ok>(calm).Value.Select(r => r.Name).ToArray();

        Assert.Equal(new[] { "Mimi", "Zorp", "Blarg", "Grakk" }, compatNames);
        Assert.Equal(new[] { "Mimi", "Zorp", "Vexa", "Xylo" }, calmNames);
        Assert.NotEqual(compatNames, calmNames);
    }

    [Fact]
    public void ConfiguredFunctions_AreIndependent_AndInvalidConfigurationIsRefused()
    {
        var blarg = Applicant("Blarg");

        Assert.Equal(65, ScoreFunctions.CompatibilityFocused(blarg));
        Assert.Equal(50, ScoreFunctions.CalmnessFocused(blarg));
        Assert.Equal(65, ScoreFunctions.CompatibilityFocused(blarg));

        Assert.Throws<ArgumentOutOfRangeException>(() => ScoreFunctions.CreateScore(-1)(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScoreFunctions.CreateScore(0)(0));
    }

    [Fact]
    public void Deferred_NothingRunsBeforeEnumeration_AndWorkHappensDuringIteration()
    {
        var calls = 0;
        int Probe(AlienApplicant applicant)
        {
            calls++;
            return 50;
        }

        var lazy = SampleData.Applicants().ScoreWith(Probe);
        Assert.Equal(0, calls);

        var first = lazy.First();
        Assert.Equal(1, calls);
        Assert.Equal("Zorp", first.Applicant.Name);

        var firstTwo = lazy.Take(2).ToArray();
        Assert.Equal(1 + 2, calls);
        Assert.Equal(2, firstTwo.Length);
    }

    [Fact]
    public void Deferred_RepeatedEnumerationRepeatsWork_ButASnapshotDoesNot()
    {
        var calls = 0;
        int Probe(AlienApplicant applicant)
        {
            calls++;
            return 50;
        }

        var lazy = SampleData.Applicants().ScoreWith(Probe);

        _ = lazy.Count();
        _ = lazy.Count();
        Assert.Equal(12, calls);

        calls = 0;
        var snapshot = lazy.ToImmutableArray();
        Assert.Equal(6, calls);

        _ = snapshot.Sum(s => s.Score);
        _ = snapshot.Select(s => s.Applicant.Name).ToArray();
        Assert.Equal(6, calls);
    }

    [Fact]
    public void Deferred_ProcessesProjectValues_ClampsScores_AndDoesNotTouchTheSource()
    {
        var source = SampleData.Applicants();
        var before = source.ToArray();

        var scored = source.ScoreWith(a => a.Name == "Zorp" ? 500 : -5).ToArray();

        Assert.Equal(new[] { 100, 0, 0, 0, 0, 0 }, scored.Select(s => s.Score).ToArray());
        Assert.Equal(source.Select(a => a.Name), scored.Select(s => s.Applicant.Name));
        Assert.Equal(before, source);
    }

    [Fact]
    public void Deferred_ArgumentsAreCheckedImmediately_NotAtIteration()
    {
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<AlienApplicant>)null!).ScoreWith(_ => 1));
        Assert.Throws<ArgumentNullException>(() => SampleData.Applicants().ScoreWith(null!));
    }
}
