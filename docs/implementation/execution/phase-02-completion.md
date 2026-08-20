# Phase

Phase 2 — Composition, Configuration and Startup

# Status

COMPLETE — 2026-08-20

# Requirements Implemented

- Implemented deterministic modular composition for NFR-OGMA-001/005 and
  NFR-PROD-001/009 within the Phase 2 scope.
- Added typed, redacted runtime options with external capabilities disabled by
  default and no credentials in application configuration.
- Added a cancellable required/optional startup contract, capability health and
  recoverable catalogue gating.
- Preserved later physical reference-device acceptance as `NOT ASSESSED`; see
  [requirement traceability](evidence/phase-02/requirement-traceability.md).

# Major Code Changes

- Replaced the 295-line registration body in
  `src/OgmaLibrary.App/CompositionRoot.cs` with six deterministic registrars in
  `src/OgmaLibrary.App/Composition/`.
- Added `OgmaRuntimeOptions` and `OgmaConfigurationException` in
  `src/OgmaLibrary.App/Configuration/`.
- Added startup contracts, ordered tasks, capability probing and the coordinator
  in `src/OgmaLibrary.App/Startup/`.
- Made `App.axaml.cs` assign a lightweight window first, yield a dispatcher frame
  and compose the validated runtime outside the UI thread.
- Reduced `ApplicationStartup.cs` to the coordinator lifecycle facade.
- Removed inactive AI advisor/pipeline/view-model registrations from the core
  graph; Phase 27 remains the activation authority.
- Consolidated the PDF worker to one options-aware registration shared by
  ingestion and reader services.

# Database Changes

No schema or migration change. Existing migrations now execute as the required,
recoverable `catalogue.migration` startup task before the catalogue can open.

During full regression, concurrent LAN audit writes were found contending for
SQLite's single-writer lock. `AuditRepository` now serializes its append-only
write stream. This changes neither schema nor audit durability and preserves all
events in deterministic order.

# Pipeline Changes

- Startup order is migration → interrupted-job recovery → hosted workers.
- Required failure stops unsafe continuation; optional failure leaves the
  catalogue usable.
- Tasks are idempotent/retryable, cancellation-aware and stopped in reverse order.
- Search readiness and optional capability health are deferred from catalogue
  availability.

# Search Changes

No search contract change. Search/index services remain registered locally, but
index readiness is reported as deferred and does not block catalogue startup.

# AI/RAG Changes

External AI remains disabled. The invalid prior state—advisor pipelines registered
without `IAiGateway`—was removed from default composition. No prompt, embedding or
provider request occurs during startup.

# UI Changes

- Added `DesktopShellWindow` and `StartupShellViewModel` for bootstrap, ready,
  partial-degraded, blocked, retry and diagnostic-export states.
- Corrected catalogue visibility so blocked/bootstrap surfaces cannot show stale
  library UI behind them.
- Corrected recovery focus timing so the enabled retry action receives keyboard
  focus after bindings settle.
- Added English/French startup strings and token-backed loading/recovery styling.
- State behavior is recorded in the
  [startup state matrix](evidence/phase-02/startup-state-matrix.md).

# 3D Changes

No 3D activation. The option and capability probe can report that runtime
detection is required; they do not claim WebGL/native-host availability.

# Security/Privacy Changes

- Invalid paths, booleans and worker settings fail closed without echoing values.
- Capability probes do not contact external services or open PDFs.
- Startup diagnostics export stable codes, summaries and timing only; configured
  paths, exception text, PDF content, prompts and credentials are excluded.
- External metadata and classroom Host require explicit opt-in; external AI has
  no Phase 2 activation option.

# Tests Added

- Composition disabled/enabled matrices and strict service-provider validation.
- Options and worker-path redaction tests.
- Required/optional startup failure, retry, cancellation and reverse-stop tests.
- Real migration/hosted-worker startup integration test.
- Module-order and non-blocking cold-start architecture drift tests.
- Headless bootstrap, blocked-degraded and partial-degraded render tests with
  screenshot evidence.

# Evaluations Performed

- Reviewed the full Phase 2 roadmap, related SRS/HLD requirements and later-phase
  dependency constraints before implementation.
- Applied the software architecture/configuration, SRS traceability and design
  system state/error skills.
- Visually inspected the generated startup screenshots at original resolution.
  This found and corrected a catalogue-visibility binding defect.
- Exercised default/off and explicit metadata-on matrices without network calls.
- Repeated the unchanged 20-client LAN catalogue P95 gate three times after the
  audit-write contention correction.

# Performance Results

- Release build completed in 1 minute 54 seconds with 0 warnings and 0 errors.
- Bootstrap window is assigned before graph validation and database/worker work;
  architecture tests prohibit synchronous startup initialization and require the
  dispatcher-yield plus background composition shape.
- 20-client authenticated LAN catalogue P95 gate: three consecutive focused
  passes with the existing `< 2,000 ms` assertion after serializing audit writes.
- Final sequential Release regression: 813/813 passed — 39 architecture, 645
  core/service/database/performance and 129 headless UI tests.
- CI and contributor commands now use `-m:1` so performance test projects do not
  compete with separate architecture/UI test hosts on shared runners. The
  unchanged metadata 2,000-book budget passed three isolated runs and passed the
  final sequential suite; the concurrent page-render test passed five isolated
  runs and the final sequential suite.
- Retained 3D gates: npm audit found zero vulnerabilities; typecheck and bundle
  passed; shelf p95 0.115 ms and grid3d p95 0.105 ms on this host.
- NuGet transitive vulnerability scan reported no vulnerable packages in any
  application or test project.
- W-REF-01 physical cold-start P95 remains `NOT ASSESSED` in Phase 2 and is not
  represented as a pass by the headless/architectural evidence.

# Deviations From Plan

- The roadmap described missing AI registrations as a current gap, but strict
  validation proved the existing core registered AI pipelines without their
  gateway. Because Phase 27 owns external AI activation, the smallest correct
  Phase 2 action was to remove those inactive registrations rather than introduce
  a premature provider or misleading placeholder gateway.
- Full regression exposed pre-existing LAN SQLite write contention. It was fixed
  immediately because the unchanged release performance gate failed; no test
  threshold was relaxed.

# Deferred Findings

- Physical W-REF-01/M-REF-01 startup, cached page-turn and cross-platform evidence
  remains assigned to the performance/release phases.
- Search index readiness is detection-pending until the search lifecycle phases.
- 3D host/WebGL capability remains detection-pending until Phases 31–33.
- AI provider configuration, privacy gateway and cost runtime remain Phase 27.
- Canonical multi-root configuration replaces the compatibility `LibraryRoot`
  option in Phase 5.

# Kaizen Cleanup

- Split one oversized composition body into named, ordered responsibility modules.
- Removed duplicate PDF worker and incomplete AI graph registrations.
- Centralized safe configuration parsing, startup timing, retry and capability
  reporting.
- Preserved existing good services and schemas instead of rewriting unrelated
  subsystems.
- Converted UI startup failures from blocking/silent behavior into explicit,
  testable and recoverable states.

# Definition of Done Verification

- [x] All modules resolve in disabled and explicit-metadata-enabled matrices.
- [x] Startup is asynchronous, cancellable, retryable and contains no synchronous
  migration or shell initialization wait.
- [x] Catalogue opens when optional startup work fails and remains hidden when a
  required migration fails.
- [x] Configuration is typed, validated, fail-closed and redacted.
- [x] Architecture tests lock deterministic modules and the cold-start shape.
- [x] Startup/capability contracts and UI state matrix are documented.
- [x] Phase-specific, dependent and rendered UI tests pass.
