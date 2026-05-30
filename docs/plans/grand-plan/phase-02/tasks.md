# Phase 02 — Tasks

> Work packages and tasks for Solution Scaffolding & Architecture Skeleton.
> ID format: `P02-WP<n>-T<m>`.

---

## WP1 — Solution + 9 projects + build configuration

**Goal:** the solution exists, all projects compile, and the build is
rule-enforced from commit one.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP1-T1 | Create `src/` directory and `OgmaLibrary.sln`. Add all 9 `dotnet new` project stubs in order: `classlib` for Domain, Application, Infrastructure, Reader, Bookshelf3D, Workers; `avalonia` (or Avalonia project template) for App; `xunit` for Tests and Tests.Architecture. | Phase 01 complete | 0.5 d | ADR-0001, ADR-0002, HLD §F |
| P02-WP1-T2 | Write `src/Directory.Build.props` with the properties and analyzer references from README §6. Pin all package versions to the values confirmed in Phase 01 Spike 1 `spikes/RESULTS.md §S1`. | P02-WP1-T1, Spike 1 results | 0.5 d | NFR-PROD-012, L.7, GenerateDocumentationFile |
| P02-WP1-T3 | Write `.editorconfig` at the repo root. Include: C# indentation (4 spaces), CRLF on Windows / LF on macOS (CI normalizes to LF via `.gitattributes`), XAML formatting, Markdown line-length 120. Align with the Development Standards doc. | Phase 00 governance | 0.25 d | dotnet format, NFR-PROD-012 |
| P02-WP1-T4 | Add a `.gitattributes` file: `*.cs text eol=lf`, `*.xaml text eol=lf`, `*.md text eol=lf`, `*.pdf binary`. This ensures the golden-corpus PDF fixtures are not corrupted by Git line-ending normalization. | P02-WP1-T3 | 0.1 d | Golden corpus integrity, CI cross-platform |
| P02-WP1-T5 | Confirm `dotnet build OgmaLibrary.sln --configuration Release` exits 0 with zero warnings on both platforms (CI runners or local). All 9 projects produce empty stub assemblies. | P02-WP1-T1..T3 | 0.25 d | Global DoD §3 |

---

## WP2 — Domain model skeleton

