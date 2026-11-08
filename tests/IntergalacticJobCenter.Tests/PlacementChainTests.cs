using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;
using IntergalacticJobCenter.Services;
using IntergalacticJobCenter.Workflow;

namespace IntergalacticJobCenter.Tests;

public class PlacementChainTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    private static ApplicantRegistry Registry() => ApplicantRegistry.From(new[]
    {
        TestApplicants.Make("Zorp", "Zorgian", 6, 2, 10, 90),
        TestApplicants.Make("Blarg", "Blargonian", 10, 8, 55, 70),
        TestApplicants.Make("Mimi", "Mimikian", 0, 0, 0, 100)
    });

    private static PlacementRequest Request(
        string? applicant = "Zorp", string? policy = "office", string? id = "APP-001",
        string? officer = "Officer Kim", int startOffsetDays = 7) =>
        new(applicant, policy, id, officer, Today.AddDays(startOffsetDays), Today);

    private static PlacementReceipt Success(Result<PlacementReceipt, AppError> result) =>
        Assert.IsType<Result<PlacementReceipt, AppError>.Ok>(result).Value;

    private static AppError Failure(Result<PlacementReceipt, AppError> result) =>
        Assert.IsType<Result<PlacementReceipt, AppError>.Err>(result).Error;

    [Fact]
    public void CompleteSuccess_ChainProducesTheDocumentedReceipt()
    {
        var receipt = Success(PlacementService.Place(Registry(), Request()));

        Assert.Equal("APP-001", receipt.ApplicationId);
        Assert.Equal("Zorp", receipt.ApplicantName);
        Assert.Equal("Front-Desk Receptionist", receipt.Job);
        Assert.Equal(90, receipt.Score);
        Assert.Equal("Officer Kim", receipt.ApprovedBy);
        Assert.Equal(new DateOnly(2026, 10, 25), receipt.StartDate);
        Assert.Equal(receipt.StartDate, receipt.State.StartDate);
    }

    [Fact]
    public void CompleteSuccess_SamePersonUnderTheOtherPolicy_GivesADifferentResult()
    {

        Assert.IsType<Result<PlacementReceipt, AppError>.Err>(
            PlacementService.Place(Registry(), Request("Blarg", "office")));

        var receipt = Success(PlacementService.Place(Registry(), Request("Blarg", "extreme", "APP-002")));

        Assert.Equal(88, receipt.Score);
        Assert.Equal("Multi-Task Operator", receipt.Job);
    }

    [Fact]
    public void CompleteSuccess_ZeroValuesAreAcceptedAsRealData()
    {

        var receipt = Success(PlacementService.Place(Registry(), Request("Mimi", "office", "APP-003")));

        Assert.Equal(100, receipt.Score);
    }

    [Fact]
    public void DomainFailure1_RuleViolated_ApplicationRejectedWithTheRuleInTheMessage()
    {
        var error = Assert.IsType<AppError.Domain>(Failure(PlacementService.Place(Registry(), Request("Blarg", "office"))));

        Assert.Equal(ErrorCode.ApplicationRejected, error.Code);
        Assert.Equal("Blarg", error.Subject);
        Assert.Contains("Aggression 55 exceeds maximum 30 of Office Safety Policy.", error.Message);
        Assert.Contains("Score 60 is below the hire threshold 65", error.Message);
    }

    [Fact]
    public void DomainFailure2_MissingRequiredItem_ApplicantAndPolicy()
    {
        var noApplicant = Assert.IsType<AppError.Domain>(Failure(PlacementService.Place(Registry(), Request("Nobody"))));
        var noPolicy = Assert.IsType<AppError.Domain>(Failure(PlacementService.Place(Registry(), Request(policy: "mars"))));
        var blankApplicant = Assert.IsType<AppError.Domain>(Failure(PlacementService.Place(Registry(), Request(applicant: " "))));

        Assert.Equal(ErrorCode.ApplicantNotFound, noApplicant.Code);
        Assert.Equal("Nobody", noApplicant.Subject);
        Assert.Equal(ErrorCode.PolicyNotFound, noPolicy.Code);
        Assert.Equal("mars", noPolicy.Subject);
        Assert.Equal(ErrorCode.ApplicantNotFound, blankApplicant.Code);
        Assert.Equal("(blank)", blankApplicant.Subject);
    }

    [Fact]
    public void DomainFailure3_StateDoesNotPermit_TransitionRejected()
    {
        var pastStart = Assert.IsType<AppError.Domain>(Failure(PlacementService.Place(Registry(), Request(startOffsetDays: -1))));
        var blankOfficer = Assert.IsType<AppError.Domain>(Failure(PlacementService.Place(Registry(), Request(officer: " "))));

        Assert.Equal(ErrorCode.TransitionRejected, pastStart.Code);
        Assert.Equal("APP-001", pastStart.Subject);
        Assert.Contains("before today (2026-10-18)", pastStart.Message);
        Assert.Equal(ErrorCode.TransitionRejected, blankOfficer.Code);
        Assert.Contains("Approving officer is required", blankOfficer.Message);

        var confirmed = Success(PlacementService.Place(Registry(), Request())).State;
        var rejected = WorkflowSteps.Reject(confirmed, "Changed our mind");
        var error = Assert.IsType<Result<ApplicationState, AppError>.Err>(rejected).Error;
        Assert.Equal(ErrorCode.TransitionRejected, error.Code);
        Assert.Contains("final and cannot be rejected", error.Message);
    }

    [Fact]
    public void DomainFailure_EarlierFailureWinsAndLaterStepsAreSkipped()
    {

        var error = Failure(PlacementService.Place(Registry(), Request("Nobody", officer: " ", startOffsetDays: -5)));

        Assert.Equal(ErrorCode.ApplicantNotFound, error.Code);
    }

    [Fact]
    public void Chain_IsPure_RepeatingTheSameRequestGivesEqualOutcomes_AndTheRegistryIsUnchanged()
    {
        var registry = Registry();
        var before = registry.Applicants.ToArray();

        var first = PlacementService.Place(registry, Request());
        var second = PlacementService.Place(registry, Request());
        var failed1 = PlacementService.Place(registry, Request("Blarg", "office"));
        var failed2 = PlacementService.Place(registry, Request("Blarg", "office"));

        Assert.Equal(first, second);
        Assert.Equal(failed1, failed2);
        Assert.Equal(before, registry.Applicants.ToArray());
    }

    [Fact]
    public void Chain_NullArguments_AreProgrammingErrors()
    {
        Assert.Throws<ArgumentNullException>(() => PlacementService.Place(null!, Request()));
        Assert.Throws<ArgumentNullException>(() => PlacementService.Place(Registry(), null!));
    }
}
