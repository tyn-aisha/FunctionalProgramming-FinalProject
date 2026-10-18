# Intergalactic Job Center — Assignment 7: Exception Handling in FP

Introduction to Functional Programming | Academic year 2026–2027 | Team of 4
Earlier stages are kept and still active: the pure pipeline and checked `AlienApplicant.Create` (Assignment 5 and its refactoring, `docs/REFACTORING.md`), the validated factory and safe state transitions (Assignment 6, `docs/README_Assignment6.md`). This stage adds explicit failures (`Result`), explicit absence (`Maybe`), `Map`/`Bind` composition, and file-exception handling at one boundary.

## 1. What is new

| Need | Type / code | Meaning |
|---|---|---|
| expected failure | `Result<T, E>` = `Ok(Value)` / `Err(Error)`; here `E` = `AppError` | an operation that can fail for business or environment reasons |
| absence | `Maybe<T>` = `Present(Value)` / `Absent` | a lookup that may find nothing |
| validation outcome | `ApplicationCreation` (A6), `ApplicantCreation` (refactoring) | the factory accepted the data or lists every violated rule |
| workflow state | `ApplicationState` (A6) | where an application is in its life cycle (Pending / Approved / Rejected / Confirmed) |
| transition outcome | `TransitionResult` (A6) | what happened when a command was applied to a state |

A **validation failure** says "this data breaks a rule" and has no state yet. A **workflow state** says "this valid application is currently here". They are different types on purpose; `PlacementService` converts the factory outcome and the transition outcome into `Result` so they can be chained.

## 2. Task 1 — Explicit domain outcomes (`Result<T, E>`)

`Functional/Result.cs`: the base constructor is private (only `Ok` and `Err` exist), case constructors are internal and reject `null` (a success without a payload or a failure without an error cannot be built), and the payload is only reachable after the case is established (pattern matching, `Match`, `Map`, `Bind`). `AppError` (`Functional/AppError.cs`) carries `Code`, `Subject` (the item or rule involved) and `Message`; blank subjects/messages are refused. Domain failures (`AppError.Domain`) and operational problems (`AppError.Operational`) are separate cases. Business failures are returned, never thrown.

### Documented outcomes of `PlacementService.Place(registry, request)`

| # | Case | Request | Success value / Error |
|---|---|---|---|
| — | **Success** | Zorp, `office`, APP-001, Officer Kim, start +7 days | `Ok(PlacementReceipt)`: Zorp, Front-Desk Receptionist, score 90, approved by Officer Kim, start 2026-10-25 |
| 1 | **Rule violated** (invalid value for the policy) | Blarg, `office` | `Err(Domain ApplicationRejected)`, subject `Blarg`, message: `Aggression 55 exceeds maximum 30 of Office Safety Policy. Score 60 is below the hire threshold 65 of Office Safety Policy.` |
| 2a | **Missing required item** (applicant) | `Nobody` (or a blank name) | `Err(Domain ApplicantNotFound)`, subject `Nobody` (`(blank)`), message `Applicant 'Nobody' is not in the registry.` |
| 2b | **Missing required item** (policy) | policy `mars` | `Err(Domain PolicyNotFound)`, subject `mars`, message `Policy 'mars' does not exist. Use 'office' or 'extreme'.` |
| 3a | **State/operation does not permit it** | start date before today | `Err(Domain TransitionRejected)`, subject `APP-001`, message `InvalidInput: Start date 2026-10-17 is before today (2026-10-18).` |
| 3b | same | blank officer | `Err(Domain TransitionRejected)`, message `InvalidInput: Approving officer is required.` |
| 3c | same | `WorkflowSteps.Reject` on a Confirmed application | `Err(Domain TransitionRejected)`, message `InvalidTransition: A confirmed application is final and cannot be rejected.` |

Blarg shows that the outcome depends on the policy: the same request with `extreme` succeeds (score 88, Multi-Task Operator).

## 3. Task 2 — Missing values (`Maybe<T>`)

| Lookup | Returns | Used as |
|---|---|---|
| `ApplicantRegistry.Find(name)` | `Maybe<AlienApplicant>` (trimmed, case-insensitive; blank name = Absent) | **required** in the chain: `ToResult` turns Absent into `ApplicantNotFound` |
| `PolicyCatalog.Find(key)` | `Maybe<ScoringPolicy>` | **required**: Absent becomes `PolicyNotFound` |
| `ApplicantRegistry.FindAggression(name)` | `Maybe<int>` | **optional/informational**: shown as present or absent in the demo; no failure is created |

