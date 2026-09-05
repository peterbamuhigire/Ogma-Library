# Scope, method and currentness

## Audit question

Does the current application behave as a safe and useful PDF processor against
the portions of PDF 2.0 that a desktop library reader must support, and does
the existing plan contain the controls and evidence needed to make that claim?

## Scope boundary

This audit covers:

- file opening, validation, parsing, encryption and containment;
- page trees, inherited page attributes, geometry and rendering;
- text extraction, copy/search, OCR and page anchors;
- outlines, destinations, labels, links, thumbnails and metadata;
- annotations, forms, signatures, actions and active-content policy;
- caches, prefetch, scrolling, zoom and reader responsiveness;
- ingestion, FTS, embeddings, AI grounding, assets, write-back and portability;
- Windows/macOS accessibility, packaging, release and evidence governance.

It does not claim to certify ISO conformance. Formal certification, legal
licence review, cryptographic validation and complete PDF 2.0 coverage require
specialist review and a controlled corpus.

## Evidence inspected

1. Source tree under `src/`, focused PDF/reader/OCR/search/security tests, and
   package references.
2. `docs/plans/aug-39/`, especially the PDF processing, testing, security,
   risk and data-flow appendices.
3. Phase execution progress and evidence files for phases 10, 11, 16, 21, 23
   and 24, including the documented 1,071-test run.
4. ADR-0004 (PDFium adapter) and ADR-0008 (database-first annotations/writeback).
5. `docs/pdf-standards/pdf-reader-source-extractions-2026-09-04.md`, which
   synthesises the four user-provided books and is treated as a local concept
   source, not as current standards authority.
6. Current official standards and implementation sources listed in the
   [source register](./13-source-and-claim-register.md).

## Method

The analysis follows the research-engine discipline:

- separate normative sources, implementation documentation, repository facts
  and engineering inference;
- trace every material claim to a source, code path or evidence record;
- record what was not tested instead of inferring it from a green mock test;
- distinguish durable book concepts from current dependency/standards facts;
- apply a Kaizen loop: **Observe → Baseline → Select → Experiment → Check →
  Standardise → Teach → Re-measure**.

## Definitions that prevent a plan mismatch

**PDF document conformance** describes the file. **PDF processor conformance**
describes reader/writer behavior for the processor’s supported feature set.
**PDF/UA-2** and **PDF/A-4** are separate related standards with different
purposes: accessibility and preservation/input profiles, not synonyms for a
general-purpose reader implementation.

**Supported** means a feature is implemented, tested against representative
fixtures and included in the published profile. **Degraded** means the page or
document remains safely usable while a feature is lost and the user can see the
limitation. **Refused** means the file/action is blocked with a reason and
recovery path. Silent loss is neither compliance nor acceptable UX.

## Currentness gate

Before each release, update the source register and dependency review. The
local ISO book is a durable reference, but the current PDF Association page
reports ISO 32000-2:2020 with Errata Collection 3 and related technical
specifications. The app must record which errata/revisions the selected engine
and tests cover. Dependency versions must be re-evaluated rather than upgraded
automatically.

## Known evidence limitations

- The current audit was performed from source, plans and available local
  evidence, not from a formal ISO test suite.
- Synthetic PDFs and headless Avalonia tests cannot prove all real PDF feature
  combinations or physical AT behavior.
- Physical Windows/macOS, release signing/notarisation, real OS sandbox escape,
  and licensed/legally governed corpus evidence remain `NOT_ASSESSED` where the
  execution ledger says so.
- The local source books may have uncertain provenance; their extracted ideas
  are cross-checked against current official sources before becoming claims.