**Goal:** the `Domain` project contains the core entities, value objects, and
repository interfaces that all subsequent phases will build on. Every public
member has an XML doc comment.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP2-T1 | Design and write `BookId` (ULID-based value object, sealed, `IEquatable<BookId>`, `[JsonConverter]`); `ContentHash` (SHA-256 hex, 64-char validated string, `IEquatable`); `Isbn` (10/13 digit, validated, normalized). Each has XML doc comments and a factory method `Create()` that validates its invariants and throws `ArgumentException` (not `InvalidOperationException`) on invalid input. | Phase 00 CON-5, ADR-0005 | 0.5 d | FR-LIB-003, FR-META-001, HLD §F identity model |
| P02-WP2-T2 | Write the `Book` entity: properties `BookId`, `Title`, `Subtitle`, `Description`, `PublishedYear`, `Language`, `CoverImagePath` (relative to sidecar), `Rating` (0–5 nullable), `ReadingStatus` (enum), `Tags` (`IReadOnlyCollection<string>`), `Confidence` (`ConfidenceScore`). No persistence concern; no EF Core attributes in `Domain`. | P02-WP2-T1, CON-5 | 0.5 d | FR-LIB-001, FR-CAT-001/004, HLD §F Book entity |
| P02-WP2-T3 | Write `BookFile` entity: `BookFileId`, `BookId` (FK reference only — not navigation property in Domain), `RelativePath`, `ContentHash`, `SizeBytes`, `MtimeUtc`, `PdfFingerprint` (optional), `AvailabilityStatus` (enum: Available/Unavailable/PasswordProtected). | P02-WP2-T2 | 0.25 d | FR-LIB-002/003/004, HLD §F |
| P02-WP2-T4 | Write `Author` entity and `Shelf` entity. `Author`: `AuthorId`, `DisplayName`, `NormalizedName`. `Shelf`: `ShelfId`, `Name`, `Description`, `CreatedUtc`, `IsSystem` (bool — system shelves like "All Books" cannot be deleted). | P02-WP2-T2 | 0.25 d | FR-CAT-003, HLD §F |
| P02-WP2-T5 | Write `ReadingProgress` entity: `BookFileId` (composite key with `ProfileId` for future multi-user), `LastPageIndex`, `LastScrollOffset`, `LastOpenedUtc`, `TotalPagesRead`, `ReadingTimeSeconds`. | P02-WP2-T2 | 0.25 d | FR-READ-001, HLD §F |
| P02-WP2-T6 | Write `Annotation` entity: `AnnotationId`, `BookFileId`, `PageIndex`, `SelectionStartCharIndex` (nullable), `SelectionEndCharIndex` (nullable), `BoundingRect` (JSON blob, nullable), `HighlightColor` (hex string), `NoteText` (nullable), `CreatedUtc`, `UpdatedUtc`. Durable across abnormal termination (NFR-OGMA-008 — the schema supports atomic upsert). | P02-WP2-T2 | 0.25 d | FR-READ-007/008, NFR-OGMA-008 |
| P02-WP2-T7 | Write `AuditEvent` entity: `AuditEventId`, `EventType` (string, namespaced e.g. "library.book.added"), `EntityId` (string), `ActorId` (string, "local-user" for single-user mode), `TimestampUtc`, `Payload` (JSON string). Append-only contract: no `Delete` method in `IAuditRepository`. | P02-WP2-T2 | 0.25 d | CTRL-OGMA-018, NFR-PROD-013 |
| P02-WP2-T8 | Write repository interfaces in `OgmaLibrary.Domain.Repositories`: `IBookRepository` (CRUD + list); `IShelfRepository`; `IAnnotationRepository`; `IReadingProgressRepository`; `IAuditRepository` (append-only: `AppendAsync` only, no delete/update). All methods take `CancellationToken`. | P02-WP2-T1..T7 | 0.5 d | ADR-0005, HLD §F, NFR-PROD-010 |
| P02-WP2-T9 | Verify: every public type and member in `OgmaLibrary.Domain` has an XML doc comment. Run `dotnet build` and confirm zero `CS1591` warnings with `GenerateDocumentationFile=true` and `<NoWarn>` does NOT suppress CS1591. | P02-WP2-T1..T8 | 0.25 d | L.7, NFR-PROD-012 |

---

## WP3 — DI composition root + hello-world main window

**Goal:** the application starts on both platforms with a minimal main window
whose title is localized (en/fr).

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP3-T1 | Add the `ILocalizationService` interface to `OgmaLibrary.Application`: `string Get(string key)`, `CultureInfo ActiveCulture { get; }`, `void SetCulture(CultureInfo culture)`. Add a `NullLocalizationService` (returns the key unchanged) for stubs before the real implementation in Phase 03. | P02-WP2-T8 | 0.25 d | I18N-STRATEGY §2, FR (i18n), Phase 03 input |
| P02-WP3-T2 | In `OgmaLibrary.App`, write `Program.cs` / `App.axaml.cs` that bootstraps `Microsoft.Extensions.DependencyInjection`, registers all bounded-context services (stub implementations for Phase 02), and launches the Avalonia main window. Use the `UseAvaloniaApp<App>()` pattern. | P02-WP1-T5, P02-WP2-T8 | 0.5 d | ADR-0002, DI composition root |
| P02-WP3-T3 | Write `MainWindow.axaml` and `MainWindowViewModel.cs`. The window title is bound to `ILocalizationService.Get("MainWindow.Title")`. The window body is a placeholder (`<TextBlock Text="Ogma Library — Phase 02 Skeleton" />`). Both en and fr resource files have the `MainWindow.Title` key. | P02-WP3-T1/T2 | 0.25 d | Global DoD §4 (i18n), hello-world |
| P02-WP3-T4 | Run the application on Windows (local or CI): confirm it starts without exception and the window title reads "Ogma Library". Run on macOS: confirm the same. Record both observations in a short verification note. | P02-WP3-T3 | 0.25 d | ADR-0002, cross-platform parity |

---

## WP4 — Architecture tests

