# Whole-app downstream audit

## Ingestion and identity

The canonical identity and scan plans are strong prerequisites. PDF identity
must bind edition/file observations to a content hash and retain path history.
Rename/move must not trigger unnecessary re-extraction; replacement must. A
disconnected root must not infer deletion. Structural PDF diagnostics belong to
the same lifecycle as extraction artifacts.

## Metadata, ISBN and write-back

Metadata extraction and ISBN detection still contain direct PdfPig opens, so
they bypass the intended security boundary and artifact provenance. Route them
through the document context or consume completed page/text artifacts. Keep
candidate evidence separate from canonical user-approved metadata.

ADR-0008’s database-first decision is correct for safety. PDF write-back should
remain disabled by default until the writer can preserve or deliberately
invalidate signatures, retain required objects, produce a verifiable output,
and provide backup/diff/restore/audit. PdfPig “can reopen the file” is not a
sufficient conformance or preservation check.

## OCR, FTS, embeddings and AI

Phase 23/24 made good progress on versioning, selective OCR, confidence and
staged FTS rebuilds. The remaining PDF-specific rule is provenance continuity:

```text
source hash + physical page + effective text layer + extractor/config version
  -> OCR alternative (if selected) -> FTS chunk -> embedding -> AI citation
```

No embedding or language-model answer may be treated as PDF truth. If the page
is scanned, OCR confidence is low, reading order is ambiguous or extraction is
stale, the user should see that state and the AI gateway must preserve the page
anchor and uncertainty.

## Reader and portability

The reader now has important navigation, zoom, page-scroll and cache/session
slices. The remaining standard-aligned work is actual page geometry, progressive
rendering, multi-page virtualisation/continuous mode, reliable text selection,
destination navigation, annotations, and physical keyboard/screen-reader
journeys. Reader-state export is application data, not a PDF conformance feature;
it must not claim portability of PDF annotations until PDF write-back exists.

## 3D, classroom and LAN

These are not PDF parser features. They must nevertheless consume canonical
book/page/asset identifiers and the same safe file/active-content policies. LAN
and classroom views should serve approved rendered/derived assets or a guarded
read model, not expose arbitrary source paths or active PDF content by default.
3D is a catalogue enhancement and must never be the only route to a PDF.

## Packaging and release

PDFium native assets, OCR data, parser versions, errata profile, sandbox
profiles, licenses/notices and fixture hashes must be included in release
provenance. Signed Windows and notarized macOS builds, clean-install tests,
rollback, backup/restore and physical accessibility remain release gates—not
documentation assumptions.
