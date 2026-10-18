# Refactoring: applicant intake (unchecked public record → checked construction + explicit outcomes)

**Module chosen:** applicant intake = `AlienApplicant` + `ApplicantParser` + the entry to the pipeline.
**Pipeline kept:** the same three pure stages (`Stages.FilterAndNormalize` → `Stages.ScoreApplicants` → `Stages.BuildReport`, composed in `PlacementPipeline.Run`). Their signatures did not change.

## 1. What was wrong in the old design (Assignment 5 ZIP; snapshot in `docs/before/`)

| Problem | Where | Effect |
|---|---|---|
| Public positional record with `init` properties: anyone could build `new AlienApplicant("Ghost","Phantom",0,0,0,150)` or write `applicant with { Aggression = 500 }` | `Models/AlienApplicant.cs` | validity depended on the caller having used the parser first (a hidden "validate before you call the pipeline" rule) |
| Stage 2 silently clamps scores to 0–100 | `Stages.ScoreApplicants` | out-of-range garbage was turned into a plausible score |
| `null` name or race | `Stages.Normalize` | `NullReferenceException` deep inside Stage 1 |
| Missing lines / policy | `ApplicantParser.ParseLines`, `PlacementPipeline.Run` | exceptions only; the caller had no explicit outcome type |

## 2. What changed

| Before | After |
|---|---|
| `record AlienApplicant(string Name, ...)` with a public constructor | `sealed record AlienApplicant` with a **private constructor**, get-only properties, and `AlienApplicant.Create(...)` returning the union `ApplicantCreation` = `Valid(AlienApplicant)` / `Invalid(ImmutableArray<string> Reasons)` |
| range checks lived only in `ApplicantParser` (and were repeated in `ApplicationRules`) | one place: `AlienApplicant.Create`; the parser only reads text and calls `Create`; the duplicate check in `ApplicationRules` was removed |
| normalisation helpers hidden in `Stages` | `AlienApplicant.Normalize()` returns a **new** value (idempotent); Stage 1 calls it |
| `ApplicantParser.ParseLines` + `PlacementPipeline.Run` called separately, errors as exceptions | `ApplicantIntake.Process(lines, policy)` returns the union `IntakeOutcome` = `Processed(Report, Skipped, ParsedCount)` / `Refused(Reasons)`; bad rows are data, missing inputs are `Refused` |
| `Program` printed parse failures and reports from separate calls | `Program` matches the `IntakeOutcome` cases exhaustively (throwing default arm) |

Files: `Models/AlienApplicant.cs`, `Models/ApplicantCreation.cs`, `Input/ApplicantParser.cs`, `Pipeline/ApplicantIntake.cs`, `Pipeline/IntakeOutcome.cs`, `Workflow/ApplicationRules.cs`, `Workflow/ValidatedAlienApplication.cs`, `Pipeline/Stages.cs`, `Program.cs`.

## 3. Concrete before / after

All "after" results are asserted in `tests/IntergalacticJobCenter.Tests/RefactorTests.cs`. The "before" results are traced by hand from the Assignment 5 code (the Assignment 5 ZIP can be run to confirm them).

### A. The case the old design mishandled (and the new design rejects)

Input: applicant `Ghost, Phantom, tentacles 0, eyes 0, aggression 0, compatibility 150` (compatibility is limited to 0–100), Office Safety Policy.

| | Output |
|---|---|
| **Before** — `PlacementPipeline.Run(new[] { new AlienApplicant("Ghost","Phantom",0,0,0,150) }, OfficeSafety)` | Stage 1 accepts (aggression 0 ≤ 30, compatibility 150 ≥ 50). Stage 2: (6·150 + 4·100) / 10 = 130, clamped to **100**. Stage 3: `1  Ghost  Phantom  100  Recommended  Front-Desk Receptionist`, "Recommended: 1 of 1". An invalid alien is hired with a fabricated perfect score. |
| **After** — `AlienApplicant.Create("Ghost","Phantom",0,0,0,150)` | `Invalid(["Compatibility 150 is outside the allowed range 0-100."])`. No object exists, so the pipeline cannot receive it. |
| **After, from CSV** — line `Ghost,Phantom,0,0,0,150` | `Processed` with `Skipped = [Line 2: Compatibility 150 is outside the allowed range 0-100.]`; the report does not contain Ghost. |

Tests: `Invalid_GhostWithCompatibility150_IsRejectedAtConstruction`, `Invalid_GhostFromCsv_IsSkippedWithReason_AndNeverReachesTheReport`.

### B. Other inputs

| Input | Before | After |
|---|---|---|
| `new AlienApplicant(null, "Phantom", ...)` | `NullReferenceException` inside Stage 1 | `Invalid(["Name is required."])` (and `"Race is required."` for a null race) |
| `applicant with { Aggression = 500 }` | compiled; bypassed every rule | does not compile (get-only property) |
| `" ", Zorgian, -1, 65, 101, 90` | accepted by the constructor | `Invalid` with 4 reasons: name, tentacles −1, eyes 65, aggression 101 |
| `ApplicantIntake` with `lines = null` | `ParseLines(null)` throws `ArgumentNullException` | `Refused(["Input lines are missing."])` |
| policy `null` | `Run(..., null)` throws | `Refused(["Policy is missing."])` |
| normal sample file, Office Safety Policy | Mimi 100, Zorp 90 recommended, Xylo 58 not recommended; Blarg, Grakk, Vexa rejected | **identical** (behaviour preserved) |
| `Zorp` twice (`Zorp` / `ZORP`) | second rejected as duplicate | same; repeating the whole run gives the same outcome |

## 4. Required assertions

| Required check | Tests (`RefactorTests.cs` unless noted) |
|---|---|
| Normal | `Normal_RawLinesToReport_ThroughTheThreeStagePipeline`, `Normal_EmptyDataset_...`, `Normal_CheckedConstruction_AcceptsTheLimitsAndZero` |
| Invalid | `Invalid_GhostWithCompatibility150_...`, `Invalid_GhostFromCsv_...`, `Invalid_EveryViolatedRuleIsReported_...`, `Invalid_MissingPrerequisites_...`, `Invalid_NoPublicConstructor_...` (plus the `InvalidFields_...` theory in `PipelineTests.cs`) |
| Repeated operation | `Repeated_ProcessingTheSameInputTwice_GivesTheSameOutcome`, `Repeated_NormalizeIsIdempotent_AndRepeatedRecordIsRejectedAsDuplicate` |
| Input preservation | `InputPreservation_LinesAndApplicantsAreNeverChanged`, `InputPreservation_NormalizeReturnsANewValue_OriginalKeepsItsText` (plus `Stage1_DoesNotModifyCallerInput_...` in `PipelineTests.cs`) |

## 5. Honest limits

- The "before" results in section 3 are hand-traced; confirm them by running the Assignment 5 ZIP.
- `Run` still throws `ArgumentNullException` for null arguments (programming errors); the explicit `Refused` outcome is offered by `ApplicantIntake.Process`.
- Stage 2 still clamps scores returned by a *policy* delegate. That is a different module (policy construction) and was left unchanged.