**Goal:** three architecture rules are enforced by automated tests that run in
CI from Phase 02 onward.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP4-T1 | Add `NetArchTest.eXtended` (or `NetArchTest` stable) NuGet reference to `OgmaLibrary.Tests.Architecture`. Confirm it resolves on net10.0. | P02-WP1-T5 | 0.1 d | Architecture tests setup |
| P02-WP4-T2 | Write test `Architecture_DomainProject_HasNoOutwardDependencies`: assert that no type in `OgmaLibrary.Domain` has a dependency on `OgmaLibrary.Application`, `OgmaLibrary.Infrastructure`, `OgmaLibrary.Reader`, `OgmaLibrary.Bookshelf3D`, or `OgmaLibrary.Workers`. | P02-WP4-T1 | 0.25 d | HLD §F, SOURCE-SUMMARY §F bounded-context discipline |
| P02-WP4-T3 | Write test `Architecture_OnlyAppBindsImplementations`: assert that no type outside `OgmaLibrary.App` implements more than one interface from `OgmaLibrary.Application` (i.e., no cross-context binding). More precisely: assert that implementation types for `OgmaLibrary.Infrastructure` interfaces only reside in `OgmaLibrary.Infrastructure` (not in `Reader` or `Bookshelf3D`). | P02-WP4-T1 | 0.25 d | HLD §F single composition root |
| P02-WP4-T4 | Write test `Architecture_OnlyInfrastructureUsesHttpClient`: assert no type outside `OgmaLibrary.Infrastructure` references `System.Net.Http.HttpClient` directly. | P02-WP4-T1 | 0.25 d | ADR-0007, CTRL-OGMA egress chokepoint |
| P02-WP4-T5 | Confirm all 3 architecture tests pass in CI (both Windows and macOS runners). Confirm that introducing a deliberate violation (e.g. a `Domain` class importing `Application`) causes the appropriate test to fail. | P02-WP4-T2..T4 | 0.25 d | Global DoD §3, architecture integrity |

---

## WP5 — GitHub Actions CI

**Goal:** every PR to `develop` runs the full check matrix on both platforms.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP5-T1 | Write `.github/workflows/ci.yml` with the `strategy.matrix.os` configuration (see README §6 CI workflow design). | Phase 00 governance (branch strategy, hybrid gate) | 0.5 d | Global DoD §3, cross-platform CI |
| P02-WP5-T2 | Add the `dotnet format --verify-no-changes` step first so formatting failures give a clear message before the build step runs. | P02-WP5-T1 | 0.1 d | dotnet format gate |
| P02-WP5-T3 | Add the hybrid validation gate step (`python -m engine validate Ogma-Library`). Use `ubuntu-latest` for this step if the engine runs on Linux; otherwise add it to the Windows runner step. | P02-WP5-T1, Phase 00 hybrid gate confirmed | 0.25 d | Global DoD §6, SOURCE-SUMMARY §A |
| P02-WP5-T4 | Add GitHub branch protection rules for `main` and `develop` via the repo settings (document the settings in `docs/governance/BRANCH-STRATEGY.md`): require CI passing on both matrix legs before merge; require at least one code review approval. | P02-WP5-T1, Phase 00 governance | 0.25 d | Governance, NFR-PROD-012 |
| P02-WP5-T5 | Validate: open a test PR that intentionally fails `dotnet format` → CI reports failure. Open a test PR that passes all steps → CI reports success. Both tests on both matrix legs. | P02-WP5-T1..T4 | 0.25 d | CI validation |

---

## WP6 — Golden-corpus test harness

