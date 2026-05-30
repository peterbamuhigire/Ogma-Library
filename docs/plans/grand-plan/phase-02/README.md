# Phase 02 — Solution Scaffolding & Architecture Skeleton

One sentence: Build the permanent structural foundation — the 9-project
solution, build configuration, DI composition root, domain model skeleton,
architecture tests, CI matrix, golden-corpus harness, and open-source
documentation baseline — so every subsequent phase builds on a tested,
rule-enforced substrate.

---

## 1. Status & metadata

| Field | Value |
| --- | --- |
| **Status** | Not started |
| **Tier** | MVP (all build infrastructure gates the MVP) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD Phase 0 (infrastructure) |
| **Platforms** | CI runs on **both `windows-latest` and `macos-latest` GitHub Actions runners**; all checks must be green on both before any PR merges to `develop` |
| **Depends on** | Phase 00 (governance, license, ADRs), Phase 01 (dependency matrix confirmed, winning PDFium wrapper known) |

---

## 2. Objectives

1. The 9-project Avalonia/.NET 10 solution (`OgmaLibrary.sln`) exists with all
   projects in the correct dependency order; `dotnet build` succeeds with
   warnings-as-errors on both platforms.
2. `Directory.Build.props` enforces: `net10.0` TFM, `Nullable=enable`,
   `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`, and the
   approved Roslyn analyzer set (SonarAnalyzer.CSharp, StyleCop.Analyzers,
   Roslynator.Analyzers) across all projects.
3. `.editorconfig` encodes the C#/XAML style rules that match the Development
   Standards doc and the Phase 00 governance.
4. The DI composition root (`App` project) wires all bounded-context interfaces
   to their implementations via `Microsoft.Extensions.DependencyInjection`; the
   application starts (a "hello world" main window) on both platforms.
5. The domain model skeleton (`Domain` project) defines the core bounded-context
   entities (`Book`, `BookFile`, `Author`, `Shelf`, `ReadingProgress`,
   `Annotation`, `AuditEvent`) with XML doc comments and no outward dependencies.
6. Architecture tests (`Tests.Architecture` project) enforce: the `Domain`
   project has no outward dependencies; only the `App` project binds
   implementations; and a single egress chokepoint test confirms that no project
   other than `Infrastructure` directly references HTTP clients.
7. CI is configured on GitHub Actions: Windows and macOS runners; `dotnet format
   --verify-no-changes`, `dotnet build`, `dotnet test`, and architecture tests
   all pass in the CI matrix.
8. The golden-corpus test harness is instantiated: fixture loading helpers,
   `MANIFEST.sha256` verification, and the seed-deterministic synthetic
   corpus generator are usable from `Tests.Integration`.
9. The i18n analyzer (no hard-coded UI strings) and the pseudolocale runner
   are wired into the build and pass with the "hello world" main window.
10. Performance-budget instrumentation hooks are in place: `IBenchmarkContext`
    interface, `Stopwatch`-based wall-clock measurements in the domain service
    layer, and a `BenchmarkBaseline.md` record for Phase 02.
11. Open-source documentation baseline: `CLAUDE.md` (up to date), a developer
    guide (`docs/developer-guide/README.md`), and the first ADR for any Phase 02
    architectural decisions are committed.

---

## 3. Scope

### In scope

