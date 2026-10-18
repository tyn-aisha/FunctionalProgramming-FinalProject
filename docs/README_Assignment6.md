# Intergalactic Job Center — Assignment 6: Handling Temporal Coupling

Introduction to Functional Programming | Academic year 2026–2027 | Team of 4
Assignment 5 (screening and scoring pipeline) is unchanged and documented in `docs/README_Assignment5.md`. This stage adds the **placement and confirmation workflow** on top of its immutable models (`AlienApplicant`, `ScoringPolicy`, `Stages.JobFor`).

## 1. Task 1 — The hidden dependency (the unsafe "before" design)

After an alien has been recommended, an HR officer must validate the application, approve it and finally confirm the placement. A typical legacy design stores everything in one mutable object and relies on the caller to invoke the methods in the right order:

```csharp
// UNSAFE LEGACY DESIGN (documentation only, not part of the build)
public sealed class UnsafePlacementDesk
{
    public AlienApplicant? Applicant;
    public string Status = "New";
    public bool Approved;

    public void Load(AlienApplicant applicant) { Applicant = applicant; }

    public void Validate()
    {
        // crashes with NullReferenceException if Load() was never called
        Status = Applicant.Aggression > 30 ? "Invalid" : "Valid";
    }

    public void Approve() { Approved = true; }            // never looks at Status
    public void Confirm() { Status = "Confirmed"; }       // never looks at Approved or Status
}
```

**The hidden rule** is "call `Load`, then `Validate`, then `Approve`, then `Confirm`". The compiler does not enforce it, so every call below compiles and runs:

| Call sequence | What happens | Why it is wrong |
|---|---|---|
| `Confirm()` straight after `new` | Status becomes `Confirmed` | an unvalidated, unapproved application is confirmed |
| `Load(blarg); Validate(); Approve(); Confirm()` | Status `Invalid` is overwritten by `Confirmed` | the validation result is ignored (Blarg has aggression 55) |
| `Validate()` before `Load()` | `NullReferenceException` | the hidden prerequisite is missing |
| `Confirm(); Confirm();` | no signal that the second call is a repeat | repeated operations have no defined rule |
| failed `Validate()` after a partial `Load()` | object stays half-initialised | no state preservation |

This is **temporal coupling**: correctness depends on the time order of calls, not on the data passed.

## 2. How the new design removes it

| Hidden rule in the legacy code | Explicit replacement |
|---|---|
| "validate before using the application" | `ValidatedAlienApplication` can only be produced by `Create`; holding one *is* the proof of validation |
| "approve before confirm" | `Confirm` is only accepted in the `Approved` state; otherwise `Failed(MissingPrerequisite)` |
| mutable `Status` string | closed record hierarchy `ApplicationState` (Pending / Approved / Rejected / Confirmed) |
| "current date" read inside `Confirm` | the date is an explicit input: `Confirm(StartDate, Today)` |
| "who approved" kept in a field | the officer is the payload of the `Approve` command and of the `Approved` state |
| repeated call undefined | documented idempotent rule (section 5) |
| failure leaves a broken object | failure returns the very same state instance |

## 3. Domain model

### Validated factory (Tasks 1–2) — `ValidatedAlienApplication.Create(id, applicant, policy)`

Returns a closed `ApplicationCreation`: `Created(ValidatedAlienApplication)` or `CreationRejected(ImmutableArray<string> Reasons)`. It never throws for bad data and reports **all** violated rules:

| Rule | Message example |
|---|---|
| id present, 5–20 chars of letters, digits or `-` (trimmed) | `Application id is required.` |
| applicant and policy present | `Applicant is required.` / `Policy is required.` |
| applicant invariants (name, race, tentacles/eyes 0–64, aggression/compatibility 0–100) are guaranteed by `AlienApplicant.Create`; the factory no longer re-checks them | `Tentacles -1 is outside the allowed range 0-64.` (from `AlienApplicant.Create`) |
| aggression ≤ policy maximum (inclusive) | `Aggression 55 exceeds maximum 30 of Office Safety Policy.` |
| compatibility ≥ policy minimum (inclusive) | `Compatibility 40 is below minimum 50 of ...` |
| score ≥ hire threshold (inclusive) | `Score 60 is below the hire threshold 65 of Office Safety Policy.` |

**Access restrictions:** the constructor is `private`; every property is get-only, so a `with` expression cannot change a value; the only entry point is `Create`. A test checks via reflection that no type in the model has a public constructor.

### Closed record hierarchies (Task 3)

| Type | Cases (payload) | File |
|---|---|---|
| `ApplicationState` | `Pending`; `Approved(ApprovedBy)`; `Rejected(Reason)`; `Confirmed(ApprovedBy, StartDate)` — each also carries the `ValidatedAlienApplication` | `Workflow/ApplicationState.cs` |
| `WorkflowCommand` | `Approve(Officer)`; `Reject(Reason)`; `Confirm(StartDate, Today)` | `Workflow/WorkflowCommand.cs` |
| `TransitionResult` | `Transitioned(Previous, Current)`; `AlreadyApplied(Current)`; `Failed(Unchanged, Error, Message)` | `Workflow/TransitionResult.cs` |
| `ApplicationCreation` | `Created`; `CreationRejected` | `Workflow/ApplicationCreation.cs` |

