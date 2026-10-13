# Intergalactic Job Center — Final Project

Introduction to Functional Programming | Academic year 2026–2027 | C# / .NET 8 console application

| Item | Value |
|---|---|
| Repository URL | [fill in] |
| Branch | [fill in] |
| Final commit identifier | [fill in] |
| .NET SDK | 8.0 or newer (`dotnet --version`) |
| Native toolchain | C99 compiler (`gcc` or `clang`), or MSVC `cl` on Windows |

Team: Dinmukhammet Imanalyuly, Aisha Tynyshtykbay (IT-2505), Yeraly Seitzhan, Bertan Maidabek (groups of the other members: [fill in]).

## 1. Problem, users, goals

Earth HR officers receive applications from alien life forms (name, race, tentacles, eyes, aggression 0–100, atmosphere compatibility 0–100). The application screens them under a policy, scores them, places approved applicants through a safe approve → confirm sequence, prices each placement with a native C function, records confirmed placements in a persistent ledger, and produces reports. The design goal is a functional core (immutable models, pure functions, explicit outcomes) with all input and output at the edges.

## 2. Run the application

Working directory: the solution root (the folder containing `IntergalacticJobCenter.sln`).

```
sh native/build.sh
dotnet restore
dotnet build
dotnet test
dotnet run --project src/IntergalacticJobCenter
```

- `native/build.sh` builds the native library for Linux/macOS and runs its C self-test; on Windows run `native\build.cmd` from an "x64 Native Tools Command Prompt for VS". The ZIP already contains `native/runtimes/linux-x64/libijc_native.so` (Linux x86-64, gcc 13.3.0), so that platform can skip the step. Without the binary, workflows that need the fee report a clear `NativeLibraryUnavailable` failure.
- Start with no argument (or `app`) for the interactive menu: `dotnet run --project src/IntergalacticJobCenter -- app [applicantsFile] [ledgerFile] [exportFile]`. Defaults: `data/applicants_sample.json`, `output/placements.json`, `output/shortlist.csv`. Paths are relative to the working directory.
- A start date must be on or after the current day, because the application uses the system date as "today".
- Scripted demos of earlier stages: `-- workflow`, `-- result`, `-- reports`, and `-- data/applicants_sample.csv both`.

### Menu workflows

| # | Command | Workflow (input → rules → result) |
|---|---|---|
| 1 | Load applicants file | path → format and content validation, all-or-nothing → new registry, or the previous one kept with the problem shown |
| 2 | Screen and score under a policy | policy key → pipeline (filter/normalise → score with the policy delegate → placement report) → ranked recommendations and rejections |
| 3 | Place an applicant | name, policy, application id, officer, start date → lookup, validated factory, approve, confirm → receipt, or the first failure |
| 4 | Hiring shortlist with native fees | scoring choice (curried configurations), minimum score, minimum compatibility → shortlist + native fee + risk overview |
| 5 | Risk overview | minimum compatibility → grouping by aggression band with totals |
| 6 | Place and record | as 3, plus the native fee, ledger rules (unique application id, one placement per applicant) and a saved JSON ledger |
| 7 | Export the shortlist | as 4 → CSV file written through a temp file |
| 8 | Show the ledger | ledger → entries and a by-job summary |

## 3. Architecture

| Layer | Folders | Pure? |
|---|---|---|
| Domain models | `Models/` | immutable records, checked factory `AlienApplicant.Create` |
| Functional primitives | `Functional/` | `Result<T,E>`, `Maybe<T>`, `AppError`, `Outcomes` |
| Pipeline and policies | `Pipeline/`, `Policies/` | pure stages, higher-order function, two policies |
| Safe operation sequence | `Workflow/` | validated factory, closed state hierarchy, pattern-matched transitions |
| Queries | `Queries/`, `Reports/`, `Lookup/` | extensions, LINQ reports, curried scoring, `yield` scoring |
| Services (composition) | `Services/` | `Map`/`Bind` chains over the pure parts |
| Boundaries (side effects) | `Boundary/`, `Persistence/`, `Streaming/`, `Native/`, `Reports/ReportExporter.cs`, `Presentation/` | files, console, native library |

Data flow: file → `ApplicantFileLoader` (boundary, validation) → `ApplicantRegistry` (immutable snapshot) → pure pipeline/reports/chains → `Result` → console text or file output. No pure function reads the console, files, the clock or a mutable global; the current date and the fee adapter are passed in.

## 4. Final requirements → implementation