- Creating the `OgmaLibrary.sln` and 9 projects under `src/`:
  - `OgmaLibrary.App` — Avalonia composition root, main window, DI wiring.
  - `OgmaLibrary.Domain` — entities, value objects, domain events, repository
    interfaces; zero outward dependencies.
  - `OgmaLibrary.Application` — use-case interfaces, DTOs, application services
    (thin); depends on `Domain` only.
  - `OgmaLibrary.Infrastructure` — SQLite (EF Core), file system adapters,
    PDF adapters, HTTP clients (AI providers), sidecar management; depends on
    `Domain` and `Application`.
  - `OgmaLibrary.Reader` — PDFium-backed reader bounded context; depends on
    `Domain` and `Application`.
  - `OgmaLibrary.Bookshelf3D` — WebView-hosted Three.js bookshelf context;
    depends on `Domain` and `Application`.
  - `OgmaLibrary.Workers` — background jobs (scanning, thumbnails, enrichment);
    depends on `Application` and `Infrastructure`.
  - `OgmaLibrary.Tests` — unit and integration tests; references all other
    projects.
  - `OgmaLibrary.Tests.Architecture` — `NetArchTest.eXtended` (or `NetArchTest`)
    architecture tests; references all production projects.
- `Directory.Build.props` with the enforced properties listed in Objective 2.
- `.editorconfig` (C#, XAML, Markdown rules).
- DI composition root in `App` (`Program.cs` / `App.axaml.cs`): service
  registration for all bounded contexts; the main window opens on both platforms.
- Domain model skeleton: entity classes with XML doc comments, value objects
  (`BookId`, `ContentHash`, `Isbn`), repository interfaces
  (`IBookRepository`, `IShelfRepository`, etc.).
- Architecture tests (see Objective 6).
- GitHub Actions CI workflows:
  - `ci.yml`: `windows-latest` + `macos-latest` matrix; steps: checkout,
    setup .NET 10, `dotnet format --verify-no-changes`, `dotnet build`, `dotnet
    test` (includes architecture tests).
  - The hybrid validation gate step: `python -m engine validate Ogma-Library`
    (runs on `ubuntu-latest` if the gate engine supports it, else
    `windows-latest`).
- Golden-corpus test harness: `tests/golden-corpus/` directory, fixture helpers,
  `ManifestVerifier` (SHA-256 checks), `SyntheticCorpusGenerator` (seeded).
- i18n analyzer: a Roslyn source generator or a build-time MSBuild task that
  scans Avalonia XAML and C# view-models for string literals that reach the UI;
  fails the build with a custom diagnostic (`OGMA0001`) if any are found.
- Pseudolocale runner: a test that starts the Avalonia app with
  `CultureInfo.CurrentUICulture = new CultureInfo("qps-ploc")` and asserts no
  UI element throws a `MissingManifestResourceException`.
- Performance-budget instrumentation: `IBenchmarkContext` interface in
  `Application`; `StopwatchBenchmarkContext` in `Infrastructure`; DI-registered
  so production services can emit wall-clock measurements without referencing
  `System.Diagnostics.Stopwatch` directly.
- Open-source documentation baseline (per SOURCE-SUMMARY §L.7):
  - `/init` to create or update `CLAUDE.md`.
  - `docs/developer-guide/README.md`: building, running, testing, contributing,
    solution structure, dependency graph.
  - First changelog entry via `documentation-generation:changelog-automation`.

### Explicitly out of scope

- Any actual SQLite schema or EF Core migrations (Phase 04).
- Ingestion pipeline, scanning, thumbnails (Phase 05).
- UI design, icons, color tokens, i18n strings (Phase 03).
- PDFium render integration (Phase 08).
- Any FTS5, search, or AI implementation (Phases 10-13).
- The 3D bookshelf Three.js implementation (Phase 14).
- LAN transport (Phase 16).

---

## 4. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| ADR-0001 | MVP | .NET 10 LTS used as TFM in all projects | `Directory.Build.props`; `dotnet build` on CI (both platforms) |
| ADR-0002 | MVP | Avalonia shell; solution compiles and app starts | Main window renders on Windows + macOS (CI + `/run` verification) |
| ADR-0005 | MVP | SQLite catalogue + sidecar (interfaces defined) | `IBookRepository`, `ICatalogueContext` interfaces in `Domain`/`Application` |
| HLD §F | MVP | 9-project architecture; dependency direction | Architecture tests: `Architecture_DomainProject_HasNoOutwardDependencies`, `Architecture_OnlyAppBindsImplementations` |
| NFR-PROD-012 | MVP | Signed builds + reversible migrations (build baseline) | `dotnet build` warnings-as-errors; `dotnet format` clean; CI green |
| L.7 | MVP | Open-source: XML docs, GenerateDocumentationFile=true | `Directory.Build.props`; `docfx` or `dotnet xmldoc` confirms no public member lacks an XML doc comment |
| L.7 | MVP | CLAUDE.md current, developer guide exists | Files committed; `/init` run; developer guide covers build + test |
| Global DoD §3 | MVP | dotnet format, dotnet build, dotnet test, arch tests pass | CI matrix green on Windows + macOS |
| Global DoD §4 | MVP | No hard-coded UI strings; pseudolocale check | i18n analyzer (OGMA0001) fires on a test string, not on the hello-world window |
| SOURCE-SUMMARY §J | MVP | Golden-corpus harness instantiated | `ManifestVerifier` tests pass; `SyntheticCorpusGenerator` is deterministic from seed |
| NFR-OGMA (all) | MVP | Performance-budget instrumentation hooks in place | `IBenchmarkContext` interface defined; `BenchmarkBaseline.md` produced |

---

## 5. Dependencies

### Depends on

- Phase 00: governance files (LICENSE, CONTRIBUTING.md, ADRs ratified), repo
  governance (branch strategy, commit hook), hybrid gate operational.
- Phase 01: dependency matrix results (Spike 1) → `Directory.Build.props`
  package version pins; winning PDFium wrapper (Spike 2 ADR-0004 amendment)
  → `OgmaLibrary.Reader` project NuGet reference.

### Unblocks

- Phase 03: design system, icons, and i18n are built on top of the solution
  skeleton.
- Phases 04-23: all depend on the clean, rule-enforced project structure.
- The open-source documentation baseline is required before any phase can
  be merged to `main` (CONTRIBUTING.md says so).

---

## 6. Architecture & approach

### Project structure

```
OgmaLibrary.sln
src/
  OgmaLibrary.App/                   # Avalonia entry point; DI composition root
  OgmaLibrary.Domain/                # Entities, value objects, repo interfaces; no outward deps
  OgmaLibrary.Application/           # Use-case interfaces, DTOs; depends on Domain only
  OgmaLibrary.Infrastructure/        # SQLite, FS, PDF adapters, HTTP, AI; depends on Domain+App
  OgmaLibrary.Reader/                # PDFium reader context; depends on Domain+App
  OgmaLibrary.Bookshelf3D/           # WebView 3D context; depends on Domain+App
  OgmaLibrary.Workers/               # Background workers; depends on App+Infra
tests/
  OgmaLibrary.Tests/                 # Unit + integration tests
  OgmaLibrary.Tests.Architecture/    # NetArchTest architecture tests
  golden-corpus/
    fixtures/                        # Cleared PDF fixtures + MANIFEST.sha256
    synthetic/                       # Seed-deterministic corpus generator
spikes/                              # Phase 01 spike code (excluded from solution)
docs/
  adrs/                              # ADR-0001..0010+
  developer-guide/
  governance/
  plans/grand-plan/
```

### Dependency graph (enforced by architecture tests)

```
App ──depends on──> Domain, Application, Infrastructure, Reader, Bookshelf3D, Workers
Application ──────> Domain
Infrastructure ───> Domain, Application
Reader ───────────> Domain, Application
Bookshelf3D ──────> Domain, Application
Workers ──────────> Application, Infrastructure
Domain ───────────> (nothing — strict isolation)
```

Rule enforced: `Architecture_DomainProject_HasNoOutwardDependencies` using
`NetArchTest`: `Types().That().ResideInAssembly("OgmaLibrary.Domain")
.Should().NotHaveDependencyOnAny("OgmaLibrary.Application",
"OgmaLibrary.Infrastructure", "OgmaLibrary.Reader", ...)`.

### Single egress chokepoint (architecture test)

`Architecture_OnlyInfrastructureUsesHttpClient`: `Types().That()
.HaveNameEndingWith("HttpClient").Or().HaveName("HttpClient")
.Should().ResideInNamespaceContaining("OgmaLibrary.Infrastructure")`.

This test enforces that no bounded context makes direct HTTP calls; all network
access must go through `Infrastructure` adapters, which implement the
`Application`-layer interfaces. This is the architectural embodiment of
CTRL-OGMA and ADR-0007 (single egress chokepoint).

### `Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SonarAnalyzer.CSharp" Version="[pinned]" PrivateAssets="all" />
    <PackageReference Include="StyleCop.Analyzers" Version="[pinned]" PrivateAssets="all" />
    <PackageReference Include="Roslynator.Analyzers" Version="[pinned]" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Package versions are pinned using the values confirmed in Phase 01 Spike 1.

### Domain model skeleton

Key entities (all in `OgmaLibrary.Domain.Entities`; all sealed unless designed
for inheritance; all with XML doc comments on every public member):

- `Book` — the abstract bibliographic Work; owns `BookFiles`, `Authors`,
  `ShelfMemberships`, `Tags`, `AiCategories`.
- `BookFile` — a physical PDF with `RelativePath`, `ContentHash` (SHA-256),
  `SizeBytes`, `MTime`, `PdfFingerprint`, `AvailabilityStatus`.
- `Author` — name, normalized form, linked to `Books` (many-to-many).
- `Shelf` — virtual shelf; a `Book` can be in multiple shelves.
- `ReadingProgress` — `LastPageIndex`, `LastScrollOffset`, `LastOpenedUtc`.
- `Annotation` — `PageIndex`, `SelectionStart`, `SelectionEnd`,
  `HighlightColor`, `NoteText`; linked to `BookFile`.
- `AuditEvent` — `EventType`, `EntityId`, `ActorId`, `TimestampUtc`, `Payload`
  (JSON); append-only; no delete.

Value objects: `BookId` (ULID), `ContentHash` (SHA-256 hex string, validated),
`Isbn` (validated 10/13 digit, normalized), `ConfidenceScore` (0.0–1.0 range).

Repository interfaces (in `OgmaLibrary.Domain.Repositories`):
`IBookRepository`, `IShelfRepository`, `IAnnotationRepository`,
`IReadingProgressRepository`, `IAuditRepository`.

### CI workflow design

File: `.github/workflows/ci.yml`

```yaml
strategy:
  matrix:
    os: [windows-latest, macos-latest]
steps:
  - uses: actions/checkout@v4
  - uses: actions/setup-dotnet@v4
    with: { dotnet-version: '10.x' }
  - run: dotnet format --verify-no-changes
  - run: dotnet build OgmaLibrary.sln --configuration Release
  - run: dotnet test OgmaLibrary.sln --configuration Release --no-build
  - run: python -m engine validate Ogma-Library   # hybrid validation gate
```

The `dotnet test` step runs both `OgmaLibrary.Tests` (golden-corpus harness
fixtures + i18n analyzer checks + pseudolocale runner) and
`OgmaLibrary.Tests.Architecture` (dependency-direction + egress-chokepoint).

### i18n analyzer (OGMA0001)

A Roslyn DiagnosticAnalyzer registered in `OgmaLibrary.App` and activated via
`Directory.Build.props`. It fires `OGMA0001` ("Hard-coded string in UI context")
on any `string` literal:
- Assigned to an Avalonia property of type `string` in XAML code-behind.
- Passed as an argument to any method whose parameter is named `header`,
  `text`, `label`, `content`, or `title` in a type that derives from
  `Control` or `Window`.

The analyzer is validated in Phase 02 by:
1. A test that introduces a deliberately hard-coded string and asserts
   `OGMA0001` fires.
2. Confirming that the "hello world" main window (which uses `ILocalizationService`
   to look up its title) does not trigger `OGMA0001`.

### Pseudolocale runner

`OgmaLibrary.Tests` includes a test that:
1. Instantiates the Avalonia `Application` in headless mode (Avalonia headless
   testing API).
2. Sets `CultureInfo.CurrentUICulture` to a pseudo-locale (`qps-ploc` or a
   custom `AccumulatedLocale` that wraps every string in `[» ... «]`).
3. Opens the main window and asserts no `MissingManifestResourceException` is
   thrown and all visible text controls have non-empty content.

### Performance-budget instrumentation

`IBenchmarkContext` (in `OgmaLibrary.Application`):

```csharp
/// <summary>Abstracts wall-clock timing for performance budget enforcement.</summary>
public interface IBenchmarkContext
{
    /// <summary>Starts a named timing scope. Dispose the returned handle to stop.</summary>
    IDisposable Measure(string operationName);

    /// <summary>Returns the last recorded duration for the named operation.</summary>
    TimeSpan GetLastDuration(string operationName);
}
```

`StopwatchBenchmarkContext` in `Infrastructure` wraps `System.Diagnostics.
Stopwatch`. Domain services accept `IBenchmarkContext` via constructor injection
so CI benchmark tests can inject a recording context and assert that operations
complete within NFR-OGMA budget values.

### Cross-platform approach (Windows + macOS)

- All CI checks run on both `windows-latest` and `macos-latest` runners.
  A PR that is green on Windows but red on macOS is **not mergeable**.
- The Avalonia app uses `net10.0` with no `#if WINDOWS` / `#if MACOS`
  directives in the domain or application layer; platform differences are
  confined to `Infrastructure` adapters and the `App` composition root.
- The native PDFium library (from the winning wrapper chosen in Phase 01
  Spike 2) must have both `win-x64` and `osx-arm64` / `osx-x64` native assets
  referenced in `OgmaLibrary.Reader.csproj`.
- The WebView control in `OgmaLibrary.Bookshelf3D` is not activated in Phase 02
  (stub implementation only); the platform-specific WebView hosting is
  implemented in Phase 14.

---

## 7. Work breakdown (summary)

| WP | Work package | Est. |
| --- | --- | --- |
| WP1 | Create solution + 9 projects + Directory.Build.props + .editorconfig | 2 d |
| WP2 | Domain model skeleton (entities, value objects, repository interfaces) | 2 d |
| WP3 | DI composition root + hello-world main window (Windows + macOS) | 1 d |
| WP4 | Architecture tests (NetArchTest; 3 core rules) | 1 d |
| WP5 | GitHub Actions CI matrix (Windows + macOS) + hybrid gate | 1 d |
| WP6 | Golden-corpus test harness (ManifestVerifier + SyntheticCorpusGenerator) | 2 d |
| WP7 | i18n analyzer (OGMA0001) + pseudolocale runner | 2 d |
| WP8 | Performance-budget instrumentation (IBenchmarkContext) | 1 d |
| WP9 | Open-source documentation baseline (CLAUDE.md, developer guide, changelog) | 1 d |
| WP10 | Code review + DoD checklist + merge | 2 d |

Detail in `tasks.md`.

---

## 8. Cross-cutting checklist

- [x] **Colorful icons + manifest:** Phase 02 has no shipped UI surface beyond
  the hello-world main window. `icons.md` = stub. No icon procurement yet (that
  is Phase 03).
- [x] **i18n (en/fr):** The hello-world main window's title string is
  externalized via `ILocalizationService`. The `en` resource file has the key
  `MainWindow.Title = "Ogma Library"`. The `fr` resource file has
  `MainWindow.Title = "Bibliothèque Ogma"`. The i18n analyzer confirms no
  hard-coded string escapes. The pseudolocale runner passes.
- [x] **Accessibility:** No shipped interactive controls beyond the main window
  shell. The Avalonia `AutomationPeer` base is confirmed available (Avalonia's
  built-in accessibility infrastructure). Phase 03 adds the full accessibility
  scaffold.
- [x] **Privacy/egress:** No off-device calls in Phase 02. The architecture
  test `Architecture_OnlyInfrastructureUsesHttpClient` enforces the egress
  chokepoint from day one.
- [x] **Reversibility:** No user data operations in Phase 02. The `AuditEvent`
  entity is defined as append-only from the start (enforced by domain design).
- [x] **Performance budgets:** `IBenchmarkContext` interface is wired; a
  `BenchmarkBaseline.md` records the Phase 02 build time and cold-start time
  of the hello-world app on both reference machines (as a trend baseline for
  NFR-OGMA-001).
- [x] **Bounded-context tests:** The 3 architecture tests (domain isolation,
  App-only binding, single egress chokepoint) are green on both CI runners.
- [x] **Documentation:** `GenerateDocumentationFile=true`; all public types in
  `Domain` have XML doc comments; developer guide covers build + test; CLAUDE.md
  is up to date.

---

## 9. Definition of Done

- [ ] `OgmaLibrary.sln` contains all 9 projects with correct dependency
  references; `dotnet build OgmaLibrary.sln --configuration Release` exits 0
  with no warnings on both CI runners.
- [ ] `Directory.Build.props` enforces all properties in Objective 2; verified
  by attempting to add a warning-generating code snippet and confirming it fails.
- [ ] `.editorconfig` is in the repo root; `dotnet format --verify-no-changes`
  exits 0 on both CI runners.
- [ ] The application starts on Windows and macOS and displays the hello-world
  main window (verified via `/run` on both platforms).
- [ ] All `Domain` entities have XML doc comments on every public member;
  `dotnet build` with `GenerateDocumentationFile=true` produces no
  `CS1591` warning.
- [ ] Architecture tests pass: `Architecture_DomainProject_HasNoOutwardDependencies`,
  `Architecture_OnlyAppBindsImplementations`,
  `Architecture_OnlyInfrastructureUsesHttpClient`.
- [ ] CI matrix (`windows-latest` + `macos-latest`) is green: format, build,
  test, architecture tests all pass.
- [ ] Hybrid validation gate (`python -m engine validate Ogma-Library`) exits 0
  in CI.
- [ ] Golden-corpus `MANIFEST.sha256` is verified by `ManifestVerifier`; the
  `SyntheticCorpusGenerator` produces identical output for seed 42 on two
  consecutive runs.
- [ ] i18n analyzer fires `OGMA0001` on a deliberate test violation; the
  hello-world window does not trigger it; pseudolocale runner passes.
- [ ] `CLAUDE.md` is current; developer guide covers building, testing, and
  contributing; `CHANGELOG.md` has a Phase 02 entry.
- [ ] No open R1 or R2 defect.
- [ ] Code review (`/code-review --effort high`) completed; all findings
  resolved.

---

## 10. Skills to use

See `skills.md` for full invocation guidance. Summary:

- `architecture:system-architecture-design` — validate the 9-project layout
  against HLD §F before creating it.
- `architecture:validation-contract` — design the repository interfaces and the
  `IBenchmarkContext` contract.
- `sdlc-meta:sdlc-design` — design the CI workflow and the golden-corpus
  harness.
- `cicd-pipeline-design` / `cicd-pipelines` — GitHub Actions workflow authoring.
- `language-standards` (C# / .NET) — `Directory.Build.props`, `.editorconfig`,
  analyzer configuration.
- `documentation-generation:docs-architect` — developer guide and CLAUDE.md.
- `sdlc-meta:e2e-testing` — golden-corpus harness fixture framework.
- `superpowers:test-driven-development` — architecture tests and i18n analyzer
  tests written before the implementation.

---

## 11. Deliverables

| Artifact | Location |
| --- | --- |
| `OgmaLibrary.sln` | repo root `src/` |
| 9 project files (`*.csproj`) | `src/<Project>/` |
| `Directory.Build.props` | repo root `src/` |
| `.editorconfig` | repo root |
| Domain model skeleton (entities + value objects + repo interfaces) | `src/OgmaLibrary.Domain/` |
| DI composition root | `src/OgmaLibrary.App/Program.cs`, `App.axaml.cs` |
| Architecture tests | `tests/OgmaLibrary.Tests.Architecture/` |
| CI workflow | `.github/workflows/ci.yml` |
| Golden-corpus harness | `tests/golden-corpus/` |
| i18n analyzer (OGMA0001) | `src/OgmaLibrary.App/Analyzers/` or dedicated `OgmaLibrary.Analyzers` project |
| `IBenchmarkContext` + `StopwatchBenchmarkContext` | `src/OgmaLibrary.Application/` + `src/OgmaLibrary.Infrastructure/` |
| `BenchmarkBaseline.md` | `docs/performance/` |
| `CLAUDE.md` (updated) | repo root |
| Developer guide | `docs/developer-guide/README.md` |
| `CHANGELOG.md` (Phase 02 entry) | repo root |

---

## 12. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Avalonia 11.x not yet fully .NET 10 stable at Phase 02 time | R5 | Phase 01 Spike 1 confirmed this; if Avalonia is only available on net8.0, apply the ADR-0001 bridge policy (compile `App`/`Bookshelf3D`/`Reader` with `<TargetFramework>net8.0</TargetFramework>` under the bridge exception); file a tracking issue to move to net10.0 when Avalonia supports it. |
| Roslyn analyzer version conflicts between SonarAnalyzer, StyleCop, Roslynator | R5 | Pin all three analyzer packages to specific versions confirmed in Spike 1; set each to `PrivateAssets="all"` so they don't pollute transitive references. |
| i18n analyzer false positives on non-UI string literals | R5 | Narrow the analyzer's detection scope: only fire on `Control.Text`, `Window.Title`, `MenuItem.Header`, `Button.Content`, and similar Avalonia UI properties (not all string literals in the codebase). Add an `[SuppressMessage("Ogma", "OGMA0001")]` escape hatch for justified exceptions. |
| CI macOS runner missing native PDFium binary | R5 | Phase 02 does not activate the PDFium reader; `OgmaLibrary.Reader` compiles with a stub `IPdfRenderer` that throws `NotImplementedException`. No native binary is needed until Phase 08. |
| Golden-corpus fixture license (CON-9) not fully resolved at Phase 02 start | R5 | Use only the fixtures confirmed cleared in Phase 00 (CON-9); Phase 02 replaces any placeholder with a confirmed fixture before the harness is committed. |

---

## 13. Owner asks

1. **Analyzer rule severity:** The Development Standards doc specifies which
   Roslyn diagnostic codes are `Error` vs `Warning` vs `Info`. If the standards
   doc is not specific on StyleCop rule severity, Peter must approve the proposed
   `.editorconfig` severity settings before Phase 02 closes (to avoid
   re-configuring them in Phase 03 under time pressure).
2. **DocFX vs alternative:** For XML-doc publishing (the future open-source
   developer reference), confirm whether DocFX or an alternative (e.g. Sandcastle
   Help File Builder, or mdBook for Markdown-only) is preferred. Phase 02 sets
   `GenerateDocumentationFile=true`; the publishing toolchain is not selected
   until this answer is received (can be Phase 03 or later as long as the XML
   is generated).
3. **Sign-off on the domain model skeleton:** The entity design (especially
   `Work` / `Edition` / `BookFile` cardinality — CON-5 from Phase 00) must be
   owner-reviewed before Phase 04 adds the database schema. Phase 02 produces
   the code; Phase 00 decisions.md §CON-5 must align.

---

## 14. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand Plan authoring | v1.0 baseline created |
