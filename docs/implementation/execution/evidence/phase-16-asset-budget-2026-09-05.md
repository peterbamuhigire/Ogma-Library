# Phase 16 Evidence - Large-Library Preferred Asset Lookup

Date: 2026-09-05

## Verification

The visual-asset manifest lookup was exercised against 50,000 books, each with
one ready cover manifest row. One hundred lookups for distinct books were timed
using high-resolution stopwatch ticks after the corpus was persisted in SQLite.

```text
books=50000
samples=100
p95Milliseconds=0.424
budgetMilliseconds=150
```

The lookup returned the requested book's ready cover on every sample. The
query remains book/kind/status scoped and uses the manifest index; no high-
resolution variants or image bytes are materialized by the catalogue lookup.

## Gate disposition

The local 50,000-book preferred-asset lookup subgate is closed at p95 <=150 ms.
Disk-generation volume, GPU/texture residency, physical accessibility, and
cross-platform asset budgets remain open and are not inferred from this test.
