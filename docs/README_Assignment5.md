# Intergalactic Job Center — Assignment 5: Composing Functions Using Pipelining

Introduction to Functional Programming | Academic year 2026–2027 | Team of 4

## 1. Topic, users and workflow (Task 1)

**Topic.** The Intergalactic Job Center screens alien applicants who want a job on Earth.

**Intended users.** Earth HR officers who must decide which alien life forms to recommend for work.

**Operation implemented completely.** `validated applicants -> screened -> scored -> job-placement report`, run under one of two business policies.

### Input fields (CSV: `Name,Race,Tentacles,Eyes,Aggression,Compatibility`)

| Field | Type | Valid range |
|---|---|---|
| Name | text | required, no commas |
| Race | text | required |
| Tentacles | int | 0–64 (0 is valid) |
| Eyes | int | 0–64 (0 is valid) |
| Aggression | int | 0–100 |
| Compatibility (Earth atmosphere) | int | 0–100 |

### Business rules

| | Office Safety Policy | Extreme Conditions Policy |
|---|---|---|
| Max aggression (inclusive) | 30 | 80 |
| Min compatibility (inclusive) | 50 | 20 |
| Score | `(6*Compat + 4*(100-Aggr)) / 10` (integer division) | `min(Tent,10)*5 + min(Eyes,10)*3 + Compat/5` |
| Recommended when score ≥ | 65 | 60 |

Common rules: names/races are normalised (extra spaces collapsed, race capitalised); duplicate names (case-insensitive) are rejected, first one wins; scores are clamped to 0–100.

Job (only for recommended applicants): Tentacles ≥ 8 → *Multi-Task Operator*; else Eyes ≥ 6 → *Surveillance Analyst*; else Compatibility ≥ 90 → *Front-Desk Receptionist*; else *General Clerk*.

### Sample dataset and expected results (documented before running) — `data/applicants_sample.csv`

| Name | Tent | Eyes | Aggr | Compat | Purpose |
|---|---|---|---|---|---|
| Zorp | 6 | 2 | 10 | 90 | normal accepted |
| Blarg | 10 | 8 | 55 | 70 | rejected by Office, accepted by Extreme |
| Xylo | 2 | 2 | 30 | 50 | **boundary**: exactly the Office limits |
| Grakk | 12 | 4 | 95 | 80 | rejected by both (too aggressive) |
| Mimi | 0 | 0 | 0 | 100 | zero values are valid |
| Vexa | 4 | 6 | 20 | 15 | rejected by both (incompatible) |

**Office Safety Policy:** Mimi 100 Recommended (Front-Desk Receptionist), Zorp 90 Recommended (Front-Desk Receptionist), Xylo 58 Not recommended. Rejected: Blarg, Grakk, Vexa. Recommended: 2 of 3.

**Extreme Conditions Policy:** Blarg 88 Recommended (Multi-Task Operator), Zorp 54, Xylo 26, Mimi 20 Not recommended. Rejected: Grakk, Vexa. Recommended: 1 of 4.

**Outcome changes with the policy:** Blarg Rejected → Recommended; Mimi Recommended → Not recommended; Zorp Recommended → Not recommended.

## 2. Inputs and validation (Task 2)

- Immutable `record` models: `AlienApplicant`, `ParseFailure`, `ParseOutcome`, and all pipeline models. Collections that leave a function are `ImmutableArray<T>` copies, never the caller's mutable list.
- `ApplicantParser` is the only place where raw text becomes domain data. It uses `int.TryParse` with `CultureInfo.InvariantCulture`; every invalid field gives a clear message with the line number; nothing is replaced by a default and no null-forgiving operator is used.
- **Missing vs empty vs zero:** `null` dataset → `ArgumentNullException`; empty/header-only dataset → valid empty result; `0` for tentacles/eyes/aggression is accepted as a real value; an empty field is "missing" and rejected.

## 3. Pipeline stages (Task 3) — `src/IntergalacticJobCenter/Pipeline/Stages.cs`

| Stage | Signature | Rule | Example |
|---|---|---|---|
| 1 `FilterAndNormalize` | `IReadOnlyList<AlienApplicant>, int maxAggr, int minCompat -> ScreeningResult` | normalise text, drop duplicates, reject aggression > max or compatibility < min | `"  zorp "`, `"ZORGIAN"`, A=10, C=90, limits 30/50 → eligible `zorp`, `Zorgian` |
| 2 `ScoreApplicants` | `ScreeningResult, Func<AlienApplicant,int> -> ScoredBatch` | call the supplied delegate for every eligible applicant, clamp to 0–100 | Zorp under Office → 90 |
| 3 `BuildReport` | `ScoredBatch, string policyName, int minHireScore -> PlacementReport` | order by score, rank, decide, pick job | Mimi 100, Zorp 90, Xylo 58 at threshold 65 → #1 Rec, #2 Rec, #3 Not rec |

The composition is visible in `PlacementPipeline.Run`:

```csharp
var screened = Stages.FilterAndNormalize(applicants, policy.MaxAggression, policy.MinCompatibility);
var scored   = Stages.ScoreApplicants(screened, policy.Score);
return Stages.BuildReport(scored, policy.Name, policy.MinimumHireScore);
```

**Trace of one record (Zorp, Office policy):** CSV line `Zorp,Zorgian,6,2,10,90` → (parser) `AlienApplicant("Zorp","Zorgian",6,2,10,90)` → (Stage 1) in `ScreeningResult.Eligible` → (Stage 2) `ScoredApplicant(Zorp, 90)` → (Stage 3) `Placement(Rank 2, Zorp, Zorgian, 90, Recommended, "Front-Desk Receptionist")`.

## 4. Configurable rule (Task 4)

