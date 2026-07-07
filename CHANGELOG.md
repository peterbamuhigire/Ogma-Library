# Changelog

All notable changes to Ogma Library are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html) from its first
release.

## [Unreleased]

### Added

- **Phase 00 — Project inception & decision closure.** Closed all 8 open
  questions and 9 context gaps (`docs/plans/grand-plan/phase-00/decisions.md`);
  ratified ADR-0001…0009 and drafted ADR-0010 (opt-in LAN Host mode); added the
  open-source governance baseline (CONTRIBUTING, CODE_OF_CONDUCT, DCO, SECURITY,
  PR template with Change-Impact-Analysis, Conventional Commits hook, branch
  strategy, reference-hardware spec).
- **Phase 01 — Risk spikes.** Seven throwaway proofs under `spikes/`: .NET 10
  dependency matrix, PDFium wrapper benchmark (PDFtoImage selected), WebView↔C#
  bridge contract (7/7), 500-spine WebGL2 scene, SQLite FTS5 (P95 1.97 ms), the
  `IAiProvider` gateway, and LAN transport (196.75 MB/s). ADR-0003/0004/0010
  amended with measured evidence (`spikes/RESULTS.md`).
- **Phase 02 — Solution scaffolding.** The 9-project `OgmaLibrary.sln` on .NET 10
  with `Directory.Build.props` (warnings-as-errors, XML docs), `.editorconfig`,
  and `nuget.config`. Domain skeleton (value objects with real ISBN check-digit
  and SHA-256 hashing, entities, repository interfaces). `IBenchmarkContext` +
  `StopwatchBenchmarkContext`; the single `CompositionRoot`. Three NetArchTest
  architecture rules (domain isolation, no-HTTP egress chokepoint, App-only DI).
- **Phase 02 — Running Avalonia skeleton.** A localized main window (full English
  and French via `ILocalizationService`, no hard-coded UI strings), rendered and
  screenshot-verified headlessly (`OgmaLibrary.Tests.Ui`).
- **Phase 02 — Test harness & CI.** Golden-corpus harness (`ManifestVerifier`,
  seed-deterministic `SyntheticCorpusGenerator`) and a GitHub Actions CI matrix
  on Windows + macOS (format, build, test, screenshot artifacts).

### Fixed

- **July 2026 remediation Phase 01 - restore/build stabilization.** Canonical
  restore now passes with NuGet audit and warnings-as-errors still enabled by
  upgrading EF Core SQLite declarations to 9.0.17 and explicitly resolving the
  SQLitePCLRaw native bundle to 3.0.3. The Phase 15 OCR migration regression
  test now seeds/asserts the previous schema through raw SQLite commands so it
  remains valid against the patched native SQLite engine. (`F-BLD-001`,
  `F-TEST-001`)
- **Reader now displays rendered PDF pages.** The page-render pipeline
  (`PdfiumAdapter` → PNG, `PageRenderCache`, `ReaderSessionService.CurrentRenderer`)
  existed but was never surfaced in the UI — the reader showed only a "Page X of N"
  placeholder. `ReaderViewModel` now renders the current page off the UI thread
  (2× supersampled, stale renders cancelled) into a `PageImage` bitmap, and
  `ReaderView` binds it to an `Image`; the page-number text is now a fallback shown
  only until the bitmap is ready. Rendering refreshes on open and on every page
  change. (FR-READ-001)
- **Page navigation by mouse wheel and keyboard.** Scrolling the wheel over the page
  turns pages (down = next, up = previous), as do PageDown/PageUp, the arrow keys,
  and Space — previously navigation was only possible via the toolbar buttons.
- **"Back to Library" now closes the open document.** Returning to the catalogue
  previously only switched views, leaving the PDF renderer and file handle open and
  reading progress unflushed. `ReaderViewModel.CloseAsync` /
  `MainShellViewModel.ReturnToLibraryAsync` now flush progress and release the
  renderer and rendered bitmap before returning. (NFR-OGMA-008)

### Notes

- Tracked follow-ups within Phase 02: the OGMA0001 hard-coded-string Roslyn
  analyzer (`TRACK-P02-ANALYZER`; the no-hard-coded-strings rule is currently
  enforced by convention, code review, and the pseudolocale/culture-switch
  tests), and publishing reference docs to a developer site.
- The product is pre-release; nothing here constitutes a shipped version yet.
