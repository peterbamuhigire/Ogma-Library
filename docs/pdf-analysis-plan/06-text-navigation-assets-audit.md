# Text, navigation and asset audit

## Text extraction

The current PdfPig text layer produces normalized words and top-left normalized
boxes. It caps words/characters and classifies pages as Full, Partial, Empty or
Scanned. This is a useful bounded primitive, but “words exist” does not prove
correct Unicode, reading order or semantic structure.

The next contract must preserve glyph/run/page provenance, font and encoding
diagnostics, `/ToUnicode` availability, direction, reading-order confidence,
coordinates, ligature handling and extraction version. Missing or ambiguous
mapping must be reported as degraded. OCR must remain an alternative derived
layer, never overwrite primary extracted text.

The present in-document search has two concrete quality issues: it scans pages
sequentially rather than using the durable index when available, and its scanned
placeholder can be returned as a match even when the query is not found. Search
results must be page-anchored, bounded, version-aware and honest about OCR or
degraded text provenance.

## Navigation

`PdfTableOfContentsService` extracts bounded bookmark nodes and page numbers,
which is a useful fallback. It currently discards or does not expose named and
explicit destinations, target coordinates/zoom, page labels, link actions,
attachments and the distinction between a page-tree index and semantic
structure. These are independent PDF features and should not be conflated.

Target navigation model:

```text
physical page index -> localized PDF page label
outline node -> destination -> page/coordinates/zoom
link annotation -> safe internal target or policy-filtered external target
history -> back/forward destination state
```

If a destination cannot be resolved, fall back to the physical page index and
show a degraded diagnostic. Never silently jump to page one.

## Thumbnails and covers

The generated first-page JPEG and visual-asset manifest are good foundations.
The remaining gaps are embedded `/Thumb` preference, page/variant semantics,
content/parser/render-version cache keys, lazy low/high variants, aspect-ratio
policy, and visible loading/failure states. A missing thumbnail must not appear
as an empty card or unexplained colored dialog.

The target pipeline is: inspect valid embedded thumbnail; otherwise render a
small bounded first page through the worker; register a versioned artifact;
validate dimensions/bytes; expose loading, success and actionable failure.
Thumbnail work is low-priority and cancellable so it cannot starve the reader.

## Metadata and structure

Info dictionary, XMP metadata, catalog language, page labels, tagged structure,
alternate text and attachments need a documented precedence and trust model.
Metadata is data from the file, not automatically verified truth. Derived ISBN,
FTS, OCR and AI records must retain source/page/extractor/config evidence.
