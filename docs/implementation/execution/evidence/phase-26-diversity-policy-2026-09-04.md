# Phase 26 hybrid diversity policy evidence

Date: 2026-09-04

Hybrid ranking now accepts an optional `HybridDiversityPolicy`. The default
semantic-search path caps repeated known authors at three results while
preserving deterministic score and book-ID ordering; callers can use
`HybridDiversityPolicy.None` when pure score order is required. Missing authors
use the book ID as their independent key, preventing unrelated metadata-poor
books from being grouped together.

Verification: `HybridRankingServiceTests` passed 8/8, including deterministic
one-per-author interleaving and the existing fallback, blend, tie-break, and
100-query determinism coverage.

Remaining Phase 26 gates include representative-corpus quality metrics, ANN or
equivalent target-scale retrieval, 50,000-book latency/memory acceptance, and
the final search-contract freeze.
