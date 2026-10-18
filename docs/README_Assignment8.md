# Intergalactic Job Center — Assignment 8: Extension Methods and LINQ

Introduction to Functional Programming | Academic year 2026–2027 | Team of 4
One runnable project across Assignments 5–8. Still active and reused here: the pure pipeline and checked `AlienApplicant.Create` (A5 + `docs/REFACTORING.md`), the validated factory and safe state transitions (A6, `docs/README_Assignment6.md`), `Result`/`Maybe`/`Map`/`Bind`, the placement chain and the file boundary (A7, `docs/README_Assignment7.md`). New in this stage: custom extensions, two LINQ reports, currying, `yield` processing, resource ownership, and a native C function called through P/Invoke.

## 1. Build, run, test

**Prerequisites:** .NET SDK 8.0 or newer (`dotnet --version`); a C99 compiler (`gcc`/`clang` on Linux/macOS, MSVC `cl` on Windows); internet access for the first NuGet restore.
**Working directory:** the solution root (the folder containing `IntergalacticJobCenter.sln`) after extracting the ZIP.

```
sh native/build.sh          # Linux/macOS: builds native/runtimes/<rid>/libijc_native.* and runs the C self-test
native\build.cmd            # Windows: run inside "x64 Native Tools Command Prompt for VS"
dotnet restore
dotnet build
dotnet test
dotnet run --project src/IntergalacticJobCenter -- reports      # Assignment 8 demo (writes output/shortlist_compatibility.csv)
```

The ZIP already contains the Linux x86-64 binary `native/runtimes/linux-x64/libijc_native.so`, so on that platform the native step can be skipped. On Windows or macOS build the library first; the native tests and the `reports` demo fail with a clear `NativeLibraryUnavailable` message until the binary exists. Earlier demos still work: `-- result`, `-- workflow`, `-- data/applicants_sample.csv both`.

## 2. Task 1 — Two (three) custom extension methods

| Extension | Receiver → return | Behaviour | Why it belongs on the receiver | Used in |
|---|---|---|---|---|
| `Sequence<T,E>` (`Queries/ResultSequenceExtensions.cs`) | `IEnumerable<Result<T,E>>` → `Result<ImmutableArray<T>,E>` | all values in order, or the **first** error; stops enumerating at the first failure; empty sequence = success with an empty array | "many operations that may fail → one outcome" is a property of the whole sequence of results | `ReportWorkflow.Price` (native fee per row) |
| `AtLeast` (`Queries/ApplicantQueryExtensions.cs`) | `IEnumerable<ScoredApplicant>` → `IEnumerable<ScoredApplicant>` | keeps `Score >= minScore` (inclusive) | the shortlist rule is a filter over scored applicants | `ShortlistReport.Build` |
| `RankByScore` (same file) | `IEnumerable<ScoredApplicant>` → ordered sequence | `OrderByDescending(Score).ThenBy(Name, Ordinal)`: deterministic ranking | one official ranking for every report | `ShortlistReport.Build` |
| `ScoreWith` (`Queries/DeferredScoring.cs`, Task 4) | `IEnumerable<AlienApplicant>` → lazy `IEnumerable<ScoredApplicant>` | `yield return` of applicant + clamped score | scoring is a transformation of an applicant sequence | `ShortlistReport.Build` |

All are pure: they never modify the source or any shared state, and each is called by application code (not only by tests).

## 3. Task 2 — Two reports (`Reports/`)

Both read the applicants of the registry loaded from `data/applicants_sample.json` (or `.csv`), are pure, and return `Result<ImmutableArray<Row>, AppError>` snapshots. Display (`Presentation/ReportsDemo.cs`) and export (`ReportExporter`) are separate.

