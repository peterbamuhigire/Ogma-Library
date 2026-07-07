# Architecture Decision Records — Ogma Library

This directory contains the Architecture Decision Records (ADRs) for Ogma Library.
ADRs are written in [MADR](https://adr.github.io/madr/) style.

## Index

| Number | Title | Status | Date |
|--------|-------|--------|------|
| [0001](0001-target-dotnet-10-lts.md) | Target .NET 10 LTS as the Application Runtime | Accepted | 2026-05-30 |
| [0002](0002-avalonia-cross-platform-shell.md) | Adopt Avalonia as the Cross-Platform Desktop Shell | Accepted | 2026-05-30 |
| [0003](0003-webview-threejs-3d-shelf.md) | Render the 3D Shelf with WebView-Hosted Three.js Behind a Spike Gate | Accepted (spike amendment pending) | 2026-05-30 |
| [0004](0004-pdfium-wrapper-adapter.md) | Render and Extract PDF Content with PDFium Behind an Adapter | Accepted (wrapper amendment pending) | 2026-05-30 |
| [0005](0005-sqlite-catalogue-sidecar-assets.md) | Use a SQLite Catalogue of Record with a Sidecar Asset Folder | Accepted | 2026-05-30 |
| [0006](0006-hybrid-search-metadata-fts5-embeddings.md) | Build Search as Hybrid Metadata, FTS5, and Semantic Embeddings | Accepted | 2026-05-30 |
| [0007](0007-provider-neutral-ai-gateway-privacy-tiers.md) | Route AI Through a Provider-Neutral Gateway with Four Privacy Tiers | Accepted | 2026-05-30 |
| [0008](0008-database-first-annotations-pdf-writeback-later.md) | Store Annotations and Metadata Database-First, Write Back to PDF Later | Accepted | 2026-05-30 |
| [0009](0009-velopack-msix-dmg.md) | Distribute with Velopack for Direct Channels and MSIX for Store and Enterprise | Accepted | 2026-05-30 |
| [0010](0010-optin-library-host-mode.md) | Opt-In Library Host Mode Amends CI-2 for the Classroom Track | Proposed | 2026-05-30 |
| [0011](0011-local-tesseract-ocr.md) | Run OCR Locally with Tesseract, Never Through AI Providers | Accepted | 2026-06-01 |
| [0012](0012-classroom-identity-roles-private-state.md) | Classroom Identity, Roles, and Private State | Proposed | 2026-06-02 |
| [0013](0013-school-managed-ai-host-gateway.md) | School-Managed AI Through the Host Gateway | Accepted | 2026-06-02 |
| [0014](0014-ef-core-10-on-net10-runtime.md) | Align EF Core and Microsoft Extensions Packages to .NET 10 | Accepted | 2026-07-07 |
| [0015](0015-documentation-baseline-v2.md) | Documentation Baseline v2.0 Supersedes the v1.0 Baseline | Accepted | 2026-07-07 |

## MADR Conventions

- **Immutable once Accepted.** An accepted ADR is never edited in place to reverse or substantially alter the decision. A superseding decision is always a new ADR with a higher number; the superseded ADR is updated only to record its new status ("Superseded by ADR-NNNN") and nothing else.
- **Amendment log exceptions.** ADR-0003 and ADR-0004 carry an "Amendment Log" section for recording spike outcomes that were explicitly anticipated at ratification time. These amendments fill a pre-declared placeholder and do not alter the decision itself.
- **File naming is permanent.** Files are named `NNNN-slug.md` with a zero-padded four-digit number. The number is never reused, even if the ADR is later superseded.
- **Status vocabulary:** `Proposed` — drafted and under review; `Accepted` — ratified and binding on the build phases; `Rejected` — considered and declined; `Superseded` — replaced by a later ADR (name the successor); `Deprecated` — no longer relevant (reason noted).
- **Linked cross-references.** The "Affects" section of each ADR names the controls, requirements, and other ADRs it constrains or depends on, so the decision graph can be traversed without full-text search.

## Adding a New ADR

1. Assign the next available four-digit number.
2. Create `NNNN-descriptive-slug.md` in this directory.
3. Follow the MADR template: Title, Status (`Proposed`), Date, Context, Decision Drivers, Considered Options (with pros/cons), Decision Outcome, Consequences (Positive / Negative / Affects).
4. Update this index table.
5. Submit for review; change status to `Accepted` only after the decision is ratified.
