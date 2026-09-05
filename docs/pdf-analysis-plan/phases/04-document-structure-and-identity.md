# Phase 4 — Document structure and identity

**Depends on:** Phase 3; canonical phases 3–11.
**Outcome:** a stable effective PDF document/page model drives all consumers.

## Work

- Model header/version, linearization marker, trailer chain, xref tables/streams,
  object streams, incremental revisions and encryption state.
- Resolve the catalog, page tree, inherited attributes and effective page count
  once per document context.
- Add page identity: physical index, localized page label, source hash,
  revision, parser/config version and optional content hash.
- Distinguish document open, structural parse, page model, render, text and
  navigation status.
- Retain partial page failures with page/object context; do not turn them into
  a document-wide empty result.

## Data contract

`DocumentSnapshot`, `PdfDocumentFacts`, `EffectivePageGeometry` and
`PdfDiagnostic` should be immutable/read-only from Application. Derived tables
must reference snapshot and extractor versions. Cache keys must include the
revision/content hash.

## Experiment and exit

Compare repeated service opens with one context over 20 varied PDFs. Measure
open time, allocations, page count correctness and duplicate parser count.
Exit when all consumers read the same snapshot/page model, xref/incremental
fixtures pass, and source replacement never creates mixed artifacts.
