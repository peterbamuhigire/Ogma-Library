# Phase 24 Progress - Selective OCR and Extraction Quality

Date: 2026-08-30

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

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- Phase 24 policy and OCR integrity tests: 7 passed in the focused regression
  slice; the restored English asset passed the approved checksum check.
- OCR/golden/schema regression slice: 13 passed.
- OCR control and progress UI slice: 2 focused tests passed, with the broader
  Avalonia search/index suite passing 14 tests.

## Remaining phase gate

Real mixed-PDF accuracy and CPU/memory corpus evidence, and cross-platform
packaged asset proof remain before phase 24 closure. OCR UI quality controls are
closed by the tested Index Manager state/progress/actions; physical assistive
technology evidence remains `NOT ASSESSED`.