| Requirement | Where |
|---|---|
| ≥ 2 related domain models | `AlienApplicant`, `ValidatedAlienApplication`, `ApplicationState`, `PlacementReceipt`, `LedgerEntry` |
| ≥ 6 workflows (team of 4) | section 2 (eight menu commands) |
| Functional core, immutable models, pattern matching | `Models/*`, `Workflow/ApplicationWorkflow.cs` (switch expressions over every state/command pair) |
| Pipeline ≥ 3 stages | `Pipeline/Stages.cs` (`FilterAndNormalize`, `ScoreApplicants`, `BuildReport`), composed in `Pipeline/PlacementPipeline.cs` |
| Higher-order function changing a rule | `Stages.ScoreApplicants(screening, Func<AlienApplicant,int>)`, policies in `Policies/ScoringPolicies.cs` |
| Safe operation sequence | `ValidatedAlienApplication.Create` (private constructor), `ApplicationState` (private base constructor), `ApplicationWorkflow.Apply` |
| Result for ≥ 3 domain failures | `ApplicantNotFound`, `PolicyNotFound`, `ApplicationRejected`, `TransitionRejected`, `InvalidInput`, `DuplicateApplication`, `ApplicantAlreadyPlaced` |
| `Map` and `Bind`, failure skips later stages | `Functional/Result.cs`, chain in `Services/PlacementService.cs` |
| Missing lookups explicit | `Functional/Maybe.cs`, `Lookup/ApplicantRegistry.cs`, `Lookup/PolicyCatalog.cs` |
| Operational exceptions at boundaries | `Boundary/ApplicantFileLoader.cs`, `Persistence/LedgerStore.cs`, `Reports/ReportExporter.cs`, `Native/FeeAdapter.cs` |
| ≥ 2 extensions used | `Queries/ResultSequenceExtensions.cs`, `ApplicantQueryExtensions.cs`, `DeferredScoring.cs` |
| 2 LINQ reports (filter, projection, ordered with tie-break, GroupBy, aggregate) | `Reports/ShortlistReport.cs`, `Reports/RiskReport.cs`, `Reports/LedgerReport.cs` |
| `yield return`, deferred execution, deliberate materialisation | `Queries/DeferredScoring.cs`, `Streaming/LineStreaming.cs`, `ToImmutableArray` in the reports |
| Currying / partial application | `Queries/ScoreFunctions.cs` |
| Persistence: load JSON/CSV, save or export | load: `ApplicantFileLoader`; save: `LedgerStore.Save`; export: `ReportExporter.Export` |
| Native C library through P/Invoke | `native/`, `Native/NativeMethods.cs`, `Native/FeeAdapter.cs` |
| Tests | `tests/IntergalacticJobCenter.Tests/` (section 6) |

## 5. Domain rules (summary)

- Applicant: name and race required; tentacles and eyes 0–64; aggression and compatibility 0–100; zero is valid data.
- Office Safety Policy: aggression ≤ 30, compatibility ≥ 50, score `(6·C + 4·(100−A)) / 10`, recommended at ≥ 65. Extreme Conditions Policy: aggression ≤ 80, compatibility ≥ 20, score `min(T,10)·5 + min(E,10)·3 + C/5`, recommended at ≥ 60.
- Application factory: id of 5–20 letters, digits or hyphens; the applicant must satisfy the policy limits and reach the hire threshold (inclusive).
- Transitions: Pending → Approved (non-blank officer) or Rejected (non-blank reason); Approved → Confirmed (start date not before today); Rejected and Confirmed are final; repeating the exact command that produced the current state is a no-op, anything else is refused and the state is preserved.
- Fee (native): `50000 + 250·score + 1500·tentacles` cents, plus `10000` when score ≥ 90; invalid input returns `-1`. Example: score 90, tentacles 6 → 91500 cents.
- Ledger: application ids are unique (case-insensitive) and an applicant can have one placement.

## 6. Automated tests (xUnit)

141 `[Fact]` and 15 `[Theory]` methods (213 cases counting each `[InlineData]` row), far above the required 12 (8 pure/validation, 2 workflow, 2 file).

| File | Covers |
|---|---|
| `RefactorTests.cs`, `ResultTests.cs` | checked applicant factory, `Result` states, `Map`, `Bind`, proof that failures skip functions |
| `PipelineTests.cs` | the three stages, boundaries, empty and invalid data, two policies, purity |
| `WorkflowTests.cs` | factory, closed hierarchy, transition table, missing prerequisite, repeated operation, state preservation |
| `MaybeLookupTests.cs` | present and absent lookups, zero/false/empty string are present |
| `PlacementChainTests.cs` | the integrated `Map`/`Bind` chain, three kinds of domain failure |
| `ReportAndExtensionTests.cs` | both reports, ties, empty, no match, band boundaries, the extensions |
| `ConfiguredAndDeferredTests.cs` | curried scoring, partial applications, deferred execution and materialisation |
| `ResourceTests.cs` | resource disposal on success, early stop and failure, exporter safety |
| `FileBoundaryTests.cs` | missing files and malformed CSV/JSON, all-or-nothing loading, safe reload |
| `NativeIntegrationTests.cs` | real native calls against manual values, adapter and load failures, native-backed workflows |
| `LedgerTests.cs` | ledger rules, by-job report, JSON store round trip, missing/malformed ledger files, desk workflow |
| `ConsoleApplicationTests.cs` | complete menu workflows (normal and failure), damaged ledger, end of input, input parsing |