Presence is decided by the case, not by the value: `Maybe.Some(0)`, `Maybe.Some(false)` and `Maybe.Some("")` are **Present**; only a real `null` (`FromReference`, `FromNullable`) is Absent. Mimi's aggression of 0 is `Present(0)`. A `Present` can never hold `null`; no `default(T)`, fabricated record or null-forgiving operator is used. The error for a missing required item is built lazily, only when the value is absent.

## 4. Task 3 — `Map` and `Bind` in a real chain (`Services/PlacementService.cs`)

```csharp
return FindApplicant(registry, request.ApplicantName)                                  // Result<AlienApplicant, AppError>
    .Bind(applicant => FindPolicy(request.PolicyKey)                                   // Bind -> Result<Candidate, AppError>
        .Map(policy => new Candidate(applicant, policy)))                              //   (Map inside)
    .Bind(candidate => CreateApplication(request.ApplicationId, candidate))            // Bind -> Result<ValidatedAlienApplication, AppError>
    .Map(ApplicationWorkflow.Start)                                                    // Map  -> Result<ApplicationState, AppError>
    .Bind(state => WorkflowSteps.Approve(state, request.Officer))                      // Bind -> Result<ApplicationState, AppError>
    .Bind(state => WorkflowSteps.Confirm(state, request.StartDate, request.Today))     // Bind -> Result<ApplicationState.Confirmed, AppError>
    .Map(PlacementReceipt.From);                                                       // Map  -> Result<PlacementReceipt, AppError>
```

| Combinator | Signature | When to use | Why the return types differ |
|---|---|---|---|
| `Map` | `Result<T,E>.Map(Func<T,U>) -> Result<U,E>` | the next step is a pure function that cannot fail (`Start`, `PlacementReceipt.From`) | the function returns a plain `U`, `Map` wraps it |
| `Bind` | `Result<T,E>.Bind(Func<T,Result<U,E>>) -> Result<U,E>` | the next step can itself fail (lookup, factory, transition) | the function already returns a `Result`; `Bind` does not wrap it again |

On `Err`, both return `Err(sameError)` **without calling** the supplied function and without touching a payload. Example: an unknown applicant with a blank officer and a past start date reports only `ApplicantNotFound`; validation and transitions never run. The result is handled **once** at the boundary, by pattern matching in `Presentation/ResultFormatter.cs` (`Describe`).

**Trace of Zorp (Office, success):** `Find("Zorp")` → `Present` → `Ok(AlienApplicant)` → policy `Present` → `Ok(Candidate)` → factory `Created` → `Ok(ValidatedAlienApplication)` → `Map(Start)` → `Ok(Pending)` → Approve → `Ok(Approved by Officer Kim)` → Confirm → `Ok(Confirmed 2026-10-25)` → `Map(From)` → `Ok(PlacementReceipt)`.

## 5. Task 4 — Operational exceptions at the boundary (`Boundary/ApplicantFileLoader.cs`)

| Problem | Exception caught (specific types) | Result |
|---|---|---|
| missing file or directory | `FileNotFoundException`, `DirectoryNotFoundException` | `Err(Operational FileNotFound)`, subject = path |
| access denied / I/O problem | `UnauthorizedAccessException`, `IOException` | `Err(Operational FileUnreadable)` |
| broken JSON, wrong value type, non-array root, empty file | `JsonException` | `Err(Operational MalformedContent)` naming the file |
| JSON entry with a missing field or out-of-range value, or a CSV row that is invalid | no exception; checked via `AlienApplicant.Create` / `ApplicantParser` | `Err(Operational MalformedContent)` listing `Entry n` / `Line n` problems |
| blank/invalid path, unsupported extension | `ArgumentException`, `NotSupportedException` / explicit check | `Err(Operational InvalidPath / UnsupportedFormat)` |

- There is no empty catch and no catch-all that reports success; every catch returns a failure containing the file and the exception message. Programming errors (`InvalidOperationException`, null arguments) are not caught.
- **Domain failure vs operational problem:** a domain failure (`AppError.Domain`) means the data was read fine and a rule or required item says no. An operational problem (`AppError.Operational`) means the file or its content could not be used. They are printed with different labels.
- File access exists only in this class (and the demo that calls it); the domain functions receive already-checked values.
- **No partial update:** loading is all-or-nothing (a file with one bad row is refused entirely). `ApplicantFileLoader.Reload(current, path)` returns `Reloaded(newRegistry)` or `Kept(current, problem)`. **Intentional fallback:** when a reload fails, the previous registry stays in use, completely unchanged (the same object); the degraded outcome is visible because `Kept` carries the problem and the demo prints `KEPT previous registry ... - [OPERATIONAL ...]`.
- Formats: CSV (`data/applicants_sample.csv`) and JSON (`data/applicants_sample.json`, array of `{name, race, tentacles, eyes, aggression, compatibility}`).

## 6. Task 5 — Operational paths in the console

