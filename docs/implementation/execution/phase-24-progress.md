# Phase 24 Progress - Selective OCR and Extraction Quality

Date: 2026-09-04

## Delivered in this increment

- Added a deterministic local page-quality policy: complete text pages are not
  OCRed; scanned, empty and low-word partial pages are eligible.
- Added a supported local language-pack policy for `eng`, `fra`, `deu`, `ita`
  and `spa`, with canonical selectors and bounded input length.
- Added OCR provenance fields for confidence, language, model version,
  extraction version and source-content hash.
- Added persisted selected-text state so OCR is an alternative rather than an
  overwrite of primary extraction.
- Added confidence-gated selection: good primary text remains selected; OCR is
  selected only for missing/failed/scanned/low-quality primary text and a
  confidence of at least 0.75.
- Added page-count and rendered-image size guards to keep OCR resource use
  bounded and failure non-blocking.
- Added provider-side language validation and regression coverage for text-page
  skipping, confidence selection, primary preservation, language policy and
  the existing OCR/golden corpus workflows.
- Added fail-closed SHA-256 verification for packaged Tesseract training data;
  the restored `eng.traineddata` asset is allow-listed for the pinned
  `Tesseract.Data.English` package, while language packs without an approved
  checksum are rejected before OCR starts.
- OCR invalid payloads and resource-limit failures now persist stable failure
  codes (`ocr_invalid_payload`, `ocr_page_limit`, and `ocr_render_limit`) into
  the shared retry/diagnostic runtime without exposing limit details as error
  text.
- The desktop Index Manager exposes OCR state, bounded page progress, pause,
  cancel, and retry actions with bound accessible names and safe state-based
  enablement.
- Running OCR now observes pause/cancel at durable page boundaries. The
  paused state is distinct from dead-letter quarantine, lease fields are
  cleared atomically, resume skips completed pages, and each control is
  audited without payload data. Evidence:
  `evidence/phase-17-ocr-cooperative-control-2026-09-06.md`.
- Expanded the deterministic mixed-quality extraction benchmark from 32 to 500
  books (1,500 pages) to provide a larger local throughput/allocation baseline.
- Prevented shared integration contexts from retaining completed page/chunk
  graphs across large batches; the repeat benchmark now measures the pipeline
  rather than unbounded test-context tracking.
- Reconciled the local Phase 24 evidence against the current solution run: the
  selective policy, packaged English checksum, stable failure codes, OCR
  controls, and 500-book mixed-quality synthetic baseline remain green.
- Added an end-to-end packaged-Tesseract fixture proof: a deterministic
  rasterized scanned PDF is rendered through the production isolated worker,
  recognized with the packaged English model, and checked for full expected-
  word recall and the 0.75 confidence threshold. Focused result: 1/1 passed.
  Evidence: `evidence/phase-24-packaged-tesseract-fixture-2026-09-05.md`.
- Instrumented that packaged fixture with test-host CPU/wall-time observations
  and isolated-renderer peak/private-memory telemetry. The renderer remained
  within its configured 768 MiB ceiling (58,159,104-byte peak working set and
  23,785,472-byte private memory); this is a local regression baseline, not a
  reference-machine performance claim. Evidence:
  `evidence/phase-24-packaged-ocr-resource-observation-2026-09-06.md`.
- The complete serialized Release core suite subsequently passed 925/925;
  architecture and UI baselines remain green at 41/41 and 159/159.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- Phase 24 policy and OCR integrity tests: 7 passed in the focused regression
  slice; the restored English asset passed the approved checksum check.
- OCR/golden/schema regression slice: 13 passed.
- OCR control and progress UI slice: 2 focused tests passed, with the broader
  Avalonia search/index suite passing 14 tests.
- Current full solution verification: 885 core + 41 architecture + 145 UI =
  1,071 passed, 0 failed, 0 skipped.

## Remaining phase gate

The local selective-policy, checksum-integrity, stable-failure-code,
cooperative OCR-control, synthetic 500-book mixed-quality, and generated
packaged-fixture telemetry sub-gates are closed. Representative real mixed-PDF
accuracy and resource-corpus evidence, cross-platform packaged asset proof, and
physical assistive-technology evidence remain before phase 24 closure.

The Aug-39 Definition of Done now records selective text skipping,
versioned low-quality/image detection, and non-blocking OCR failure as closed.
Representative accuracy/resource-corpus and two-platform packaged-asset gates
remain unchecked.
