# Codebase and architecture audit

## Current flow

```text
Avalonia reader/catalogue
  -> Application contracts
  -> IsolatedPdfRendererFactory / worker client
  -> pdf-worker subprocess + sandbox-local copy
  -> PDFtoImage/PDFium rendering and PdfPig extraction
  -> page cache/session, assets, OCR, FTS, annotations and AI evidence
```

The shape is directionally correct. The failure is that the arrows are not yet
the only legal entry points.

## Component findings

| Component | Current state | PDF-standard consequence | Priority |
|---|---|---|---:|
| `IPdfRenderer` | Render/text contract with width, height, scale, preview | Omits page box, rotation, annotation/form, color, OCG and capability context | P0 |
| `PdfiumAdapter` | PDFium/PDFtoImage render; PdfPig page/text parse; whole bytes loaded | Good common-file path, but duplicate/lenient parses and missing effective page model | P0 |
| `PdfWorkerClient` | Persistent session, sandbox copy, output verification, Windows limits | Useful containment primitive; macOS/real OS sandbox and per-request cancellation evidence open | P0 |
| `PdfInputBroker` | Path/root/ext/magic/size gate | Not structural PDF validation; TOCTOU/hash snapshot policy incomplete | P0 |
| `PdfTableOfContentsService` | Direct lenient PdfPig path; page-number bookmark subset | Bypasses containment; loses destinations, labels, actions and target coordinates | P0 |
| `MetadataExtractionService` | Direct PdfPig file open | Bypasses worker and makes metadata trust boundary inconsistent | P0 |
| `IsbnDetectionService` | Direct PdfPig file open plus text scanning | Same boundary issue; text evidence should be artifact-backed | P0 |
| `PdfWriteBackService` | PDFsharp mutation with PdfPig verification | Database-first decision is sound, but verification is weak for effective conformance/signatures | P1 |
| `TextLayerService` | In-memory page cache; renderer session reuse | Documentation says sidecar, code is in-memory; version/content key gap | P1 |
| `InDocumentSearchService` | Sequential extraction, substring/word matching | Placeholder scanned result can be semantically wrong; no mapping confidence | P1 |
| `ThumbnailService` | First-page generated JPEG, manifest integration | No embedded thumb preference; no complete failure/variant policy | P1 |
| `ReaderViewModel` | Navigation, zoom, page scroll, cache/prefetch | Strong UX slice; continuous document mode and actual geometry still open | P1 |
| `ReaderView` | Page-only `ScrollViewer`, native wheel behavior | Smooth local scroll is plausible; physical wheel/trackpad evidence still needed | P1 |

## Architectural diagnosis

The app currently has two competing models:

1. a worker-backed renderer/session model intended by the plan; and
2. convenience services that reopen files with PdfPig/PDFsharp directly.

The second model undermines the first by creating multiple parsers, multiple
password/error policies, repeated file reads, inconsistent leniency and a larger
attack surface. A PDF document context should be created once at the approved
boundary and expose typed read-only capabilities to all consumers.

## Required target boundary

Create a `PdfDocumentContext` behind the application contracts containing:

- immutable source identity and content hash/snapshot;
- parser/renderer versions and declared capability profile;
- effective document/page model;
- password state without retaining secrets longer than the session;
- typed feature diagnostics;
- bounded methods for render, text, navigation, metadata, assets and safe
  inspection.

All infrastructure consumers must use this context or a durable artifact made
by it. Architecture tests should reject direct production references to
`PdfDocument.Open`, `Conversion.ToImage`, `PdfSharp.PdfReader.Open` and raw PDF
path access outside the worker/broker adapter set.

## Governance mismatch

The 39-phase plan is stronger than the stale root README and correctly treats
PDF work as untrusted, versioned and evidence-gated. It is weaker than needed
as a PDF programme because it does not yet include a standards capability
matrix, errata/currentness register, page-box contract, annotation/form/action
policy, PDF/UA/PDF/A distinction or direct-bypass enforcement gate. This
12-phase plan closes those planning omissions.