Run from the solution root: `dotnet run --project src/IntergalacticJobCenter -- result` (optional second argument: data directory, default `data`).

| Path | Demo line | Expected output (key part) |
|---|---|---|
| Data loading | section 1 | `OK  data/applicants_sample.csv: 6 applicants` and the same for the JSON file (path separators follow the OS) |
| Lookup present | section 2 | `Find("Zorp") -> present: Zorp, aggression 10`; `FindAggression("Mimi") -> present: 0 (zero is real data, not absence)`; `PolicyCatalog "office" -> present: Office Safety Policy` |
| Lookup missing | section 2 | `Find("Nobody") -> absent`; `FindAggression("Nobody") -> absent`; `PolicyCatalog "mars" -> absent` |
| Normal path | section 3 | `OK  Zorp (APP-001) placed as Front-Desk Receptionist, score 90, approved by Officer Kim, starts 2026-10-25`; `OK  Blarg (APP-002) placed as Multi-Task Operator, score 88, ...` |
| Domain failure 1 | section 3 | `FAILED [DOMAIN ApplicationRejected] Blarg: Aggression 55 exceeds maximum 30 ...` |
| Domain failure 2 | section 3 | `[DOMAIN ApplicantNotFound] Nobody: ...` and `[DOMAIN PolicyNotFound] mars: ...` |
| Domain failure 3 | section 3 | `[DOMAIN TransitionRejected] APP-006: InvalidInput: Start date 2026-10-17 is before today (2026-10-18).`, blank officer, and rejecting a confirmed application (`InvalidTransition`) |
| File failure 1 | section 4 | `KEPT previous registry (6 applicants) - [OPERATIONAL FileNotFound] data/does_not_exist.csv: File not found ...` |
| File failure 2 | section 4 | `KEPT ... [OPERATIONAL MalformedContent] ... Malformed JSON in '.../applicants_malformed.json' ...` and for `applicants_invalid.csv`: `6 invalid row(s)` |
| Recovery | section 4 | `RELOADED new registry with 6 applicants` for the valid JSON file |

No unhandled exception is produced by any of these demonstrations.

## 7. Build, run, test

**Prerequisites:** .NET SDK 8.0 or newer (`dotnet --version`); internet access for the first NuGet restore.
**Working directory:** the solution root (the folder containing `IntergalacticJobCenter.sln`) after extracting the ZIP.

```
dotnet restore
dotnet build
dotnet test
dotnet run --project src/IntergalacticJobCenter -- result                                  # Assignment 7 demo
dotnet run --project src/IntergalacticJobCenter -- workflow                                # Assignment 6 demo
dotnet run --project src/IntergalacticJobCenter -- data/applicants_sample.csv both         # Assignment 5 demo
```

## 8. Requirement → file/method map

| Requirement | Where |
|---|---|
| Result with protected states | `Functional/Result.cs`, `Functional/AppError.cs`, `Functional/ErrorCode.cs`, `Functional/Outcomes.cs` |
| ≥ 3 domain failures | `Services/PlacementService.cs` (`FindApplicant`, `FindPolicy`, `CreateApplication`), `Services/WorkflowSteps.cs` |
| Maybe and real lookups | `Functional/Maybe.cs`, `Lookup/ApplicantRegistry.cs` (`Find`, `FindAggression`), `Lookup/PolicyCatalog.cs` |
| Missing → failure | `Maybe<T>.ToResult` used in `PlacementService.FindApplicant` / `FindPolicy` |
| Map and Bind | `Result<T,E>.Map` / `.Bind`, used in `PlacementService.Place` |
| Final outcome handled once | `Presentation/ResultFormatter.cs` (`Describe`), `Presentation/ResultDemo.cs` |
| Boundary exceptions | `Boundary/ApplicantFileLoader.cs` (`Load`, `Reload`), `Boundary/ReloadOutcome.cs`, `Boundary/ApplicantDto.cs` |
| Earlier code kept active | pipeline (`Pipeline/*`), `AlienApplicant.Create`, `ValidatedAlienApplication.Create`, `ApplicationWorkflow.Apply` (all called by `PlacementService`) |
| Console paths | `Presentation/ResultDemo.cs`, `Program.cs` (`result` argument), `data/*.csv`, `data/*.json` |
| Tests | `tests/IntergalacticJobCenter.Tests/ResultTests.cs`, `MaybeLookupTests.cs`, `PlacementChainTests.cs`, `FileBoundaryTests.cs` (plus earlier test files) |

## 9. Automated tests (xUnit)

The four new files hold 33 `[Fact]` and 2 `[Theory]` methods (41 cases when each `[InlineData]` row is counted). The earlier `PipelineTests.cs`, `RefactorTests.cs` and `WorkflowTests.cs` stay in the suite, so the cumulative total is far above the required 10. File tests write into a fresh temporary directory and delete it in `Dispose`.