### Report 1 — Hiring shortlist (`ShortlistReport.Build`), user: HR officer
- **Input:** applicants, a scoring function (Task 3), `minScore` (0–100; otherwise `Err Domain InvalidInput`, subject `minScore`).
- **Stages:** `ScoreWith` → `AtLeast` (**Where**) → `RankByScore` (**OrderByDescending + ThenBy**) → **Select** (rank, job) → `ToImmutableArray`.
- **Output fields:** Rank, Name, Race, Tentacles, Score, Job.
- **Expected** (`CompatibilityFocused`, min 60): 1 Mimi 100, 2 Zorp 90, 3 Blarg 65, 4 Grakk 65. Blarg and Grakk tie at 65; ordinal name order decides, whatever the input order. `CalmnessFocused`: Mimi 100, Zorp 90, Vexa 67, Xylo 66.

### Report 2 — Aggression risk overview (`RiskReport.Build`), user: safety officer
- **Input:** applicants, `minCompatibility` (0–100; otherwise `Err Domain InvalidInput`).
- **Stages:** **Where** (compatibility ≥ min) → **GroupBy** aggression band (Calm 0–20, Tense 21–50, Hostile 51–100) → **Select** (**Count**, **Sum** of tentacles, **Aggregate** for the maximum compatibility, ordered names) → **OrderByDescending(Count) + ThenBy(Band)** → snapshot.
- **Output fields:** Band, Count, TotalTentacles, MaxCompatibility, Names.
- **Expected** (min 20; Vexa has compatibility 15 and is filtered out): Calm 2 / 6 / 100 / "Mimi, Zorp"; Hostile 2 / 22 / 80 / "Blarg, Grakk"; Tense 1 / 2 / 50 / "Xylo". Calm and Hostile tie on Count, so the band name breaks the tie.

Edge cases tested: empty dataset, no matching records, tied sort values (input order reversed gives the same result), inclusive thresholds, band boundaries 0/20/21/50/51/100, invalid thresholds.

## 4. Task 3 — Currying and partial application (`Queries/ScoreFunctions.cs`)

`CreateScore : int -> int -> (AlienApplicant -> int)` is curried: compatibility weight, then calmness weight, then the applicant. Score = `(wC·Compatibility + wA·(100 − Aggression)) / (wC + wA)` (integer division). Weights must be non-negative and not both zero (`ArgumentOutOfRangeException` when the configuration is applied, before any applicant is scored).

Two partial applications: `CompatibilityFocused = CreateScore(80)(20)` and `CalmnessFocused = CreateScore(20)(80)`. They are passed into `ReportWorkflow.Run` / `ShortlistReport.Build` as arguments; no mutable global setting exists.

| Applicant | CompatibilityFocused | CalmnessFocused |
|---|---|---|
| Zorp | 90 | 90 |
| Blarg | 65 | 50 |
| Xylo | 54 | 66 |
| Grakk | 65 | 20 |
| Mimi | 100 | 100 |
| Vexa | 28 | 67 |

Same dataset, different result: the shortlist (min 60) is Mimi, Zorp, Blarg, Grakk versus Mimi, Zorp, Vexa, Xylo.

## 5. Task 4 — Deferred execution

`DeferredScoring.ScoreWith` is the `yield return` workflow over real applicants. Arguments are checked immediately; the body runs only during iteration. Probes live in the tests (and in the demo), never in the calculation.

| Moment (6 applicants) | Scorer calls |
|---|---|
| after creating the sequence | 0 |
| after `First()` | 1 |
| after enumerating the lazy sequence twice (`Count()` ×2) | 12 |
| after one `ToImmutableArray()` (deliberate materialisation point) | 6 |
| after reading the snapshot any number of times | still 6 |

**Before materialisation** every enumeration repeats the scoring (and would repeat a file read if the source were a file). **After** `ToImmutableArray`, reuse is free and predictable. The reports materialise exactly once at the end of the query. `LineStreaming` (section 6) is deliberately consumed only once.

## 6. Task 5 — Resource ownership and disposal

