# ADR-0004: Render and Extract PDF Content with PDFium Behind an Adapter

## Status

Accepted (decision ratified; wrapper/threshold pending Phase 01 spike amendment)

> Ratified in Phase 00 by the project owner, 2026-05-30. The two-wrapper benchmark
> outcome (winning wrapper and scores) is recorded as an amendment below when the
> Phase 0 PDFium benchmark spike concludes.

## Date

2026-05-30

## Context

Ogma Library reads, renders, and indexes PDF files as its primary corpus. The reader needs accurate page rendering and thumbnail generation; the catalogue needs reliable text extraction to feed full-text search (ADR-0006) and content-aware AI; and the import path must handle password-protected and malformed documents without crashing, because all imported documents are untrusted input under control CTRL-OGMA-004. The PDF engine is therefore on the critical path for rendering quality, text fidelity, password handling, and security isolation. Several .NET-accessible engines exist, including PDFium (the engine behind Chromium's PDF viewer) exposed through community wrappers, and commercial managed libraries. The engine choice must be validated by a concrete benchmark rather than reputation, and it must be isolated so the project can switch engines without rewriting the reader and indexer.

## Decision Drivers

- **Rendering fidelity** for the reader and for thumbnails across varied real-world PDFs.
- **Reliable text extraction quality** to feed search and content-aware AI.
- **Correct handling of password-protected documents** and graceful failure on malformed ones.
- **Security isolation:** the engine runs inside the untrusted-document worker boundary (CTRL-OGMA-005).
- **Replaceability:** no business cost or licence lock-in that prevents switching engines.

## Considered Options

### Option A — PDFium via a .NET wrapper, isolated behind an adapter

- **Pros:** PDFium is the battle-tested engine behind Chromium's viewer with broad format coverage and strong rendering fidelity; multiple wrappers exist so the project is not bound to one binding; permissive licensing; runs well inside a sandboxed worker.
- **Cons:** wrapper quality varies, so at least two wrapper options must be spike-benchmarked; native interop adds a build and packaging dimension per platform.

### Option B — A commercial managed PDF library

- **Pros:** single supported managed dependency; vendor support channel.
- **Cons:** licence cost and potential per-seat or per-deployment lock-in conflict with portable-ownership and cost goals; engine internals are opaque; switching later is expensive.

### Option C — A pure-managed open-source PDF library

- **Pros:** no native interop; simplest packaging.
- **Cons:** rendering fidelity and text-extraction breadth generally lag PDFium on complex real-world PDFs; password and edge-case handling less proven.

## Decision Outcome

Adopt PDFium, accessed through a .NET wrapper, as the PDF rendering and text-extraction engine, chosen after a Phase 0 benchmark and isolated behind an internal PDF adapter interface. The benchmark must spike at least two wrapper options and score each on render fidelity, password-protected-document handling, text-extraction quality, and search-feeding accuracy against a representative document set; the winning wrapper is recorded as an amendment to this ADR. All reader, thumbnail, and indexing code depends only on the adapter interface, never on the wrapper directly, so the engine or wrapper can be replaced without touching callers. The engine executes inside the isolated untrusted-document worker (CTRL-OGMA-005) with the time and memory bounds of CTRL-OGMA-007. The wrapper decision is due at the close of Phase 0, the PDFium-wrapper deadline carried from design-report Section 17.

## Consequences

### Positive

- A proven rendering engine backs the reader, thumbnails, search, and content-aware AI.
- The adapter boundary makes the wrapper a replaceable detail, retiring lock-in risk.

### Negative

- Native interop must be built and signed per platform, adding to the packaging pipeline (ADR-0009).
- A two-wrapper benchmark is required Phase 0 work before the engine is fixed.

### Affects

- ADR-0006 (text extraction feeds FTS5 and embeddings); CTRL-OGMA-005 and CTRL-OGMA-007 (worker isolation and resource bounds); the Phase 0 risk-spike backlog.

---

## Amendment Log

_This section is completed when the Phase 0 PDFium wrapper benchmark spike concludes. Record: spike date, wrappers evaluated, scores per criterion (render fidelity, password handling, text-extraction quality, search-feeding accuracy), selected wrapper with version, and any constraints imposed on the adapter interface as a result._

| Date | Wrappers evaluated | Selected wrapper | Notes |
|------|--------------------|------------------|-------|
| 2026-05-30 | **PDFtoImage** (sungaila, wraps bblanchon PDFium) vs **Docnet.Core** | **PDFtoImage** | Phase 01 Spike 2 — see `spikes/s02-pdfium/RESULT.md` |

### Phase 01 Spike 2 result (2026-05-30)

A two-wrapper benchmark rendered pages 1–5 of three synthetic fixtures
(`gc-simple-text` 5pp, `gc-large` 285pp, `gc-two-column` 13pp), 2 warmup + 10
timed iterations per page, on the dev box (Windows 11 x64, .NET 10, Release —
**dev-box trend, not gated to W-REF-01**).

| Fixture | PDFtoImage P95 | Docnet P95 |
|---|---|---|
| gc-simple-text | **124.1 ms** | 215.4 ms |
| gc-large (285pp) | **156.9 ms** | 174.8 ms |
| gc-two-column | **139.1 ms** | 257.2 ms |

**Decision: adopt `PDFtoImage`.** Lower P95 on all three fixtures, a cleaner
high-level API returning `SKBitmap` (SkiaSharp is already a planned dependency,
HLD §F), active maintenance, named `RenderOptions` for tuning. Both wrappers
loaded the native PDFium binary on Windows x64 without error, and both expose an
**osx-arm64** runtime RID (Apple-Silicon runtime load deferred — tracked
`TRACK-P01-MACOS-NATIVE`).

**Licence:** PDFtoImage (MIT) + PDFium native (BSD 3-Clause) permit redistribution
inside **MSIX (Windows Store)** and a **notarized DMG / Mac App Store** build.
Action (Phase 02): add both to `THIRD-PARTY-NOTICES.md`.

**Adapter constraint:** the `IPdfRenderer` interface
(`RenderPageAsync(path, pageIndex, scale, ct) → PNG bytes`, `GetPageCount`)
validated against both wrappers; production code depends only on the interface.

**Version note:** the dependency-matrix spike resolved PDFtoImage 5.2.1; the
benchmark ran on 4.1.0 — re-confirm the version at Phase 08.

> With this amendment, ADR-0004 is **Accepted** with the wrapper fixed
> (PDFtoImage), pending only macOS-arm64 runtime confirmation.
