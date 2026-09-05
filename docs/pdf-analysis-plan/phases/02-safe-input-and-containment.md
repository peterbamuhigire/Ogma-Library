# Phase 2 — Safe input and containment

**Depends on:** Phase 1; canonical phases 5, 10, 17, 37.
**Outcome:** every PDF operation enters one verified boundary.

## Work

- Make brokered, snapshot-bound document opening the only production path.
- Replace direct `PdfDocument.Open`, PDFium and PDFsharp opens in metadata,
  ISBN, TOC, OCR, assets and write-back callers with worker/document contracts.
- Establish a stable source handle: canonical path, file identity, length,
  content hash and copy consistency check.
- Add strict/lenient parsing modes and typed error categories without broad
  “empty/zero/fallback” swallowing.
- Complete Windows and macOS OS sandbox adapters; prove network, filesystem,
  child-process and output-path denial.
- Preserve password secrecy across memory, logs, environment, command line,
  database and diagnostics.

## Tests and experiment

Architecture tests scan production assemblies for forbidden PDF opens. A hostile
corpus mutates a source during copy and attempts traversal, reparse, network,
child process and decompression/resource abuse. Hypothesis: one boundary will
reduce inconsistent failure behavior and memory duplication without increasing
reader latency. Compare failure classification, peak RSS and open-to-preview.

## Exit criteria

- Zero unapproved direct parser/render/writer file opens.
- Stable snapshot or fail-closed result for every derived artifact.
- Physical escape evidence on both supported platforms plus independent review.
- Typed password, malformed, unsupported, timeout, resource and worker-crash
  outcomes appear in the UI and job ledger.