| Resource | Owner | Release point | Exception / early-stop path |
|---|---|---|---|
| `StreamReader` over a CSV file | iterator in `Streaming/LineStreaming.cs` (opened lazily on the first `MoveNext`) | `using` inside the iterator: end of enumeration, consumer stops early (`Take`, `break`), or a read error | tested with a tracking reader: disposed after success, `Take(1)`, and an `IOException` |
| `StreamWriter` (owns its `FileStream`) | `ReportExporter.WriteCsv` | `using var writer` | the export writes `path.tmp` and then moves it over the target; a `finally` removes a leftover `.tmp`; failures become `FileWriteFailed`; an existing report is never damaged |
| `File.ReadAllText` for JSON | .NET convenience API: opens, reads and closes the file internally; the caller never holds a handle | returns before the method exits | file errors are caught at `ApplicantFileLoader.Load` |
| CSV loading | `ApplicantFileLoader.Load` consumes the lazy lines **inside the call** (`ParseLines` → `ImmutableArray`) | the reader is closed before `Load` returns | tests open the file with `FileShare.None` afterwards: succeeds only if no handle is left |

Garbage collection is never relied on to close a file.

## 7. Task 6 — Retention and allocation note

- **Where data is materialised:** `ApplicantFileLoader.Load` (`ImmutableArray<AlienApplicant>`), `ApplicantRegistry` (one immutable array + dictionary), each report (`ToImmutableArray`), `ReportBundle`. Nothing else keeps data.
- **Deferred queries and closures:** `ScoreWith` captures its source and scorer only while the lazy sequence is alive; the reports consume it immediately and return snapshots, so no closure retains the applicants after the call. The curried functions capture two integers.
- **Caches, long-lived collections, events:** none are used. The registry never grows after construction; a reload builds a new one and the old one becomes unreachable when the caller drops it. No event subscriptions exist.
- **Design decision:** CSV lines are streamed one at a time and parsed into the final snapshot, so the full file text and a second list of lines are never held. JSON uses `ReadAllText` because `System.Text.Json` deserialises the whole array anyway.
- **Trade-off:** streaming keeps memory proportional to one line and delays the file open until iteration, but can be consumed once safely and keeps the file open during processing. A snapshot costs memory for all items but is reusable, thread-safe to read, and independent of the file. Because this dataset feeds two reports and an export, the snapshot is the right end state.
- No memory leak is claimed or fixed.

## 8. Tasks 7–9 — Native C library

### Function contract (`native/ijc_native.h`, `native/ijc_native.c`)

| Item | Value |
|---|---|
| Export | `int32_t ijc_placement_fee_cents(int32_t score, int32_t tentacles)` |
| Meaning | placement fee in **integer cents** (1 credit = 100 cents); no floating point, so no rounding |
| Formula | `50000 + 250·score + 1500·tentacles`, plus `10000` when `score >= 90` |
| Valid range | `0 ≤ score ≤ 100`, `0 ≤ tentacles ≤ 64`; maximum result 181000 |
| Invalid input | returns `-1`; no side effects |
| Memory | none allocated or returned; nothing to release; deterministic and pure |
| Manual example | score 90, tentacles 6: 50000 + 22500 + 9000 + 10000 = **91500** cents (915.00 credits) |

### Binding and adapter

- `Native/NativeMethods.cs`: `[DllImport("ijc_native", EntryPoint = "ijc_placement_fee_cents", CallingConvention = Cdecl)] static extern int PlacementFeeCents(int, int)`. `int32_t` ↔ C# `int`; no project setting needed (`LibraryImport` would require `AllowUnsafeBlocks`).
- `Native/FeeAdapter.cs`: validates score and tentacles **before** the call (`Domain InvalidInput`), then calls the native function and translates `DllNotFoundException`/`BadImageFormatException` → `Operational NativeLibraryUnavailable`, `EntryPointNotFoundException` or an unexpected `-1` → `Operational NativeCallFailed`. The call is injectable (`new FeeAdapter(Func<int,int,int>)`); `FeeAdapter.Native` is the real instance.
- **Real use in the workflow:** `ReportWorkflow.Price` calls the adapter for every shortlist row (via `Sequence`), and the fee appears in the console report and the exported CSV. Native calls happen outside the pure queries.

### Platform, compiler, build, loading

