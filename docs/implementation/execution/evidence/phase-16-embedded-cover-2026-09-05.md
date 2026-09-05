# Phase 16 Evidence - Embedded Cover Precedence

Date: 2026-09-05

## Implemented

The PDF worker now inspects only the first page for a bounded set of embedded
images. Candidates are capped by count, encoded size and dimensions, decoded
through SkiaSharp, normalized to PNG, and resized inside the worker to the
requested cover variant. Unsupported or malformed embedded images fall through
to the existing deterministic first-page render. A successful embedded result
is recorded in the visual-asset manifest with source `embedded`, so it outranks
provider and generated artwork while remaining below the protected custom
cover.

## Verification

- Embedded-cover worker output regression: 1 passed.
- End-to-end `ThumbnailService` embedded-source and manifest-precedence
  regression: 1 passed.
- Combined PDF-worker and Phase 16 visual-asset slice: 18 passed, 0 failed,
  0 skipped.
- Release solution build: 0 warnings, 0 errors.

## Gate disposition

The embedded-source acquisition and precedence subgate is closed locally. Large
library asset-budget measurements, physical accessibility, and cross-platform
asset evidence remain open and are not inferred from this headless run.
