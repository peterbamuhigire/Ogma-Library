# Phase 09 Progress - Duplicate and Bibliographic Resolution

Date: 2026-09-04

## Delivered in this increment

- Added `IIdentityDecisionService` and a durable repository over the canonical
  `IdentityDecisions` table.
- Domain `IdentityDecisionPolicy` now has an executable persistence boundary:
  exact complete-file hashes are automatic, while edition/work identifiers and
  similarity evidence remain review-required.
- Repeated evaluation of the same occurrence pair and policy version returns the
  existing decision instead of creating a duplicate.
- Review queries are deterministic and SQLite-compatible.
- Added acceptance tests for exact-copy idempotency and same-edition review
  proposals.
- Added deterministic candidate blocking over scoped identifiers and normalized
  title/author/year keys. Broad buckets are bounded at 256 candidates, avoiding
  an unbounded all-pairs comparison while retaining deterministic pair output.
- Added correctness and adversarial 10,000-profile scale tests for candidate
  blocking.
- Added durable reviewed identity groups with explicit edition/work scope,
  before/after change history, merge, split and exact undo operations. Active
  membership is deterministic and an occurrence cannot be placed into two
  active groups.
- Added acceptance tests proving merge/split/undo preservation and duplicate
  membership rejection.
- Added canonical alias/occurrence/group read projection for legacy book IDs.
- Catalogue summaries now collapse reviewed edition groups to a deterministic
  representative, while advisor retrieval collapses reviewed work/edition
  groups before ranking.
- Added persistence-boundary coverage proving canonical alias resolution and
  catalogue representative selection.

## Remaining phase gate

Closed. Candidate blocking, conservative provider-conflict decisions,
reversible merge/split persistence, canonical alias resolution, catalogue
representative selection, and advisor grouping behavior are now evidenced.
Physical operator review screens and cross-platform UI walkthroughs remain
release gates to be assessed in their owning platform/release phases.

The 2026-09-06 evidence-tooling smoke also passed after correcting the
committed-clean preflight selection and nested-exit-code handling. The
repository-level remote-CI sub-gate is now evidenced by the green Windows/macOS
matrix in `evidence/ci-cross-platform-regression-2026-09-06.md`; the owning
signoff remains fail-closed for manual operator review, accessibility, and
stale-preflight evidence. See
`evidence/phase-09-tooling-smoke-2026-09-06.md`.
