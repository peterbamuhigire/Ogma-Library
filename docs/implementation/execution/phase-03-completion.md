# Phase

Phase 3 - Canonical Library Identity Model

# Status

COMPLETE - 2026-08-20

# Requirements Implemented

- Established the Phase 3 domain contracts for FR-LIB-003, FR-LIB-004 and
  FR-CAT-007 without claiming the later persistence, scanner or UI workflows.
- Separated library root, file occurrence, exact content asset, edition,
  intellectual work and catalogue presentation identities.
- Made unknown, provisional, identified and ambiguous bibliographic states
  explicit.
- Scoped external identifiers by source, kind and work/edition ownership.
- Recorded detailed evidence in
  `docs/implementation/execution/evidence/phase-03/requirement-traceability.md`.

# Major Code Changes

- Added canonical strong IDs and aggregates in
  `src/OgmaLibrary.Domain/CanonicalIdentity.cs`.
- Added the versioned conservative comparison policy and decision record in
  `src/OgmaLibrary.Domain/IdentityDecisions.cs`.
- Renamed the ambiguous legacy domain and repository shapes to
  `LegacyCatalogueRecord`, `LegacyFileRecord`, `ILegacyCatalogueRepository` and
  `LegacyCatalogueRepository`.
- Removed the path-derived placeholder hash. The compatibility adapter maps only
  valid persisted file facts; invalid or absent facts remain null.
- Accepted ADR 0016 and fixed the Phase 4 migration contract.

# Database Changes

No schema or migration change. Phase 3 intentionally freezes the domain model
before Phase 4 persistence work. The required relations, keys, constraints,
legacy mapping, backup and recovery tests are specified in
`evidence/phase-03/phase-04-migration-contract.md`.

# Pipeline Changes

No scanner or processing pipeline implementation. The identity contract requires
processing to emit genuine content hashes and permits an occurrence to exist
while content identity is unknown. Filesystem outcomes for new, renamed, moved,
replaced, copied, unavailable and explicitly deleted files are specified for
Phases 4 and 7-9.

# Search Changes

No search implementation. `CataloguePresentationIdentity` establishes the
path-free work/edition/occurrence contract that later search projections must
adopt.

# AI/RAG Changes

No AI runtime implementation. The contract allows advisor retrieval to select
an edition/occurrence while diversifying by work; it cannot use a path as book
identity.

# UI Changes

No UI/API implementation, as required by the roadmap. User language now
distinguishes exact copy, another file of one edition, another edition of one
work, possible match and unavailable file. The word "duplicate" alone is not a
decision label.

# 3D Changes

No renderer change. The canonical presentation ID is the future shared 2D/3D
catalogue contract and contains no filesystem path.

# Security/Privacy Changes

- Paths remain infrastructure-owned locator data and are absent from domain
  identity evidence, decisions and presentation identity.
- External IDs cannot become authority boundaries and retain provider namespace.
- Identity decisions expose IDs, policy version, evidence tier and confidence,
  not full paths or PDF content.

# Tests Added

- Strong/default ID, content hash and enum invariants.
- Unknown content and unavailable occurrence behavior.
- Exact-hash property loop across 64 generated inputs.
- Same-edition/different-asset, same-work/different-edition, contradictory
  edition, similarity-only and provider-namespace classifications.
- Legacy SQLite adapter tests proving unknown facts remain null and verified facts
  round-trip.
- Architecture tests prohibiting paths in canonical types, obsolete ambiguous
  domain contracts and placeholder-hash code.

# Evaluations Performed

- Read the entire Phase 3 roadmap plus Phase 4/5 dependencies, database/testing
  appendices, SRS FR-LIB-003/004 and FR-CAT-007, HLD identity ownership, and
  ADR-0005 before implementation.
- Applied the software domain/database architecture and SRS traceability skills;
  applied the design-system component/error terminology rules to future-facing
  identity language.
- Compared exact copy, bibliographic edition, work, similarity and conflicting
  evidence paths. Only equal verified complete-file hashes are automatic.
- Performed source/diff checks for placeholder hashes, obsolete contracts and
  schema/package drift.

# Performance Results

- Final Release build: 0 warnings, 0 errors (1 minute 39.69 seconds on this host).
- Final sequential Release regression: 832/832 passed - 41 architecture, 662
  core/service/database/performance and 129 headless UI tests.
- 3D retained budget: shelf p95 0.057 ms; grid3d p95 0.069 ms.
- npm audit: zero vulnerabilities; typecheck and bundle succeeded.
- NuGet transitive vulnerability scan: no vulnerable packages in all ten
  production/test projects.
- Phase 3 adds no database or hot-path lookup, so Phase 4 owns index-plan and
  migration performance measurements.

# Deviations From Plan

- The roadmap named the existing class `BookRepository`; the implementation
  removes its ambiguous public semantics now but retains a clearly named legacy
  adapter until Phase 4 migrates consumers and persistence. Removing it entirely
  in Phase 3 would have prematurely implemented the schema phase.
- The existing design offered no explicit decision outcome for contradictory
  edition identifiers. The policy returns a reviewable `PossibleMatch` at reduced
  confidence rather than selecting a plausible-looking edition relationship.

# Deferred Findings

- Phase 4: canonical schema, constraints, indexes, backup, aliases, repository
  projections, migration and recovery tests.
- Phase 5: multiple approved roots, path authorization and volume/root health.
- Phases 7-9: scanner observations, reconciliation and reversible merge/split UI.
- Later search, advisor and 3D phases: adopt the shared presentation identity.

# Kaizen Cleanup

- Removed the fabricated path-derived hash and the misleading claim that one
  domain `Book` was the identity authority.
- Renamed compatibility code so new consumers cannot mistake it for canonical
  design.
- Centralized identity comparison in one versioned policy rather than scattering
  fuzzy merge logic.
- Kept the model path-free, provider-neutral and persistence-independent.
- Added executable drift gates plus a concrete Phase 4 migration handoff.

# Definition of Done Verification

- [x] Invariants and terminology are approved in ADR 0016 and the terminology
  contract.
- [x] No fake hash is permitted or generated.
- [x] Exact content copy, same edition/different asset and same-work/different-
  edition are distinct executable outcomes.
- [x] Unknown and ambiguous identity are representable without silent merging.
- [x] Domain-model freeze candidate is approved; Phase 4 owns persistence
  ratification.
