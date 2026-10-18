using System.Collections.Immutable;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Policies;
using IntergalacticJobCenter.Presentation;
using IntergalacticJobCenter.Workflow;

namespace IntergalacticJobCenter.Tests;

public class WorkflowTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);
    private static readonly AlienApplicant Zorp = TestApplicants.Make("Zorp", "Zorgian", 6, 2, 10, 90);
    private static readonly AlienApplicant Blarg = TestApplicants.Make("Blarg", "Blargonian", 10, 8, 55, 70);

    private static ValidatedAlienApplication Valid(string id = "APP-001", AlienApplicant? applicant = null) =>
        Assert.IsType<ApplicationCreation.Created>(
            ValidatedAlienApplication.Create(id, applicant ?? Zorp, ScoringPolicies.OfficeSafety)).Application;

    private static ApplicationState Pending() => ApplicationWorkflow.Start(Valid());

    private static ApplicationState Next(ApplicationState state, WorkflowCommand command) =>
        Assert.IsType<TransitionResult.Transitioned>(ApplicationWorkflow.Apply(state, command)).Current;

    private static ApplicationState Approved() => Next(Pending(), new WorkflowCommand.Approve("Officer Kim"));

    private static ApplicationState Rejected() => Next(Pending(), new WorkflowCommand.Reject("No visa"));

    private static ApplicationState Confirmed() =>
        Next(Approved(), new WorkflowCommand.Confirm(Today.AddDays(7), Today));

    private static ImmutableArray<string> RejectionReasons(ApplicationCreation creation) =>
        Assert.IsType<ApplicationCreation.CreationRejected>(creation).Reasons;

    [Fact]
    public void FactorySuccess_ReturnsValidatedApplicationWithComputedValues()
    {
        var creation = ValidatedAlienApplication.Create("  APP-001 ", Zorp, ScoringPolicies.OfficeSafety);

        var application = Assert.IsType<ApplicationCreation.Created>(creation).Application;
        Assert.Equal("APP-001", application.ApplicationId);
        Assert.Equal("Zorp", application.Applicant.Name);
        Assert.Equal("Office Safety Policy", application.PolicyName);
        Assert.Equal(90, application.Score);
        Assert.Equal("Front-Desk Receptionist", application.Job);
    }

    [Fact]
    public void FactoryRejection_ReportsEveryViolatedRule()
    {
        var reasons = RejectionReasons(
            ValidatedAlienApplication.Create("APP-002", Blarg, ScoringPolicies.OfficeSafety));

        Assert.Equal(
            new[]
            {
                "Aggression 55 exceeds maximum 30 of Office Safety Policy.",
                "Score 60 is below the hire threshold 65 of Office Safety Policy."
            },
            reasons.ToArray());
    }

    [Fact]
    public void FactoryRejection_MissingInputs_AreReportedNotThrown()
    {
        var reasons = RejectionReasons(ValidatedAlienApplication.Create(null, null, null));

        Assert.Equal(
            new[] { "Application id is required.", "Applicant is required.", "Policy is required." },
            reasons.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABCD")]
    [InlineData("APP 001")]
    [InlineData("APP_001")]
    public void FactoryRejection_InvalidIds(string id)
    {
        var reasons = RejectionReasons(ValidatedAlienApplication.Create(id, Zorp, ScoringPolicies.OfficeSafety));

        Assert.Single(reasons);
        Assert.Contains("Application id", reasons[0]);
    }

    [Fact]
    public void Factory_IsTheOnlyWayToConstruct_NoPublicConstructors()
    {
        Assert.Empty(typeof(AlienApplicant).GetConstructors());
        Assert.Empty(typeof(ValidatedAlienApplication).GetConstructors());
        Assert.Empty(typeof(ApplicationState.Pending).GetConstructors());
        Assert.Empty(typeof(ApplicationState.Approved).GetConstructors());
        Assert.Empty(typeof(ApplicationState.Rejected).GetConstructors());
        Assert.Empty(typeof(ApplicationState.Confirmed).GetConstructors());
    }

    [Fact]
    public void DistinctCases_CarryTheirOwnPayloads()
    {
        var pending = Pending();
        var approved = Assert.IsType<ApplicationState.Approved>(Approved());
        var rejected = Assert.IsType<ApplicationState.Rejected>(Rejected());
        var confirmed = Assert.IsType<ApplicationState.Confirmed>(Confirmed());

        Assert.IsType<ApplicationState.Pending>(pending);
        Assert.Equal("Officer Kim", approved.ApprovedBy);
        Assert.Equal("No visa", rejected.Reason);
        Assert.Equal(Today.AddDays(7), confirmed.StartDate);
        Assert.Equal("Officer Kim", confirmed.ApprovedBy);

        var types = new ApplicationState[] { pending, approved, rejected, confirmed }
            .Select(s => s.GetType()).Distinct().Count();
        Assert.Equal(4, types);
    }

    [Fact]
    public void DistinctCases_EachHasItsOwnDescription_ViaExhaustiveMatching()
    {
        var descriptions = new[] { Pending(), Approved(), Rejected(), Confirmed() }
            .Select(WorkflowFormatter.Describe)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Pending (Zorp, score 90)",
                "Approved by Officer Kim",
                "Rejected: No visa",
                "Confirmed, starts 2026-10-25 (approved by Officer Kim)"
            },
            descriptions);
    }

    [Fact]
    public void NormalOperation_PendingToApprovedToConfirmed()
    {
        var run = ApplicationWorkflow.Replay(Pending(), new WorkflowCommand[]
        {
            new WorkflowCommand.Approve("  Officer Kim "),
            new WorkflowCommand.Confirm(Today.AddDays(7), Today)
        });

        Assert.All(run.Steps, step => Assert.IsType<TransitionResult.Transitioned>(step));
        var confirmed = Assert.IsType<ApplicationState.Confirmed>(run.Final);
        Assert.Equal("Officer Kim", confirmed.ApprovedBy);
        Assert.Equal(new DateOnly(2026, 10, 25), confirmed.StartDate);
        Assert.Equal("APP-001", confirmed.Application.ApplicationId);
    }

    [Fact]
    public void NormalOperation_PendingToRejected()
    {
        var result = ApplicationWorkflow.Apply(Pending(), new WorkflowCommand.Reject("  No visa "));

        var transitioned = Assert.IsType<TransitionResult.Transitioned>(result);
        Assert.IsType<ApplicationState.Pending>(transitioned.Previous);
        Assert.Equal("No visa", Assert.IsType<ApplicationState.Rejected>(transitioned.Current).Reason);
    }

    [Fact]
    public void MissingPrerequisite_ConfirmBeforeApproval_IsRefused()
    {
        var pending = Pending();

        var failed = Assert.IsType<TransitionResult.Failed>(
            ApplicationWorkflow.Apply(pending, new WorkflowCommand.Confirm(Today.AddDays(1), Today)));

        Assert.Equal(TransitionError.MissingPrerequisite, failed.Error);
        Assert.Contains("must be approved first", failed.Message);
        Assert.IsType<ApplicationState.Pending>(failed.Unchanged);
    }

    [Fact]
    public void MissingPrerequisite_ConfirmRejectedApplication_IsRefused()
    {
        var failed = Assert.IsType<TransitionResult.Failed>(
            ApplicationWorkflow.Apply(Rejected(), new WorkflowCommand.Confirm(Today.AddDays(1), Today)));

        Assert.Equal(TransitionError.MissingPrerequisite, failed.Error);
    }

    [Fact]
    public void InvalidSequences_FromFinalStates_AreRefused()
    {
        var rejectApproved = ApplicationWorkflow.Apply(Approved(), new WorkflowCommand.Reject("late"));
        var approveRejected = ApplicationWorkflow.Apply(Rejected(), new WorkflowCommand.Approve("Officer Kim"));
        var approveConfirmed = ApplicationWorkflow.Apply(Confirmed(), new WorkflowCommand.Approve("Officer Kim"));
        var rejectConfirmed = ApplicationWorkflow.Apply(Confirmed(), new WorkflowCommand.Reject("late"));

        foreach (var result in new[] { rejectApproved, approveRejected, approveConfirmed, rejectConfirmed })
        {
            Assert.Equal(TransitionError.InvalidTransition, Assert.IsType<TransitionResult.Failed>(result).Error);
        }
    }

    [Fact]
    public void BlankPayloads_AreInvalidInput()
    {
        var approve = ApplicationWorkflow.Apply(Pending(), new WorkflowCommand.Approve("  "));
        var approveNull = ApplicationWorkflow.Apply(Pending(), new WorkflowCommand.Approve(null));
        var reject = ApplicationWorkflow.Apply(Pending(), new WorkflowCommand.Reject(""));

        foreach (var result in new[] { approve, approveNull, reject })
        {
            var failed = Assert.IsType<TransitionResult.Failed>(result);
            Assert.Equal(TransitionError.InvalidInput, failed.Error);
            Assert.IsType<ApplicationState.Pending>(failed.Unchanged);
        }
    }

    [Fact]
    public void RepeatedOperation_SameCommandIsANoOp()
    {
        var approved = Approved();
        var confirmed = Confirmed();
        var rejected = Rejected();

        var again1 = ApplicationWorkflow.Apply(approved, new WorkflowCommand.Approve("Officer Kim"));
        var again2 = ApplicationWorkflow.Apply(confirmed, new WorkflowCommand.Confirm(Today.AddDays(7), Today));
        var again3 = ApplicationWorkflow.Apply(rejected, new WorkflowCommand.Reject("No visa"));

        Assert.Same(approved, Assert.IsType<TransitionResult.AlreadyApplied>(again1).Current);
        Assert.Same(confirmed, Assert.IsType<TransitionResult.AlreadyApplied>(again2).Current);
        Assert.Same(rejected, Assert.IsType<TransitionResult.AlreadyApplied>(again3).Current);
    }

    [Fact]
    public void RepeatedOperation_ConflictingRepeatIsRefused()
    {
        var differentOfficer = ApplicationWorkflow.Apply(Approved(), new WorkflowCommand.Approve("Officer Lee"));
        var differentDate = ApplicationWorkflow.Apply(Confirmed(), new WorkflowCommand.Confirm(Today.AddDays(9), Today));
        var differentReason = ApplicationWorkflow.Apply(Rejected(), new WorkflowCommand.Reject("Other reason"));

        foreach (var result in new[] { differentOfficer, differentDate, differentReason })
        {
            Assert.Equal(TransitionError.InvalidTransition, Assert.IsType<TransitionResult.Failed>(result).Error);
        }
    }

    [Fact]
    public void Boundary_StartDateEqualToToday_IsAccepted_YesterdayIsRefused()
    {
        var approved = Approved();

        var onToday = ApplicationWorkflow.Apply(approved, new WorkflowCommand.Confirm(Today, Today));
        var yesterday = ApplicationWorkflow.Apply(approved, new WorkflowCommand.Confirm(Today.AddDays(-1), Today));

        Assert.Equal(Today, Assert.IsType<ApplicationState.Confirmed>(
            Assert.IsType<TransitionResult.Transitioned>(onToday).Current).StartDate);
        var failed = Assert.IsType<TransitionResult.Failed>(yesterday);
        Assert.Equal(TransitionError.InvalidInput, failed.Error);
        Assert.Contains("is before today", failed.Message);
    }

    [Fact]
    public void Boundary_ScoreExactlyOnHireThreshold_IsAccepted_OneBelowIsRejected()
    {

        var onThreshold = TestApplicants.Make("Edge", "Testian", 0, 0, 27, 60);
        var oneBelow = TestApplicants.Make("Below", "Testian", 0, 0, 28, 60);

        var accepted = Assert.IsType<ApplicationCreation.Created>(
            ValidatedAlienApplication.Create("APP-010", onThreshold, ScoringPolicies.OfficeSafety));
        var reasons = RejectionReasons(
            ValidatedAlienApplication.Create("APP-011", oneBelow, ScoringPolicies.OfficeSafety));

        Assert.Equal(65, accepted.Application.Score);
        Assert.Equal(new[] { "Score 64 is below the hire threshold 65 of Office Safety Policy." }, reasons.ToArray());
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(4, false)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void Boundary_IdLength(int length, bool accepted)
    {
        var creation = ValidatedAlienApplication.Create(new string('A', length), Zorp, ScoringPolicies.OfficeSafety);

        Assert.Equal(accepted, creation is ApplicationCreation.Created);
    }

    [Fact]
    public void StatePreservation_FailedCommandReturnsTheSameStateInstance()
    {
        var states = new[] { Pending(), Approved(), Rejected(), Confirmed() };
        var badCommands = new WorkflowCommand[]
        {
            new WorkflowCommand.Approve(""),
            new WorkflowCommand.Reject(" "),
            new WorkflowCommand.Confirm(Today.AddDays(-5), Today)
        };

        foreach (var state in states)
        {
            var snapshot = state with { };
            foreach (var command in badCommands)
            {
                var result = ApplicationWorkflow.Apply(state, command);

                if (result is TransitionResult.Failed failed)
                {
                    Assert.Same(state, failed.Unchanged);
                }

                Assert.Equal(snapshot, state);
            }
        }
    }

    [Fact]
    public void StatePreservation_ReplayContinuesFromUnchangedStateAfterFailures()
    {
        var run = ApplicationWorkflow.Replay(Pending(), new WorkflowCommand[]
        {
            new WorkflowCommand.Confirm(Today.AddDays(7), Today),
            new WorkflowCommand.Approve(""),
            new WorkflowCommand.Approve("Officer Kim"),
            new WorkflowCommand.Confirm(Today.AddDays(-1), Today),
            new WorkflowCommand.Reject("too late")
        });

        Assert.IsType<TransitionResult.Failed>(run.Steps[0]);
        Assert.IsType<TransitionResult.Failed>(run.Steps[1]);
        Assert.IsType<TransitionResult.Transitioned>(run.Steps[2]);
        Assert.IsType<TransitionResult.Failed>(run.Steps[3]);
        Assert.IsType<TransitionResult.Failed>(run.Steps[4]);

        var approved = Assert.IsType<ApplicationState.Approved>(run.Final);
        Assert.Equal("Officer Kim", approved.ApprovedBy);
    }

    [Fact]
    public void Purity_SameInputGivesEqualResult_AndInputIsNotModified()
    {
        var approved = Approved();
        var before = Describe(approved);
        var command = new WorkflowCommand.Confirm(Today.AddDays(3), Today);

        var first = ApplicationWorkflow.Apply(approved, command);
        var second = ApplicationWorkflow.Apply(approved, command);

        Assert.Equal(first, second);
        Assert.Equal(before, Describe(approved));
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationWorkflow.Start(null!));
        Assert.Throws<ArgumentNullException>(() => ApplicationWorkflow.Apply(null!, new WorkflowCommand.Approve("x")));
        Assert.Throws<ArgumentNullException>(() => ApplicationWorkflow.Apply(Pending(), null!));
        Assert.Throws<ArgumentNullException>(() => ApplicationWorkflow.Replay(Pending(), null!));
    }

    [Fact]
    public void Demo_RendersTheDocumentedScenario()
    {
        var text = WorkflowDemo.Render();

        Assert.Contains("Aggression 55 exceeds maximum 30 of Office Safety Policy.", text);
        Assert.Contains("[MissingPrerequisite]", text);
        Assert.Contains("Final state: Confirmed, starts", text);
    }

    private static string Describe(ApplicationState state) => WorkflowFormatter.Describe(state);
}
