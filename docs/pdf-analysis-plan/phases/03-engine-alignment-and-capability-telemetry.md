# Phase 3 — Engine alignment and capability telemetry

**Depends on:** Phase 2; canonical phases 1, 10, 11, 38.
**Outcome:** PDFium/PDFtoImage/PdfPig versions and behavior are reproducible.

## Work

- Record exact managed/native dependency versions and platform RIDs in the
  profile and evidence manifest.
- Re-run ADR-0004’s wrapper comparison on the real acceptance corpus; do not
  infer current behavior from the 2026 synthetic spike.
- Compare the pinned PdfPig 0.1.9 path with current candidate releases before
  changing it; evaluate fixes against extraction and memory regressions.
- Build a feature probe for xref variants, encryption, fonts, annotations,
  forms, OCG, page boxes, transparency and navigation.
- Emit per-document/per-page capability telemetry with bounded size and no
  source text or secret leakage.
- Add golden image/text/diagnostic baselines that are updated only by review.

## Experiment

Run old and candidate engine stacks over identical fixture hashes. Measure
visual difference, text precision/recall, page latency, allocations, crash
rate and diagnostic stability. Roll back if any profile-critical fixture loses
fidelity or safety; standardise only after owner sign-off.

## Exit criteria

- Engine matrix and errata coverage are published.
- No dependency change lands without corpus comparison and license/native-asset
  review.
- Each unsupported feature has a known behavior and user impact.
