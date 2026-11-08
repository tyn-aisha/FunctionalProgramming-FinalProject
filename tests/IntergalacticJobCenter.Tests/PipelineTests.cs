using System.Collections.Immutable;
using IntergalacticJobCenter.Input;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Pipeline;
using IntergalacticJobCenter.Policies;

namespace IntergalacticJobCenter.Tests;

public class PipelineTests
{
    private static readonly string[] SampleLines =
    {
        "Name,Race,Tentacles,Eyes,Aggression,Compatibility",
        "Zorp,Zorgian,6,2,10,90",
        "Blarg,Blargonian,10,8,55,70",
        "Xylo,Xylosian,2,2,30,50",
        "Grakk,Grakkian,12,4,95,80",
        "Mimi,Mimikian,0,0,0,100",
        "Vexa,Vexari,4,6,20,15"
    };

    private static ImmutableArray<AlienApplicant> Sample()
    {
        var outcome = ApplicantParser.ParseLines(SampleLines);
        Assert.Empty(outcome.Failures);
        return outcome.Applicants;
    }

    private static AlienApplicant Alien(string name, int tentacles, int eyes, int aggression, int compatibility) =>
        TestApplicants.Make(name, "Testian", tentacles, eyes, aggression, compatibility);

    private static (string Name, int Score, PlacementDecision Decision, string Job)[] Rows(PlacementReport report) =>
        report.Placements.Select(p => (p.Name, p.Score, p.Decision, p.Job)).ToArray();

    [Fact]
    public void NormalWorkflow_OfficeSafety_ProducesDocumentedResult()
    {
        var report = PlacementPipeline.Run(Sample(), ScoringPolicies.OfficeSafety);

        Assert.Equal("Office Safety Policy", report.PolicyName);
        Assert.Equal(
            new[]
            {
                ("Mimi", 100, PlacementDecision.Recommended, "Front-Desk Receptionist"),
                ("Zorp", 90, PlacementDecision.Recommended, "Front-Desk Receptionist"),
                ("Xylo", 58, PlacementDecision.NotRecommended, "None")
            },
            Rows(report));
        Assert.Equal(2, report.RecommendedCount);
        Assert.Equal(
            new[]
            {
                "Blarg: Aggression 55 exceeds maximum 30",
                "Grakk: Aggression 95 exceeds maximum 30",
                "Vexa: Compatibility 15 is below minimum 50"
            },
            report.Rejected.Select(r => $"{r.Name}: {r.Reason}").ToArray());
    }

    [Fact]
    public void NormalWorkflow_ExtremeConditions_ProducesDocumentedResult()
    {
        var report = PlacementPipeline.Run(Sample(), ScoringPolicies.ExtremeConditions);

        Assert.Equal(
            new[]
            {
                ("Blarg", 88, PlacementDecision.Recommended, "Multi-Task Operator"),
                ("Zorp", 54, PlacementDecision.NotRecommended, "None"),
                ("Xylo", 26, PlacementDecision.NotRecommended, "None"),
                ("Mimi", 20, PlacementDecision.NotRecommended, "None")
            },
            Rows(report));
        Assert.Equal(1, report.RecommendedCount);
        Assert.Equal(
            new[]
            {
                "Grakk: Aggression 95 exceeds maximum 80",
                "Vexa: Compatibility 15 is below minimum 20"
            },
            report.Rejected.Select(r => $"{r.Name}: {r.Reason}").ToArray());
    }

    [Fact]
    public void EmptyDataset_IsValidAndProducesEmptyReport()
    {
        var outcome = ApplicantParser.ParseLines(Array.Empty<string>());
        Assert.Empty(outcome.Applicants);
        Assert.Empty(outcome.Failures);

        foreach (var policy in new[] { ScoringPolicies.OfficeSafety, ScoringPolicies.ExtremeConditions })
        {
            var report = PlacementPipeline.Run(outcome.Applicants, policy);
            Assert.Empty(report.Placements);
            Assert.Empty(report.Rejected);
            Assert.Equal(0, report.RecommendedCount);
            Assert.Equal(policy.Name, report.PolicyName);
        }
    }

