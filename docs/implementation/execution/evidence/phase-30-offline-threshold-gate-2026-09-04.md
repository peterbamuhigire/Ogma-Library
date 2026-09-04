# Phase 30 Offline Threshold Gate Evidence

Date: 2026-09-04

## Scope

`AdvisorOfflineEvaluator.EvaluateGate` compares a versioned evaluation report
with explicit lower bounds for Precision@K, Recall@K, MRR, nDCG, grounding,
constraint satisfaction, and diversity. It fails closed for an empty case set
and rejects out-of-range thresholds before evaluation.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase30AdvisorQualityTests" --verbosity minimal -m:1
```

Result: 5 passed, 0 failed, 0 skipped.

The tests cover the existing metric report plus a fully passing explicit
threshold set and an empty-evidence fail-closed result.

## Still open

Human-labeled benchmark data, feedback consent, quarantined live-provider
evaluation, full-shell accessibility, and retrieval-freeze evidence remain
open.