**Goal:** fixture loading and verification are available to all integration tests
from Phase 02 onward.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP6-T1 | Copy the cleared PDF fixtures (from Phase 00 CON-9 manifest) into `tests/golden-corpus/fixtures/`. Compute SHA-256 for each and write `MANIFEST.sha256` (one `<hash>  <filename>` line per file). | Phase 00 CON-9 cleared, fixtures available | 0.5 d | SOURCE-SUMMARY §J golden corpus |
| P02-WP6-T2 | Write `ManifestVerifier.cs` in a shared test helper project (`OgmaLibrary.Tests.Helpers`): reads `MANIFEST.sha256`; for each entry, asserts the file exists and its SHA-256 matches. Run as an `[OneTimeSetUp]` (NUnit) or `IClassFixture` (xUnit) in all integration test classes. | P02-WP6-T1 | 0.25 d | SOURCE-SUMMARY §J oracle integrity |
| P02-WP6-T3 | Write `SyntheticCorpusGenerator.cs`: given an integer seed and a count N, generates N `SyntheticBook` records with deterministic title, author, ISBN-13 (mod-11 valid), tag list, and file-name patterns. Used for performance tests in later phases. Confirm two runs with seed 42 and N=500 produce identical records (determinism test). | P02-WP6-T2 | 0.5 d | SOURCE-SUMMARY §J perf corpora, NFR-OGMA perf tests |
| P02-WP6-T4 | Write a `GoldenCorpusTests.cs` integration test: for each fixture in `MANIFEST.sha256`, load the file as a `FileStream` and assert `ManifestVerifier` passes. This test is the baseline for all future per-fixture tests. | P02-WP6-T1..T3 | 0.25 d | SOURCE-SUMMARY §J |
| P02-WP6-T5 | Confirm the golden-corpus tests pass in CI (both runners). The PDF fixture files are checked into the repo under `tests/golden-corpus/fixtures/` (they are small, cleared, public-domain files; confirm total size < 10 MB to keep CI fast). | P02-WP6-T1..T4, CI green | 0.25 d | CI, golden corpus |

---

## WP7 — i18n analyzer + pseudolocale runner

**Goal:** no hard-coded UI string can enter the codebase; the hello-world
window proves the i18n pipeline works.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP7-T1 | Create the initial `en.resx` and `fr.resx` resource files in `OgmaLibrary.App/Localization/`. Add `MainWindow.Title` in both. Add a `Strings.Designer.cs` (or use `ILocalizationService.Get("key")` pattern — TBD in Phase 03; for Phase 02, use the resx strongly-typed accessor as a placeholder). | P02-WP3-T3 | 0.25 d | I18N-STRATEGY §2/3, Global DoD §4 |
| P02-WP7-T2 | Write the Roslyn DiagnosticAnalyzer `HardCodedStringAnalyzer.cs` (diagnostic ID `OGMA0001`). Detect string literals assigned to Avalonia UI properties by checking: (a) the enclosing type is a `Control` subtype, or (b) the assignment target is a property named `Text`, `Header`, `Title`, `Content`, or `Label` on an Avalonia type. Fire `OGMA0001` with message "Hard-coded UI string: use ILocalizationService or resource keys." | P02-WP1-T2 (Directory.Build.props) | 1 d | I18N-STRATEGY §2 (no hard-coded strings), Global DoD §4 |
| P02-WP7-T3 | Write a unit test for the analyzer: use Roslyn's `CSharpAnalyzerVerifier` to assert that a class with `myButton.Content = "Submit";` raises `OGMA0001`, and that `myButton.Content = _loc.Get("Button.Submit");` does not. | P02-WP7-T2 | 0.5 d | Analyzer correctness |
| P02-WP7-T4 | Register the analyzer as a build-time error in `Directory.Build.props` (`<Analyzer Include="..." />` or via a NuGet analyzer project). Confirm `dotnet build` fails with `OGMA0001` on a test violation and succeeds on clean code. | P02-WP7-T2/T3 | 0.25 d | Build gate enforcement |
| P02-WP7-T5 | Write the pseudolocale test in `OgmaLibrary.Tests`: use Avalonia's headless test infrastructure (`AppBuilder.Configure<App>().UseHeadless()`). Set `CultureInfo.CurrentUICulture = new CultureInfo("fr")` (Phase 02 uses fr, not a custom pseudo-locale, since the full pseudo-locale needs more resx entries; upgrade to qps-ploc in Phase 03). Open the main window; assert the `Title` property equals "Bibliothèque Ogma". | P02-WP7-T1, P02-WP3-T3 | 0.5 d | I18N-STRATEGY §5, Global DoD §4 |
| P02-WP7-T6 | Confirm the analyzer test and pseudolocale test both pass in CI (both runners). | P02-WP7-T3..T5 | 0.1 d | CI, Global DoD §4 |

