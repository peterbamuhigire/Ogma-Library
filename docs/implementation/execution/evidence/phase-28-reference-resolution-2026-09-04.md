# Phase 28 Local Reference Resolution Evidence

Date: 2026-09-04

## Delivered

- Comparison requests now resolve the reference title against the local
  catalogue using bounded active-title search.
- Exact-title matches win deterministically, followed by title and book-ID
  ordering; unavailable or unresolved references do not broaden retrieval.
- The resolved reference itself is excluded from recommendation candidates.
- Verified local author, category, and tag overlap contributes bounded
  deterministic reranking signals.
- No provider call, external metadata lookup, raw-path exposure, or raw-query
  persistence is introduced.

## Verification

`Phase28AdvisorIntentTests`: 14 passed, 0 failed, 0 skipped.

Full isolated solution validation:

```text
dotnet test OgmaLibrary.sln --no-restore
  -p:BaseOutputPath=tmp/full-suite-build-2026-09-04-phase28-reference/
  --logger "console;verbosity=minimal"
  --results-directory tmp/full-suite-results-2026-09-04-phase28-reference/
```

Result: 883 core + 41 architecture + 142 UI = 1,066 passed, 0 failed,
0 skipped.

## Remaining gate

Human-labeled Recall@K/nDCG benchmark, reference-machine confirmation,
provider/reference conformance, and final advisor UI/performance evidence remain
open.
