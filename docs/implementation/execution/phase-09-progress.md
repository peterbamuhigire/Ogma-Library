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

## Remaining phase gate

Candidate blocking is now evidenced. Reversible merge/split operations,
provider-conflict decisions, user-facing review consequences, and grouping
behavior in search/advisor consumers remain before phase 9 closure.
