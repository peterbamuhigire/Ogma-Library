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

## Remaining phase gate

Candidate blocking and reversible merge/split persistence are now evidenced.
Provider-conflict decision tests, user-facing review consequences, and direct
grouping behavior in search/advisor consumers remain before phase 9 closure.