| Required case | Tests | Expected |
|---|---|---|
| Complete success | `CompleteSuccess_ChainProducesTheDocumentedReceipt`, `..._SamePersonUnderTheOtherPolicy_...`, `..._ZeroValuesAreAcceptedAsRealData` | receipt Zorp / 90 / Front-Desk Receptionist / 2026-10-25; Blarg 88 under `extreme` |
| Domain failures (3 types) | `DomainFailure1_RuleViolated_...`, `DomainFailure2_MissingRequiredItem_...`, `DomainFailure3_StateDoesNotPermit_...` | codes `ApplicationRejected`, `ApplicantNotFound` / `PolicyNotFound`, `TransitionRejected` with useful subject and message |
| Lookup present and absent | `Lookup_PresentAndAbsent_BothWork`, `Lookup_BlankNameIsSimplyAbsent_...` (3 rows), `Lookup_ZeroAggressionIsPresent_NotAbsent`, `Maybe_ZeroFalseAndEmptyStringArePresent_OnlyNullIsAbsent`, `PolicyCatalog_PresentAndAbsent`, `RequiredLookup_MissingBecomesAFailure_...` | present vs absent; 0, false, "" are Present |
| Map success | `Map_SuccessReceivesTheValueAndReturnsTheNewValue` | function receives 3, result `value-6` |
| Bind success | `Bind_SuccessReceivesTheValue_AndKeepsTheNextResult`, `Bind_PreservesAFailureReturnedByTheNextOperation` | function receives 3; inner Result preserved |
| Failure propagation | `FailurePropagation_MapAndBindDoNotInvokeTheirFunctions`, `FailurePropagation_ALongChainStopsAtTheFirstFailure`, `Maybe_MapAndBind_AreSkippedWhenAbsent_...`, `DomainFailure_EarlierFailureWinsAndLaterStepsAreSkipped` | counters stay 0; executed steps are exactly `map-1`, `bind-fails` |
| Missing file | `MissingFile_IsReportedWithTheFileName_AndTheRegistryIsPreserved`, `MissingDirectory_BlankPathAndUnsupportedType_...` | `FileNotFound` with the path; `Kept` holds the same registry object |
| Malformed file | `MalformedJson_BecomesAClearFailure` (5 rows), `MalformedJson_NullRoot_...`, `MalformedJson_MissingFieldAndOutOfRangeValue_...`, `MalformedCsv_IsAllOrNothing_...` | `MalformedContent`; the valid row of a bad file is not applied |
| Result protection | `Result_ContradictoryStatesCannotBeBuilt`, `AppError_RequiresSubjectAndMessage` | null payload/error refused |
| Purity / state | `Chain_IsPure_...`, `Reload_Success_BuildsANewRegistry_...` | equal outcomes on repeat; old registry untouched |

**Latest test outcome:** _fill in after running `dotnet test` on a clean extraction (date, passed/failed counts)._

## 10. Team and individual contributions

| Member | Group | Task | Files |
|---|---|---|---|
| Dinmukhammet Imanalyuly | [fill in] | Task 1: `Result<T,E>` with protected states, `AppError`/`ErrorCode`, `Outcomes`, the three documented domain failures | `Functional/Result.cs`, `AppError.cs`, `ErrorCode.cs`, `Outcomes.cs`; `ResultTests.cs` (contradictory states, AppError) |
| Aisha Tynyshtykbay | IT-2505 | Task 2: `Maybe<T>`, registry and policy lookups, primitive-safe presence, missing → failure | `Functional/Maybe.cs`, `Lookup/ApplicantRegistry.cs`, `Lookup/PolicyCatalog.cs`; `MaybeLookupTests.cs` |
| Yeraly Seitzhan | [fill in] | Task 3: `Map`/`Bind` chain, workflow adapters, receipt, boundary pattern matching and console demo | `Services/PlacementService.cs`, `WorkflowSteps.cs`, `PlacementReceipt.cs`, `PlacementRequest.cs`; `Presentation/ResultFormatter.cs`, `ResultDemo.cs`; `Program.cs`; `PlacementChainTests.cs` |
| Bertan Maidabek | [fill in] | Task 4, tests and documentation: file boundary, malformed JSON/CSV, no partial update (`Reload`), temporary-file tests, Map/Bind proof tests, this README | `Boundary/*.cs`, `data/*.json`; `FileBoundaryTests.cs`; `ResultTests.cs` (Map/Bind/propagation); `README.md` |

Each member must be able to explain and change their own part in the defense (for example: add a fourth domain failure, change a policy threshold, add a new `Maybe` lookup, add a JSON field check, or assert that another dependent function is skipped).
