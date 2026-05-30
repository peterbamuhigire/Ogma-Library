# Phase 02 — Test Plan

> Phase 02 instantiates the test infrastructure that all subsequent phases
> build on. This document covers the test layers active in Phase 02, the
> fixtures, the deterministic oracles, and the first slice of golden-corpus
> and performance baseline coverage.

---

## Applicable test layers

| Layer | Active in Phase 02? | Notes |
| --- | --- | --- |
| Domain | Yes | Unit tests for value objects and entity invariants |
| Infrastructure | Stub | No real infrastructure implementations yet; stub DI registrations only |
| PDF | Partial | ManifestVerifier runs against the cleared fixtures; no PDF parsing yet |
| Search | No | Phase 10 |
| AI | No | Phase 12 |
| UI | Minimal | Headless Avalonia app test for pseudolocale; no full UI tests yet |
| 3D | No | Phase 14 |
| Performance | Baseline only | Cold-start measurement for `BenchmarkBaseline.md`; no budget gate yet |
| Packaging | No | Phase 22 |
| Architecture | Yes | Architecture tests are a new layer; they run in CI from Phase 02 onward |

---

## Domain layer tests (Unit)

### Value object tests

| Test class | Oracle | Method |
| --- | --- | --- |
| `BookIdTests` | `BookId.Create()` generates a non-empty ULID string; two calls generate distinct values; `BookId.Parse(string)` round-trips | xUnit `[Fact]` |
| `ContentHashTests` | Valid 64-char hex string accepted; 63-char string throws `ArgumentException`; non-hex chars throw; the same 64-char string parses to the same `ContentHash` (equality) | xUnit `[Theory]` with inline data |
| `IsbnTests` | ISBN-10 with valid check digit accepted; ISBN-10 with invalid digit throws; ISBN-13 with valid check digit accepted; ISBN-13 with invalid digit throws; both are normalized to 13-digit form; non-digit characters stripped before validation | xUnit `[Theory]` with 8 cases covering valid/invalid ISBN-10 and ISBN-13 |
| `ConfidenceScoreTests` | Score 0.0 accepted; 1.0 accepted; 0.5 accepted; -0.001 throws; 1.001 throws | xUnit `[Theory]` |

All value object tests are deterministic: no random input, no date/time
dependence, no file system access.

### Entity invariant tests

| Test | Oracle | Notes |
| --- | --- | --- |
| `Book_Title_MustNotBeEmpty` | Setting `Title = ""` or `Title = null` throws `ArgumentException` (domain invariant, not a DB constraint) | Validates domain model enforces invariants, not just database |
| `AuditEvent_CannotBeModified` | `AuditEvent` has no public setters after construction; the `IAuditRepository` interface has no `Update` method; confirmed by reflection: no `set` accessor on `AuditEvent` properties | NFR-PROD-013, CTRL-OGMA-018 |
| `Annotation_BoundingRect_IsValidJsonOrNull` | `Annotation` constructor with a non-JSON `BoundingRect` string throws `ArgumentException`; null is accepted | FR-READ-008 data integrity |

---

## Architecture layer tests

All architecture tests use `NetArchTest.eXtended` and run in
`OgmaLibrary.Tests.Architecture`.

| Test | Predicate | Expected result |
| --- | --- | --- |
| `Architecture_DomainProject_HasNoOutwardDependencies` | Types in `OgmaLibrary.Domain` should not depend on `OgmaLibrary.Application`, `OgmaLibrary.Infrastructure`, `OgmaLibrary.Reader`, `OgmaLibrary.Bookshelf3D`, `OgmaLibrary.Workers`, `OgmaLibrary.App` | Pass (all) / Fail with violating type names listed |
| `Architecture_OnlyAppBindsImplementations` | Types that implement any interface from `OgmaLibrary.Application` should reside in `OgmaLibrary.Infrastructure`, `OgmaLibrary.Reader`, `OgmaLibrary.Bookshelf3D`, `OgmaLibrary.Workers`, or `OgmaLibrary.App` (not in `Domain` or `Application` themselves) | Pass |
| `Architecture_OnlyInfrastructureUsesHttpClient` | Types that have a dependency on `System.Net.Http.HttpClient` should reside only in `OgmaLibrary.Infrastructure` | Pass |

### Violation tests (confirm the rules catch real violations)

These tests are written with a deliberate violation in a helper assembly (not
in the production codebase) and confirm the rule fires:

| Test | Approach |
| --- | --- |
| `ArchRule_DomainIsolation_DetectsViolation` | Compile a test-only assembly `OgmaLibrary.Domain.Violation.Test` that adds a reference to `OgmaLibrary.Application`; confirm the NetArchTest rule returns a failure result for this assembly |

This confirms the rules are not vacuously passing due to a misconfigured
predicate.

---