| Item | Value |
|---|---|
| Declared platform | Linux x86-64 (glibc), also provided: scripts for Linux arm64, macOS arm64/x64 (`build.sh`) and Windows x64 (`build.cmd`) |
| Compiler used for the shipped binary | gcc 13.3.0 (Ubuntu 13.3.0-6ubuntu2~24.04.1), Ubuntu 24.04.4 LTS, x86_64 |
| Build command | `cc -std=c99 -O2 -Wall -Wextra -Werror -fvisibility=hidden -shared -fPIC -o native/runtimes/linux-x64/libijc_native.so native/ijc_native.c` (`sh native/build.sh` also runs the C self-test `native/test_native.c`) |
| Windows | `cl /std:c11 /O2 /W4 /WX /LD ijc_native.c /Fe:runtimes\win-x64\ijc_native.dll` (`native\build.cmd`) |
| Binary location | `native/runtimes/<rid>/` (`linux-x64`, `win-x64`, `osx-arm64`, …) |
| How it is found | the `.csproj` copies the binary of the current OS/architecture next to the assembly (`<None … Link="libijc_native.so" CopyToOutputDirectory>`), and .NET resolves `ijc_native` → `libijc_native.so` / `ijc_native.dll` / `libijc_native.dylib` in the application directory. The copy propagates to the test project through the project reference. |
| Check | `nm -D native/runtimes/linux-x64/libijc_native.so` lists `ijc_placement_fee_cents` |

**Integration difficulty and resolution:** with `-fvisibility=hidden` (used to avoid exporting helper symbols) the function would not be exported unless it is marked explicitly. The `IJC_EXPORT` macro (`visibility("default")` / `__declspec(dllexport)`) fixes it, and `nm -D` confirms the export. The second pitfall is the library name: `DllImport("ijc_native")` must not contain the `lib` prefix or the extension, and the file must sit next to the assembly, which the `.csproj` copy item guarantees. *(Team: replace or extend this paragraph with the difficulty you meet on your own machine, e.g. building the DLL on Windows.)*

## 9. Integration: expected `reports` demo output

`dotnet run --project src/IntergalacticJobCenter -- reports` (paths use the OS separator):

1. **Data:** `OK  data/applicants_sample.csv: 6 applicants` (streamed) and the same for the JSON file.
2. **Deferred execution:** calls = 0 / 6 / 6 / 12 (see section 5).
3. **Reports:** shortlist for `CreateScore(80)(20)`: Mimi 100 (850.00 credits), Zorp 90 (915.00), Blarg 65 (812.50), Grakk 65 (842.50); for `CreateScore(20)(80)`: Mimi 100 (850.00), Zorp 90 (915.00), Vexa 67 (727.50), Xylo 66 (695.00). Risk overview: Calm 2, Hostile 2, Tense 1 (section 3).
4. **Export:** `OK  4 rows written to output/shortlist_compatibility.csv`.
5. **Failure paths (nothing exported, registry unchanged):** simulated native failure → `FAILED [OPERATIONAL NativeLibraryUnavailable] ijc_native: ...`; minimum score 150 → `[DOMAIN InvalidInput] minScore: ...`; missing file → `[OPERATIONAL FileNotFound]`; malformed JSON → `[OPERATIONAL MalformedContent]`.

## 10. Requirement → file/method map

| Requirement | Where |
|---|---|
| T1 extensions, used in the application | `Queries/ResultSequenceExtensions.cs`, `ApplicantQueryExtensions.cs`, `DeferredScoring.cs`; used by `ReportWorkflow.Price`, `ShortlistReport.Build` |
| T2 reports (Where, Select, OrderBy/ThenBy, GroupBy, Count/Sum/Aggregate) | `Reports/ShortlistReport.cs`, `Reports/RiskReport.cs` |
| T3 currying + partial application | `Queries/ScoreFunctions.cs` (`CreateScore`, `CompatibilityFocused`, `CalmnessFocused`) |
| T4 `yield` + materialisation | `Queries/DeferredScoring.ScoreWith`, `ToImmutableArray` in the reports, `Streaming/LineStreaming.cs` |
| T5 disposal | `Streaming/LineStreaming.cs`, `Reports/ReportExporter.cs`, `Boundary/ApplicantFileLoader.cs` |
| T6 resource note | section 7 |
| T7/T8 native library and binding | `native/*`, `Native/NativeMethods.cs`, `Native/FeeAdapter.cs`, `.csproj` copy items |
| T9 reproducible procedure and tests | sections 1 and 8, `NativeIntegrationTests.cs` |
| Integration and export, Result failures reused | `Reports/ReportWorkflow.cs` (`Run`, `RunAndExport`), `Presentation/ReportsDemo.cs`, `Program.cs` (`reports`) |