Each base record has a **private constructor**, so only the nested cases exist (a closed hierarchy). State-case constructors are `internal`, so code outside the assembly cannot fabricate a `Confirmed` state; states are only reachable through `Start` and `Apply`.

## 4. Pattern matching (Task 4) — `Workflow/ApplicationWorkflow.cs`

`Apply(state, command)` uses a switch expression over the four states, and each state uses a switch expression over the three commands, so all 12 combinations have an explicit arm. There are no casts, only type patterns. C# cannot yet prove that a hierarchy is closed, so each switch ends with an arm that **throws** (unreachable because of the private constructors); there is no silent fallback value.

Transition table:

| State \ Command | `Approve(officer)` | `Reject(reason)` | `Confirm(start, today)` |
|---|---|---|---|
| **Pending** | → Approved (blank officer: `InvalidInput`) | → Rejected (blank reason: `InvalidInput`) | `Failed(MissingPrerequisite)` |
| **Approved** | same officer: `AlreadyApplied`; other officer: `Failed(InvalidTransition)` | `Failed(InvalidTransition)` | start ≥ today → Confirmed; start < today: `Failed(InvalidInput)` |
| **Rejected** (final) | `Failed(InvalidTransition)` | same reason: `AlreadyApplied`; other: `Failed(InvalidTransition)` | `Failed(MissingPrerequisite)` |
| **Confirmed** (final) | `Failed(InvalidTransition)` | `Failed(InvalidTransition)` | same date: `AlreadyApplied`; other date: `Failed(InvalidTransition)` |

`Presentation/WorkflowFormatter.cs` also matches every state, result and creation case exhaustively when rendering text.

## 5. Safe operations (Task 5)

- **Pure transitions:** `Apply` and `Replay` read no clock, Console or files and mutate nothing; the new state is returned. Printing is done by `Program.cs` after the result exists.
- **Missing prerequisites** become explicit `Failed(MissingPrerequisite)` results, never exceptions or defaults.
- **State preservation:** on `Failed` the result carries the *same instance* that was passed in (`Assert.Same` in the tests). `Replay` continues from that unchanged state.
- **Repeated operations (documented rule):** repeating the exact command that produced the current state is a no-op reported as `AlreadyApplied` (idempotent). A repeat with a different payload, or any command that does not fit the state, is refused with `Failed`.
- Programming errors (null arguments) throw `ArgumentNullException`; bad *data* never throws.

## 6. Build, run, test

**Prerequisites:** .NET SDK 8.0 or newer (`dotnet --version`) and internet access for the first NuGet restore.
**Working directory:** the solution root (the folder with `IntergalacticJobCenter.sln`) after extracting the ZIP.

```
dotnet restore
dotnet build
dotnet test
dotnet run --project src/IntergalacticJobCenter -- workflow                                # Assignment 6 demo
dotnet run --project src/IntergalacticJobCenter -- data/applicants_sample.csv both         # Assignment 5 demo
```

**Expected output of the `workflow` demo (documented before running):**

1. Blarg under the Office Safety Policy → `Rejected:` with two reasons (aggression 55 exceeds maximum 30; score 60 is below threshold 65).
2. Zorp → `Created APP-001: Zorp, score 90, job Front-Desk Receptionist`.
3. Commands on APP-001 (today = 2026-10-18):

| # | Command | Expected |
|---|---|---|
| 1 | Confirm +7 days | FAILED `[MissingPrerequisite]`, state stays Pending |
| 2 | Approve "Officer Kim" | OK Pending → Approved |
| 3 | Approve "Officer Kim" | NO-OP already applied |
| 4 | Approve "Officer Lee" | FAILED `[InvalidTransition]` |
| 5 | Confirm yesterday | FAILED `[InvalidInput]` |
| 6 | Confirm +7 days | OK Approved → Confirmed (starts 2026-10-25) |
| 7 | Confirm +7 days | NO-OP already applied |
| 8 | Reject "Changed our mind" | FAILED `[InvalidTransition]` |

Final state: `Confirmed, starts 2026-10-25 (approved by Officer Kim)`.

## 7. Requirement → file/method map

| Requirement | Where |
|---|---|
| Task 1: unsafe "before" example | section 1 of this README |
| Task 2: validated factory with restricted construction | `Workflow/ValidatedAlienApplication.cs` (`Create`), `Workflow/ApplicationRules.cs`, `Workflow/ApplicationCreation.cs` |
| Task 3: closed record hierarchies with payloads | `Workflow/ApplicationState.cs`, `WorkflowCommand.cs`, `TransitionResult.cs`, `ApplicationCreation.cs` |
| Task 4: switch expressions / pattern matching in the transition path | `Workflow/ApplicationWorkflow.cs` (`Apply`, `FromPending`, `FromApproved`, `FromRejected`, `FromConfirmed`), `Presentation/WorkflowFormatter.cs` |
| Task 5: safe, pure, explicit inputs, repeats, state preservation | `ApplicationWorkflow.Apply` / `Replay`, `WorkflowCommand.Confirm(StartDate, Today)` |
| Console demonstration | `Presentation/WorkflowDemo.cs`, `Program.cs` (`workflow` argument) |
| Automated tests | `tests/IntergalacticJobCenter.Tests/WorkflowTests.cs` (plus `PipelineTests.cs` from Assignment 5) |