**Latest test outcome:** [10.11.2026, passed: 213, failed: 0].

## 7. Resource and retention note

Datasets are materialised in `ApplicantFileLoader.Load` (`ImmutableArray`), `ApplicantRegistry`, each report snapshot and `PlacementLedger`. Deferred queries (`ScoreWith`) hold their source and scorer only until consumed; the reports return snapshots. No caches, long-lived mutable collections or event subscriptions exist. CSV lines are streamed one at a time and parsed into a snapshot inside the loader call, so the file handle is closed before the call returns; the `StreamWriter` of the exporter and ledger store is disposed by `using`, and both write a temp file and move it, so a failure never damages an existing file. Trade-off: streaming uses memory for one line but can be consumed once; a snapshot costs memory for all records but can be reused safely.

## 8. Native library

| Item | Value |
|---|---|
| Export | `int32_t ijc_placement_fee_cents(int32_t score, int32_t tentacles)` in `native/ijc_native.c` |
| Valid range / invalid | 0–100 score, 0–64 tentacles / returns `-1` |
| Binding | `[DllImport("ijc_native", EntryPoint = "ijc_placement_fee_cents", CallingConvention = Cdecl)]` in `Native/NativeMethods.cs` |
| Build (Linux x86-64, gcc 13.3.0, Ubuntu 24.04) | `cc -std=c99 -O2 -Wall -Wextra -Werror -fvisibility=hidden -shared -fPIC -o native/runtimes/linux-x64/libijc_native.so native/ijc_native.c` |
| Build (Windows x64) | `native\build.cmd` (MSVC) |
| Loading | the `.csproj` copies the binary of the current OS and architecture next to the assembly; the adapter turns load and binding failures into `Result` failures |

## 9. Limitations

- The native binary shipped in the archive targets Linux x86-64; other platforms build it with the provided scripts.
- The interface is a console menu; there is no web or desktop interface, database, login or multithreading.
- Applicant names must not contain commas in the CSV input format (JSON has no such limit).

## 10. References

Microsoft documentation for .NET 8, `System.Text.Json`, LINQ and native interop (P/Invoke); xUnit documentation; course lectures and Assignments 5–8 (`docs/`). Generative AI assistance acknowledgement: [fill in according to the syllabus and the instructor's permission].

## 11. Responsibilities

| Member | Main modules |
|---|---|
| Dinmukhammet Imanalyuly | `Models/`, `Input/`, `Workflow/ValidatedAlienApplication.cs`, `ApplicationRules.cs`, `ApplicationCreation.cs`, `Functional/Result.cs`, `AppError.cs`, `ErrorCode.cs`, `Outcomes.cs`, `Queries/ResultSequenceExtensions.cs`, `ApplicantQueryExtensions.cs`, `Reports/ShortlistReport.cs`, `RiskReport.cs` |
| Aisha Tynyshtykbay | `Pipeline/Stages.cs`, `Models/PipelineModels.cs`, `Workflow/ApplicationState.cs`, `WorkflowCommand.cs`, `TransitionResult.cs`, `Functional/Maybe.cs`, `Lookup/`, `Queries/ScoreFunctions.cs`, `DeferredScoring.cs`, `Presentation/ConsoleApplication.cs`, `InputParsing.cs`, `ReportText.cs` |
| Yeraly Seitzhan | `Policies/`, `Pipeline/PlacementPipeline.cs`, `PolicyComparison.cs`, `Workflow/ApplicationWorkflow.cs`, `Services/`, `Reports/ReportWorkflow.cs`, `PricedRow.cs`, `Presentation/ReportFormatter.cs`, `ResultFormatter.cs`, `WorkflowFormatter.cs`, the demo classes, `Program.cs` |
| Bertan Maidabek | `Boundary/`, `Persistence/`, `Reports/LedgerReport.cs`, `ReportExporter.cs`, `Streaming/`, `Native/`, `native/`, the `.csproj` copy items, test infrastructure, this README |

Tests: Dinmukhammet — `RefactorTests`, `ResultTests`, `ReportAndExtensionTests`; Aisha — `PipelineTests` (stages), `MaybeLookupTests`, `ConfiguredAndDeferredTests`, `ConsoleApplicationTests`; Yeraly — `WorkflowTests`, `PlacementChainTests`, policy tests in `PipelineTests`; Bertan — `FileBoundaryTests`, `ResourceTests`, `NativeIntegrationTests`, `LedgerTests`.