## 11. Automated tests (xUnit)

The new files hold 39 `[Fact]` and 7 `[Theory]` methods (67 cases counting each `[InlineData]` row); the tests from Assignments 5–7 remain in the suite, so the cumulative total is far above the required 12. The suite contains well over 8 pure-function/validation tests, 2+ complete workflow tests and 2+ file tests.

| Area | Tests (file) | Expected |
|---|---|---|
| Reports | `Shortlist_*`, `Risk_*` (`ReportAndExtensionTests`) | normal result, empty, no matches, deterministic tie order, grouping and totals, boundaries, invalid parameters |
| Extensions | `Extension_AtLeast…`, `Extension_RankByScore…`, `Extension_Sequence…` | inclusive filter, ordinal tie-break, first failure and non-evaluation of the rest |
| Configured functions | `PartialApplications_…` (6 rows), `CurriedFunction_…`, `ConfiguredFunctions_…` (`ConfiguredAndDeferredTests`) | table in section 4; shortlists differ |
| Deferred execution | `Deferred_*` | 0 calls before iteration; 12 vs 6 calls; snapshot reuse; eager argument checks |
| Resource lifetime | `LineStreaming_*`, `Loader_Releases…`, `Export_*` (`ResourceTests`) | disposed after success, early stop, failure; file handle free; no `.tmp`; existing report preserved |
| Native integration | `NativeCall_ReturnsTheManuallyCalculatedFee` (6 rows, **real native call**), `Adapter_*` (`NativeIntegrationTests`) | 91500, 85000, 81250, 72250, 50000, 181000; fresh `DllNotFoundException` and injected failures become Result failures |
| Complete workflows | `Workflow_NormalPath_*` (3), `Workflow_NativeFailure_*`, `Workflow_InvalidParameter_*` | reports + native fees + export; failure keeps the registry and the previous export file |
| File boundaries | `FileBoundaryTests` (A7), `Loader_MissingCsv…`, `Export_MissingDirectory…` | missing and malformed files stay safe failures; temporary directories are deleted |

**Latest test outcome:** _fill in after running `dotnet test` on a clean extraction (date, passed/failed counts)._

## 12. Team and individual contributions

| Member | Group | Tasks | Files |
|---|---|---|---|
| Dinmukhammet Imanalyuly | [fill in] | T1 extensions, T2 reports | `Queries/ResultSequenceExtensions.cs`, `ApplicantQueryExtensions.cs`, `Reports/ShortlistReport.cs`, `RiskReport.cs`; `ReportAndExtensionTests.cs` |
| Aisha Tynyshtykbay | IT-2505 | T3 currying/partial application, T4 deferred `yield` processing | `Queries/ScoreFunctions.cs`, `DeferredScoring.cs`; `ConfiguredAndDeferredTests.cs` |
| Yeraly Seitzhan | [fill in] | T5 disposal, T6 resource note, integration and export | `Streaming/LineStreaming.cs`, `Reports/ReportExporter.cs`, `ReportWorkflow.cs`, `PricedRow.cs`, loader streaming change, `Presentation/ReportsDemo.cs`, `Program.cs`; `ResourceTests.cs` |
| Bertan Maidabek | [fill in] | T7–T9 native library, binding, adapter, build/loading documentation, README | `native/*`, `Native/*`, `.csproj` copy items; `NativeIntegrationTests.cs`; `README.md` |

Each member must be able to explain and change their own part in the defense (for example: change a band boundary or the ranking tie-break, a weight pair in `CreateScore`, the premium threshold in the C function and its test, or the export format).
