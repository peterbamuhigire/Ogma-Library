# Phase 10 — Derived assets, OCR, search and AI provenance

**Depends on:** Phases 4, 5 and 7; canonical phases 12–16 and 23–30.
**Outcome:** every derived result remains traceable to the effective PDF page.

## Work

- Prefer valid embedded thumbnails where available; otherwise generate bounded
  worker previews with content/render-version cache keys and visible failure.
- Record source hash, physical page, page label, render/extractor/OCR/index
  version, language, confidence and selection reason for every derived artifact.
- Keep primary extraction and OCR alternatives separate; preserve page anchors
  through FTS, embeddings, citations and advisor answers.
- Ensure stale source hashes remove or quarantine stale FTS/vector/asset results.
- Prevent AI services from receiving source text unless consented through the
  gateway; retain uncertainty and extraction quality in answer provenance.
- Treat 3D/classroom/LAN as consumers of approved assets/read models, not as
  new PDF parsing paths.

## Experiment and exit

Replace a source PDF, rerun extraction and verify that old search, vectors,
thumbnails and AI citations cannot surface as current. Measure OCR accuracy,
asset generation latency and stale-result rate. Exit when a reviewer can trace
any displayed answer back to hash/page/layer/version.