## UI layer tests (headless Avalonia)

### Pseudolocale test

| Test | Oracle | Layer |
| --- | --- | --- |
| `MainWindow_French_TitleIsLocalized` | Avalonia headless app starts with `CultureInfo("fr")`; the `MainWindow.Title` property returns "Bibliothèque Ogma" (from `fr.resx`) without throwing `MissingManifestResourceException` | UI (headless) |
| `MainWindow_English_TitleIsLocalized` | Same test with `CultureInfo("en")`; title returns "Ogma Library" | UI (headless) |
| `OGMA0001_FiringOnHardCodedString` | Roslyn analyzer unit test: a source snippet with `myButton.Content = "Submit";` produces diagnostic `OGMA0001` at the expected location | Architecture / build |
| `OGMA0001_NotFiringOnLocalized` | Source snippet with `myButton.Content = _loc.Get("Button.Submit");` produces zero diagnostics | Architecture / build |

---

## Golden-corpus fixture layer

### ManifestVerifier tests

| Test | Oracle | Fixture |
| --- | --- | --- |
| `ManifestVerifier_AllFixtures_HashMatch` | For each entry in `MANIFEST.sha256`, the file exists in `tests/golden-corpus/fixtures/` and its SHA-256 equals the recorded hash | All 10 fixtures (gc-simple-text through gc-forms-unusual-fonts) |
| `ManifestVerifier_DetectsCorruption` | A fixture file with one byte flipped produces a hash mismatch; `ManifestVerifier` throws `FixtureIntegrityException` | Modified copy of `gc-simple-text` (in-memory, not committed) |

### SyntheticCorpusGenerator tests

| Test | Oracle | |
| --- | --- | --- |
| `SyntheticCorpus_Seed42_500_IsDeterministic` | Two runs of `SyntheticCorpusGenerator(seed: 42, count: 500)` produce outputs that serialize to identical JSON strings | Determinism |
| `SyntheticCorpus_AllIsbnValid` | Every generated `SyntheticBook.Isbn` passes `Isbn.TryParse()`; the check digit is correct | CON-8, ISBN validation |
| `SyntheticCorpus_UniqueTitles` | No two books in a single generated corpus have the same `(Title, Author)` pair | Realistic corpus property |

---

## Performance baseline

Phase 02 does not gate against NFR-OGMA performance budgets (no production
pipelines exist yet), but it does produce **trend data**:

| Metric | Measurement | Oracle |
| --- | --- | --- |
| App cold start (Windows reference HW) | `IBenchmarkContext.Measure("app.startup")` from `OnFrameworkInitializationCompleted` to first frame rendered | Record the measured value in `docs/performance/BenchmarkBaseline.md`; no pass/fail threshold yet; budget gate added in Phase 20 |
| App cold start (macOS reference HW) | Same measurement on macOS | Record in `BenchmarkBaseline.md` |
| `dotnet build` time (clean, Release) | Wall clock of CI build step | Record in `BenchmarkBaseline.md`; track for build-time regression across phases |

---

## Beta gate coverage

Phase 02 does not cover any G1-G8 beta gates directly (those require production
feature code). However:

- **Architecture tests** provide a structural pre-condition for all gates:
  the dependency graph is correct, so the production code that gates G1-G8
  will be built on a sound foundation.
- **Golden-corpus harness** instantiation means that when Phase 05 (ingestion)
  and Phase 08 (reader) produce their first code, the fixture framework is
  already in place.

---

## Test configuration

| Setting | Value |
| --- | --- |
| Test framework | xUnit 2.x |
| Architecture test library | `NetArchTest.eXtended` (or `NetArchTest` stable on net10.0) |
| Headless Avalonia | `Avalonia.Headless.XUnit` (or the equivalent headless testing package) |
| Roslyn analyzer testing | `Microsoft.CodeAnalysis.CSharp.Testing.XUnit` |
| Code coverage | `coverlet.collector`; minimum coverage threshold: not enforced in Phase 02 (baseline collection only); threshold gate added in Phase 21 |
| CI parallel test execution | `dotnet test --parallel` per project; architecture tests run in a separate project to isolate from unit tests |

---

## Defect classification

| Tier | Description | Phase 02 examples |
| --- | --- | --- |
| R1 (data loss) | Not expected in Phase 02 (no user data operations) | A `AuditEvent` that allows `Delete` would be an R1 risk — the architecture test and entity invariant test would catch it |
| R2 (privacy breach) | Architecture test `Architecture_OnlyInfrastructureUsesHttpClient` catches any direct HTTP call from a non-Infrastructure project | A Domain entity that calls an HTTP endpoint directly would be R2 |
| R5 (functional) | Architecture test failure, value object invariant failure, i18n test failure | All Phase 02 test failures are R5 at most |

No R1 or R2 defects may be open when Phase 02 closes.
