using System.Collections.Immutable;
using IntergalacticJobCenter.Input;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Pipeline;
using IntergalacticJobCenter.Policies;

namespace IntergalacticJobCenter.Tests;

public class RefactorTests
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

    private static IntakeOutcome.Processed Processed(IEnumerable<string?>? lines, ScoringPolicy? policy) =>
        Assert.IsType<IntakeOutcome.Processed>(ApplicantIntake.Process(lines, policy));

    private static (string Name, int Score, PlacementDecision Decision)[] Rows(PlacementReport report) =>
        report.Placements.Select(p => (p.Name, p.Score, p.Decision)).ToArray();

    private static ImmutableArray<string> InvalidReasons(
        string? name, string? race, int tentacles, int eyes, int aggression, int compatibility) =>
        Assert.IsType<ApplicantCreation.Invalid>(
            AlienApplicant.Create(name, race, tentacles, eyes, aggression, compatibility)).Reasons;

    [Fact]
    public void Normal_RawLinesToReport_ThroughTheThreeStagePipeline()
    {
        var processed = Processed(SampleLines, ScoringPolicies.OfficeSafety);

        Assert.Equal(6, processed.ParsedCount);
        Assert.Empty(processed.Skipped);
        Assert.Equal(
            new[]
            {
                ("Mimi", 100, PlacementDecision.Recommended),
                ("Zorp", 90, PlacementDecision.Recommended),
                ("Xylo", 58, PlacementDecision.NotRecommended)
            },
            Rows(processed.Report));
        Assert.Equal(2, processed.Report.RecommendedCount);
    }

    [Fact]
    public void Normal_EmptyDataset_IsAProcessedEmptyReport_NotAnError()
    {
        var processed = Processed(new[] { SampleLines[0] }, ScoringPolicies.ExtremeConditions);

        Assert.Equal(0, processed.ParsedCount);
        Assert.Empty(processed.Report.Placements);
        Assert.Empty(processed.Skipped);
    }

    [Fact]
    public void Normal_CheckedConstruction_AcceptsTheLimitsAndZero()
    {
        foreach (var (t, e, a, c) in new[] { (0, 0, 0, 0), (64, 64, 100, 100) })
        {
            var valid = Assert.IsType<ApplicantCreation.Valid>(AlienApplicant.Create("Edge", "Testian", t, e, a, c));
            Assert.Equal((t, e, a, c),
                (valid.Applicant.Tentacles, valid.Applicant.Eyes, valid.Applicant.Aggression, valid.Applicant.Compatibility));
        }
    }

    [Fact]
    public void Invalid_GhostWithCompatibility150_IsRejectedAtConstruction()
    {
        var reasons = InvalidReasons("Ghost", "Phantom", 0, 0, 0, 150);

        Assert.Equal(new[] { "Compatibility 150 is outside the allowed range 0-100." }, reasons.ToArray());
    }

    [Fact]
    public void Invalid_GhostFromCsv_IsSkippedWithReason_AndNeverReachesTheReport()
    {
        var lines = new[] { SampleLines[0], "Ghost,Phantom,0,0,0,150", "Mimi,Mimikian,0,0,0,100" };

        var processed = Processed(lines, ScoringPolicies.OfficeSafety);

        var skipped = Assert.Single(processed.Skipped);
        Assert.Equal(2, skipped.LineNumber);
        Assert.Equal("Compatibility 150 is outside the allowed range 0-100.", skipped.Message);
        Assert.Equal(new[] { ("Mimi", 100, PlacementDecision.Recommended) }, Rows(processed.Report));
    }

    [Fact]
    public void Invalid_EveryViolatedRuleIsReported_AndNullTextIsRejectedNotCrashed()
    {
        Assert.Equal(
            new[]
            {
                "Name is required.",
                "Tentacles -1 is outside the allowed range 0-64.",
                "Eyes 65 is outside the allowed range 0-64.",
                "Aggression 101 is outside the allowed range 0-100."
            },
            InvalidReasons(" ", "Zorgian", -1, 65, 101, 90).ToArray());

        Assert.Equal(
            new[] { "Name is required.", "Race is required." },
            InvalidReasons(null, null, 1, 1, 1, 1).ToArray());
    }

    [Fact]
    public void Invalid_MissingPrerequisites_AreRefusedExplicitly_NotThrown()
    {
        var noLines = Assert.IsType<IntakeOutcome.Refused>(ApplicantIntake.Process(null, ScoringPolicies.OfficeSafety));
        var noPolicy = Assert.IsType<IntakeOutcome.Refused>(ApplicantIntake.Process(SampleLines, null));
        var neither = Assert.IsType<IntakeOutcome.Refused>(ApplicantIntake.Process(null, null));

        Assert.Equal(new[] { "Input lines are missing." }, noLines.Reasons.ToArray());
        Assert.Equal(new[] { "Policy is missing." }, noPolicy.Reasons.ToArray());
        Assert.Equal(new[] { "Input lines are missing.", "Policy is missing." }, neither.Reasons.ToArray());
    }

    [Fact]
    public void Invalid_NoPublicConstructor_SoUncheckedValuesCannotBeBuilt()
    {
        Assert.Empty(typeof(AlienApplicant).GetConstructors());
    }

    [Fact]
    public void Repeated_ProcessingTheSameInputTwice_GivesTheSameOutcome()
    {
        var first = Processed(SampleLines, ScoringPolicies.OfficeSafety);
        var second = Processed(SampleLines, ScoringPolicies.OfficeSafety);

        Assert.Equal(Rows(first.Report), Rows(second.Report));
        Assert.Equal(first.Report.Rejected.ToArray(), second.Report.Rejected.ToArray());
        Assert.Equal(first.ParsedCount, second.ParsedCount);
    }

    [Fact]
    public void Repeated_NormalizeIsIdempotent_AndRepeatedRecordIsRejectedAsDuplicate()
    {
        var applicant = TestApplicants.Make("  zorp   the  great ", "ZORGIAN", 6, 2, 10, 90);
        Assert.Equal(applicant.Normalize(), applicant.Normalize().Normalize());

        var lines = new[] { SampleLines[0], "Zorp,Zorgian,6,2,10,90", "ZORP,Other,1,1,1,99" };
        var processed = Processed(lines, ScoringPolicies.OfficeSafety);

        Assert.Equal(new[] { ("Zorp", 90, PlacementDecision.Recommended) }, Rows(processed.Report));
        var rejection = Assert.Single(processed.Report.Rejected);
        Assert.Equal("Duplicate applicant name", rejection.Reason);
    }

    [Fact]
    public void InputPreservation_LinesAndApplicantsAreNeverChanged()
    {
        var lines = new[] { SampleLines[0], "  Zorp ,  zORGIAN ,6,2,10,90", "Ghost,Phantom,0,0,0,150" };
        var linesBefore = lines.ToArray();

        var parsed = ApplicantParser.ParseLines(lines);
        var applicantsBefore = parsed.Applicants.ToArray();
        var report = PlacementPipeline.Run(parsed.Applicants, ScoringPolicies.OfficeSafety);

        Assert.Equal(linesBefore, lines);
        Assert.Equal(applicantsBefore, parsed.Applicants.ToArray());
        Assert.Single(report.Placements);
    }

    [Fact]
    public void InputPreservation_NormalizeReturnsANewValue_OriginalKeepsItsText()
    {
        var original = TestApplicants.Make("  zorp   the  great ", "ZORGIAN", 6, 2, 10, 90);

        var normalised = original.Normalize();

        Assert.NotSame(original, normalised);
        Assert.Equal("  zorp   the  great ", original.Name);
        Assert.Equal("ZORGIAN", original.Race);
        Assert.Equal("zorp the great", normalised.Name);
        Assert.Equal("Zorgian", normalised.Race);
    }
}