    [Fact]
    public void HeaderOnlyFile_IsAlsoAValidEmptyDataset()
    {
        var outcome = ApplicantParser.ParseLines(new[] { SampleLines[0] });
        Assert.Empty(outcome.Applicants);
        Assert.Empty(outcome.Failures);
    }

    [Fact]
    public void Boundary_ScreeningLimitsAreInclusive()
    {
        var applicants = new[]
        {
            Alien("OnLimit", 1, 1, aggression: 30, compatibility: 50),
            Alien("TooAggressive", 1, 1, aggression: 31, compatibility: 50),
            Alien("TooIncompatible", 1, 1, aggression: 30, compatibility: 49)
        };

        var result = Stages.FilterAndNormalize(applicants, maxAggression: 30, minCompatibility: 50);

        Assert.Equal(new[] { "OnLimit" }, result.Eligible.Select(a => a.Name).ToArray());
        Assert.Equal(new[] { "TooAggressive", "TooIncompatible" }, result.Rejected.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void Boundary_HireThresholdIsInclusive()
    {

        var applicants = new[] { Alien("Exactly65", 0, 0, 27, 60), Alien("Just64", 0, 0, 28, 60) };

        var report = PlacementPipeline.Run(applicants, ScoringPolicies.OfficeSafety);

        Assert.Equal(
            new[]
            {
                ("Exactly65", 65, PlacementDecision.Recommended, "General Clerk"),
                ("Just64", 64, PlacementDecision.NotRecommended, "None")
            },
            Rows(report));
    }

    [Fact]
    public void ZeroIsALegitimateValue_NotMissing()
    {
        var parsed = ApplicantParser.TryParseLine("Mimi,Mimikian,0,0,0,100", out var applicant, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(applicant);
        Assert.Equal(0, applicant.Tentacles);
        Assert.Equal(0, applicant.Eyes);
        Assert.Equal(0, applicant.Aggression);
    }

    [Theory]
    [InlineData(",Nameless,3,2,10,50", "Name is required")]
    [InlineData("Quix,,3,2,10,50", "Race is required")]
    [InlineData("Quix,Quixian,-1,2,10,50", "outside the allowed range")]
    [InlineData("Blob,Blobbian,abc,2,10,50", "not a whole number")]
    [InlineData("Fizz,Fizzian,4,2,120,50", "outside the allowed range")]
    [InlineData("Short,Row,3,2", "Expected 6")]
    [InlineData("Kor,Korian,4,2,10,", "Compatibility is missing")]
    [InlineData("   ", "Line is empty")]
    [InlineData(null, "Line is empty")]
    public void InvalidFields_AreRejectedWithClearMessage(string? line, string expectedMessagePart)
    {
        var parsed = ApplicantParser.TryParseLine(line, out var applicant, out var error);

        Assert.False(parsed);
        Assert.Null(applicant);
        Assert.Contains(expectedMessagePart, error);
    }

    [Fact]
    public void InvalidRows_AreReportedAndNoSuccessIsFabricated()
    {
        var outcome = ApplicantParser.ParseLines(new[]
        {
            "Name,Race,Tentacles,Eyes,Aggression,Compatibility",
            "Zorp,Zorgian,6,2,10,90",
            "Blob,Blobbian,abc,2,10,50",
            null
        });

        Assert.Equal(new[] { "Zorp" }, outcome.Applicants.Select(a => a.Name).ToArray());
        Assert.Equal(new[] { 3, 4 }, outcome.Failures.Select(f => f.LineNumber).ToArray());
    }

    [Fact]
    public void MissingInput_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicantParser.ParseLines(null));
        Assert.Throws<ArgumentNullException>(() => PlacementPipeline.Run(null!, ScoringPolicies.OfficeSafety));
        Assert.Throws<ArgumentNullException>(() => PlacementPipeline.Run(Sample(), null!));
        Assert.Throws<ArgumentException>(
            () => Stages.FilterAndNormalize(new AlienApplicant[] { null! }, 30, 50));
    }

    [Fact]
    public void TwoPolicies_SameInput_DifferentDocumentedResults()
    {
        var input = Sample();

        var office = PlacementPipeline.Run(input, ScoringPolicies.OfficeSafety);
        var extreme = PlacementPipeline.Run(input, ScoringPolicies.ExtremeConditions);

        Assert.NotEqual(Rows(office), Rows(extreme));

        Assert.Contains(office.Rejected, r => r.Name == "Blarg");
        Assert.Equal("Blarg", extreme.Placements[0].Name);
        Assert.Equal(PlacementDecision.Recommended, extreme.Placements[0].Decision);

        var differences = PolicyComparison.Compare(office, extreme);
        Assert.Equal(
            new[]
            {
                new DecisionDifference("Blarg", "Rejected", "Recommended"),
                new DecisionDifference("Mimi", "Recommended", "Not recommended"),
                new DecisionDifference("Zorp", "Recommended", "Not recommended")
            },
            differences.ToArray());
    }

    [Fact]
    public void HigherOrderStage_InvokesTheSuppliedDelegate()
    {
        var screening = Stages.FilterAndNormalize(new[] { Alien("A", 1, 1, 0, 100), Alien("B", 2, 2, 0, 100) }, 100, 0);

        var constant = Stages.ScoreApplicants(screening, _ => 42);
        var tooHigh = Stages.ScoreApplicants(screening, _ => 500);
        var tooLow = Stages.ScoreApplicants(screening, _ => -5);

        Assert.All(constant.Scored, s => Assert.Equal(42, s.Score));
        Assert.All(tooHigh.Scored, s => Assert.Equal(100, s.Score));
        Assert.All(tooLow.Scored, s => Assert.Equal(0, s.Score));
    }

    [Fact]
    public void Stage1_DoesNotModifyCallerInput_AndResultIsIndependentCopy()
    {
        var input = new List<AlienApplicant>
        {
            TestApplicants.Make("  zorp   the  great ", "ZORGIAN", 6, 2, 10, 90),
            TestApplicants.Make("Blarg", "Blargonian", 10, 8, 55, 70)
        };
        var snapshot = input.ToArray();

        var result = Stages.FilterAndNormalize(input, maxAggression: 30, minCompatibility: 50);

        Assert.Equal(snapshot, input.ToArray());
        Assert.Equal("  zorp   the  great ", input[0].Name);

        Assert.Equal("zorp the great", result.Eligible[0].Name);
        Assert.Equal("Zorgian", result.Eligible[0].Race);

        input.Clear();
        Assert.Single(result.Eligible);
    }

    [Fact]
    public void Stage1_RejectsDuplicateNamesCaseInsensitively_FirstWins()
    {
        var input = new[]
        {
            TestApplicants.Make("Zorp", "Zorgian", 6, 2, 10, 90),
            TestApplicants.Make("ZORP", "Other", 1, 1, 1, 99)
        };

        var result = Stages.FilterAndNormalize(input, 30, 50);

        Assert.Equal(new[] { "Zorp" }, result.Eligible.Select(a => a.Name).ToArray());
        Assert.Equal("Duplicate applicant name", Assert.Single(result.Rejected).Reason);
    }

    [Fact]
    public void Pipeline_IsDeterministic_AndLeavesInputUntouched()
    {
        var input = Sample();
        var snapshot = input.ToArray();

        var first = PlacementPipeline.Run(input, ScoringPolicies.OfficeSafety);
        var second = PlacementPipeline.Run(input, ScoringPolicies.OfficeSafety);

        Assert.Equal(Rows(first), Rows(second));
        Assert.Equal(first.Rejected.ToArray(), second.Rejected.ToArray());
        Assert.Equal(snapshot, input.ToArray());
    }
}