---

## WP8 — Performance-budget instrumentation

**Goal:** services can emit wall-clock measurements via a DI-injected interface
so CI benchmark tests can assert budget compliance without `Stopwatch` coupling.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP8-T1 | Write `IBenchmarkContext` in `OgmaLibrary.Application.Diagnostics` (see README §6 for the interface definition). Write `IBenchmarkScope : IDisposable` (the returned handle from `Measure()`). | P02-WP2-T8 | 0.25 d | NFR-OGMA instrumentation, Phase 20 perf tests |
| P02-WP8-T2 | Write `StopwatchBenchmarkContext : IBenchmarkContext` in `OgmaLibrary.Infrastructure.Diagnostics`. Uses `System.Diagnostics.Stopwatch` internally; stores the last duration per operation name in a `ConcurrentDictionary<string, TimeSpan>`. Thread-safe. | P02-WP8-T1 | 0.25 d | IBenchmarkContext implementation |
| P02-WP8-T3 | Write `NullBenchmarkContext : IBenchmarkContext` in `OgmaLibrary.Application.Diagnostics` — a no-op implementation for production (or for tests that don't care about timing). This is the default DI registration; `StopwatchBenchmarkContext` is registered only in test or benchmark configurations. | P02-WP8-T1 | 0.1 d | DI, test isolation |
| P02-WP8-T4 | Measure the hello-world app cold-start time on both reference machines using `StopwatchBenchmarkContext` (inject into `App.OnFrameworkInitializationCompleted`; log "app.startup" duration to console). Record in `docs/performance/BenchmarkBaseline.md` with machine spec, .NET version, and Avalonia version. | P02-WP3-T4, P02-WP8-T1 | 0.25 d | NFR-OGMA-001 baseline, BenchmarkBaseline.md |

---

## WP9 — Open-source documentation baseline

**Goal:** the repo is ready for a public audience from day one.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP9-T1 | Run `/init` to create or update `CLAUDE.md` at the repo root. The file must include: project summary (one paragraph), how to build and test, the solution structure, the governance rules (Conventional Commits, CIA checklist), and a pointer to `docs/developer-guide/`. | P02-WP1-T5, P02-WP5 done | 0.25 d | L.7, open-source readiness |
| P02-WP9-T2 | Write `docs/developer-guide/README.md`. Sections: (1) Prerequisites (.NET 10 SDK, macOS Xcode CLT), (2) Clone & build, (3) Run (`dotnet run --project src/OgmaLibrary.App`), (4) Test (`dotnet test`), (5) Architecture overview (link to HLD and ADRs), (6) Solution structure (table of all 9 projects and their roles), (7) Contributing (link to CONTRIBUTING.md and CIA workflow). | P02-WP9-T1 | 0.5 d | L.7, open-source readiness |
| P02-WP9-T3 | Add the Phase 02 entry to `CHANGELOG.md` using `documentation-generation:changelog-automation`. Format: `## [0.2.0] - 2026-<date>` (semver 0.x for pre-release phases); list the 9 work packages as bullet points under "Added". | P02-WP9-T1/T2 | 0.1 d | L.7, changelog |

---

## WP10 — Code review + DoD + merge

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P02-WP10-T1 | Request a code review via `superpowers:requesting-code-review` + `/code-review --effort high` on the Phase 02 PR. Review focus: (a) domain model completeness and correctness against HLD §F; (b) architecture test coverage (are the 3 rules sufficient?); (c) i18n analyzer false-positive risk; (d) CI matrix completeness; (e) XML doc coverage. | All WP1..WP9 complete | 0.5 d | Global DoD §8 |
| P02-WP10-T2 | Resolve all code review findings. Re-run `dotnet test` and CI. | P02-WP10-T1 | 0.5 d | Global DoD §8 |
| P02-WP10-T3 | Run the Phase 02 DoD checklist (all items in README §9). File any open items as GitHub issues with `phase-02` label. Merge the feature branch to `develop`. | P02-WP10-T2 | 0.25 d | Phase 02 DoD |
