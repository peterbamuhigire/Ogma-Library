# Phase 7 — Text semantics and extraction

**Depends on:** Phase 4; canonical phases 11, 23–24.
**Outcome:** copy, search, OCR and AI receive faithful, qualified page text.

## Work

- Preserve glyph/run coordinates, writing direction, font identity, encoding,
  `/ToUnicode` presence and extraction diagnostics.
- Calibrate reading order for columns, headers/footers, vertical text,
  ligatures, hyphenation, whitespace and Unicode normalization.
- Define extraction quality from corpus measurements, not only word-count
  heuristics. Keep `Full`, `Partial`, `Scanned`, `Empty` plus reason/confidence.
- Fix search so a scanned placeholder is not a match unless the query matched
  OCR text; use durable FTS when available and stream bounded fallback work.
- Preserve primary text and OCR alternatives with page/content/extractor
  provenance; make user selection explainable.
- Add tagged structure/alternate-text ingestion where the engine exposes it,
  without inventing semantics when it does not.

## Experiment and exit

Use ground-truthed text PDFs with valid/missing mappings, multi-column and scan
fixtures. Measure character accuracy, word/reading-order accuracy, search
recall/precision, page-anchor accuracy and OCR selection correctness. Exit when
every derived text result identifies its source layer and no silent substitution
occurs.
