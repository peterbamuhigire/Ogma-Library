# Phase 13 Evaluation Harness

This harness evaluates the AI Reading Advisor in deterministic structural mode.
It does not judge taste or literary quality; it checks whether advisor output can
be safely shown in Ogma: local-only provenance, bounded recommendation shape,
non-empty explanations, confidence labels, and stable result metrics.

Run the CI-safe mock evaluation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/evaluation/phase-13/run-eval.ps1
```

The run writes `docs/benchmarks/phase-13/eval-mock-20260601.json` and fails if
the 20-query fixture set does not reach a 100% structural pass rate.

Real-provider evaluation is manual in Phase 13 because provider quality,
latency, pricing, and privacy-consent state are environment-dependent. Use the
mock result as the release gate and compare real-provider runs against it during
product review.
