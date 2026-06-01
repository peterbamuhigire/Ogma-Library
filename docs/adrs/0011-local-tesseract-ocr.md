# ADR-0011 Local Tesseract OCR

Date: 2026-06-01

## Status

Accepted for Phase 15 implementation.

## Context

Phase 15 requires scanned PDFs to become searchable without cloud OCR egress.
The OCR pipeline must run as a resumable background job, use the existing PDF
renderer for page images, and keep the engine replaceable for future community
or school-lab deployments.

## Decision

Ogma uses the `Tesseract` NuGet package as the local OCR engine behind the
`IOcrProvider` application contract. The production adapter lives in
`OgmaLibrary.Infrastructure.Ocr.TesseractOcrProvider`, so workers and UI code do
not depend on Tesseract APIs directly.

English OCR data ships through the `Tesseract.Data.English` package. Its build
target copies `tessdata/eng.traineddata` into the application output, and the
provider defaults to `AppContext.BaseDirectory/tessdata`.

OCR jobs render pages through `IPdfRenderer`, process one page at a time, store
text as `ExtractedPages.Source = "OCR"`, and mark the book `IsOcrDerived` only
after completion. Missing native binaries or language data fail the individual
OCR job rather than desktop startup.

## Consequences

- OCR stays local and offline by default.
- The app avoids committing large language-data binaries directly to git.
- Additional languages can be added later as package or on-demand downloads.
- Engine replacement remains possible by registering another `IOcrProvider`.
- Release packaging must verify that native Tesseract assets and
  `tessdata/eng.traineddata` are present in MSIX and DMG outputs.
