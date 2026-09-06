# Phase 34 CI Load-Smoke Isolation

Date: 2026-09-06
Reviewer: Peter Bamuhigire, Lead Consultant

## Finding and correction

Protected CI run 34022144296 reported a Windows catalogue P95 of 7,828 ms while
the same test passed on macOS and on the following Windows run. The live HTTPS
load smoke was running concurrently with unrelated heavy test collections and
included cold TLS/JIT/route initialization in all simultaneously launched
samples.

The load-smoke collection is now non-parallel with other test collections and
performs one explicit authenticated warm-up request before measuring concurrent
steady-state requests. The existing catalogue/page concurrency and P95 `< 2,000
ms` thresholds are unchanged.

## Verification

The focused two-test load slice passed three consecutive local runs:

```text
Run 1: Passed 2, Failed 0 (10 s)
Run 2: Passed 2, Failed 0 (10 s)
Run 3: Passed 2, Failed 0 (11 s)
```

## Gate disposition

The full-suite benchmark-contention/cold-start ambiguity is removed while the
steady-state target remains enforced. This does not replace sustained physical
two-machine load/soak, cold-start, or reference-hardware evidence.
