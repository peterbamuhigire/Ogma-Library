# Phase 15 OCR Pipeline Golden Corpus

This corpus pins the `scanned-image-only` acceptance scenario for FR-READ-010.
The PDF fixture is intentionally tiny and is opened through the deterministic
test renderer; the oracle lives in `expected-words.txt` and verifies that OCR
output becomes page search chunks and FTS5 results.