## 8. Automated tests (xUnit)

`WorkflowTests.cs` contains 23 test methods (21 `[Fact]` and 2 `[Theory]`, 30 cases when each `[InlineData]` row is counted); `RefactorTests.cs` adds 12 more. Together with the Assignment 5 tests in `PipelineTests.cs` the cumulative total is well above the required 8.

| Required case | Tests | Expected result |
|---|---|---|
| Factory success | `FactorySuccess_ReturnsValidatedApplicationWithComputedValues` | `Created`, id trimmed, score 90, job Front-Desk Receptionist |
| Factory rejection | `FactoryRejection_ReportsEveryViolatedRule`, `..._MissingInputs_...`, `..._InvalidIds` (5 rows), `Factory_IsTheOnlyWayToConstruct_NoPublicConstructors` | explicit reason lists; no public constructors |
| Distinct cases | `DistinctCases_CarryTheirOwnPayloads`, `DistinctCases_EachHasItsOwnDescription_ViaExhaustiveMatching` | four different types with their own payloads and descriptions |
| Normal operation | `NormalOperation_PendingToApprovedToConfirmed`, `NormalOperation_PendingToRejected` | transitions succeed, payloads trimmed |
| Missing prerequisite | `MissingPrerequisite_ConfirmBeforeApproval_IsRefused`, `..._ConfirmRejectedApplication_...`, `InvalidSequences_FromFinalStates_...`, `BlankPayloads_AreInvalidInput` | `Failed` with the documented error code |
| Repeated operation | `RepeatedOperation_SameCommandIsANoOp`, `RepeatedOperation_ConflictingRepeatIsRefused` | `AlreadyApplied` (same instance) / `Failed(InvalidTransition)` |
| Boundary value | `Boundary_StartDateEqualToToday_...`, `Boundary_ScoreExactlyOnHireThreshold_...`, `Boundary_IdLength` (4 rows) | start date = today accepted, yesterday refused; score 65 accepted, 64 refused; id length 5 and 20 accepted, 4 and 21 refused |
| State preservation | `StatePreservation_FailedCommandReturnsTheSameStateInstance`, `StatePreservation_ReplayContinuesFromUnchangedStateAfterFailures` | `Assert.Same` on failure; replay ends in `Approved` |
| Purity and guards | `Purity_SameInputGivesEqualResult_AndInputIsNotModified`, `NullArguments_AreRejected`, `Demo_RendersTheDocumentedScenario` | equal results, unchanged input |

**Latest test outcome:** _fill in after running `dotnet test` on a clean extraction (date, passed/failed counts)._

## 9. Team and individual contributions

| Member | Group | Responsibility in Assignment 6 | Files |
|---|---|---|---|
| Dinmukhammet Imanalyuly | [fill in] | Tasks 1–2: identified the hidden dependency, wrote the unsafe "before" example, built the validated factory with a private constructor and domain rules | README section 1, `Workflow/ValidatedAlienApplication.cs`, `ApplicationRules.cs`, `ApplicationCreation.cs` |
| Aisha Tynyshtykbay | IT-2505 | Task 3: modelled the closed record hierarchies (states, commands, results) with payloads | `Workflow/ApplicationState.cs`, `WorkflowCommand.cs`, `TransitionResult.cs` |
| Yeraly Seitzhan | [fill in] | Task 4: switch-expression transition path over every state/command pair, exhaustive text rendering, console demo | `Workflow/ApplicationWorkflow.cs` (`Apply` and the four `From...` functions), `Presentation/WorkflowFormatter.cs`, `WorkflowDemo.cs`, `Program.cs` |
| Bertan Maidabek | [fill in] | Task 5, tests and documentation: explicit inputs (`Today`, officer), idempotent repeat rule, state preservation, `Replay`, 30 test cases, README | `ApplicationWorkflow.Replay`, `tests/**/WorkflowTests.cs`, `README.md`, `docs/` |

Each member must be able to explain and change their own part in the defense (for example: move a threshold, add a new `Approve` rule, add a state case and the matching switch arm, update the related test).

## 10. Refactoring: checked construction and explicit outcomes

The applicant intake (`AlienApplicant`, `ApplicantParser`, pipeline entry) was refactored from an unchecked public record to checked construction (`AlienApplicant.Create` → `ApplicantCreation`) with an explicit pipeline outcome (`ApplicantIntake.Process` → `IntakeOutcome`). The three-stage pure pipeline is unchanged.
Concrete before/after inputs and outputs, including the case the old design mishandled (an applicant with compatibility 150 was recommended with a fabricated score of 100), are in **`docs/REFACTORING.md`**; the old code is kept in `docs/before/`. Assertions: `tests/IntergalacticJobCenter.Tests/RefactorTests.cs`.