- **Higher-order function:** `Stages.ScoreApplicants(ScreeningResult, Func<AlienApplicant,int> scoreRule)`; the delegate invocation is `scoreRule(applicant)` inside it.
- **Two policies:** `ScoringPolicies.OfficeSafety` and `ScoringPolicies.ExtremeConditions` (immutable `ScoringPolicy` records holding limits, threshold and a `Func`).
- The policy is chosen in `Program.cs` (outside the pure pipeline) and passed as an argument. There is no mutable global setting.
- `PolicyComparison.Compare` shows which applicants change outcome when only the policy changes.

## 5. Purity (Task 5)

- Stages and `PlacementPipeline.Run` use no Console, files, clock, randomness, or mutable shared state. They never modify their inputs (local builders are created and discarded inside each call).
- All I/O is isolated in `ApplicantFileReader` (files) and `Program.cs` (Console). `ReportFormatter` only builds strings.

## 6. Build, run, test

**Prerequisites:** .NET SDK 8.0 or newer (`dotnet --version`), internet access for the first NuGet restore.
**Working directory:** the solution root (the folder containing `IntergalacticJobCenter.sln`), after extracting the ZIP.

```
dotnet restore
dotnet build
dotnet test
dotnet run --project src/IntergalacticJobCenter -- data/applicants_sample.csv both
```

Demonstrations:

```
dotnet run --project src/IntergalacticJobCenter -- data/applicants_sample.csv both      # normal, both policies + differences
dotnet run --project src/IntergalacticJobCenter -- data/applicants_sample.csv office    # one policy
dotnet run --project src/IntergalacticJobCenter -- data/applicants_sample.csv extreme   # one policy
dotnet run --project src/IntergalacticJobCenter -- data/applicants_invalid.csv both     # rejected rows with messages; the valid rows are still processed
dotnet run --project src/IntergalacticJobCenter -- data/applicants_empty.csv both       # valid empty dataset
```

## 7. Requirement → file/method map

| Requirement | Where |
|---|---|
| Workflow, dataset, expected results | this README, `data/applicants_sample.csv` |
| Immutable typed models | `Models/*.cs` |
| Boundary parsing / validation | `Input/ApplicantParser.cs` (`ParseLines`, `TryParseLine`) |
| 3 pure stages | `Pipeline/Stages.cs` (`FilterAndNormalize`, `ScoreApplicants`, `BuildReport`) |
| Composition | `Pipeline/PlacementPipeline.cs` (`Run`) |
| Higher-order function + delegate call | `Stages.ScoreApplicants` → `scoreRule(applicant)` |
| Two policies | `Policies/ScoringPolicies.cs` |
| Policy chosen outside the pipeline | `Program.cs` (`mode switch`) |
| Different result per policy | `Pipeline/PolicyComparison.cs`, test `TwoPolicies_...` |
| Isolated I/O | `Input/ApplicantFileReader.cs`, `Program.cs`, `Presentation/ReportFormatter.cs` |
| Automated tests | `tests/IntergalacticJobCenter.Tests/PipelineTests.cs` |

## 8. Automated tests (xUnit, all in `PipelineTests.cs`)

| Required case | Tests | Expected |
|---|---|---|
| Normal workflow | `NormalWorkflow_OfficeSafety_...`, `NormalWorkflow_ExtremeConditions_...` | documented tables in section 1 |
| Empty dataset | `EmptyDataset_...`, `HeaderOnlyFile_...` | no placements, no rejections, 0 recommended |
| Boundary value | `Boundary_ScreeningLimitsAreInclusive`, `Boundary_HireThresholdIsInclusive`, `ZeroIsALegitimateValue_NotMissing` | aggression 30 / compat 50 accepted, 31 / 49 rejected; score 65 recommended, 64 not |
| Invalid and missing input | `InvalidFields_AreRejectedWithClearMessage` (9 cases), `InvalidRows_...`, `MissingInput_IsRejected` | rejected with message, exceptions for null |
| Two policies | `TwoPolicies_SameInput_DifferentDocumentedResults`, `HigherOrderStage_InvokesTheSuppliedDelegate` | Blarg Rejected → Recommended, etc. |
| Purity and stages | `Stage1_DoesNotModifyCallerInput_...`, `Stage1_RejectsDuplicateNames_...`, `Pipeline_IsDeterministic_...` | input list and records unchanged; same input → same output |

**Latest test outcome:** _fill in after running `dotnet test` on a clean extraction (date, passed/failed counts)._

## 9. Team and individual contributions

| Member | Group | Responsibility | Files |
|---|---|---|---|
| Dinmukhammet Imanalyuly | [fill in] | Tasks 1 & 2: domain models, boundary validation, datasets | `Models/AlienApplicant.cs`, `Models/ParseModels.cs`, `Input/ApplicantParser.cs`, `Input/ApplicantFileReader.cs`, `data/*.csv` |
| Aisha Tynyshtykbay | IT-2505 | Task 3: the three pure stages | `Models/PipelineModels.cs`, `Pipeline/Stages.cs` |
| Yeraly Seitzhan | [fill in] | Task 4: policies, higher-order use, pipeline wiring, policy comparison, console demo | `Policies/*.cs`, `Pipeline/PlacementPipeline.cs`, `Pipeline/PolicyComparison.cs`, `Presentation/ReportFormatter.cs`, `Program.cs` |
| Bertan Maidabek | [fill in] | Tests and documentation | `tests/**`, `README.md`, `.gitignore`, solution/project files |

Each member must be able to explain and modify their own part during the defense (for example: change a threshold in `ScoringPolicies`, add a validation rule in `ApplicantParser`, change a job rule in `Stages.JobFor`, update the matching test).
