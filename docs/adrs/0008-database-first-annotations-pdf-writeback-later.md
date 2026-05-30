# ADR-0008: Store Annotations and Metadata Database-First, Write Back to PDF Later

## Status

Accepted

> Ratified in Phase 00 by the project owner, 2026-05-30.

## Date

2026-05-30

## Context

Ogma Library lets users repair metadata, highlight, bookmark, and annotate their PDFs. Two storage targets exist: the application's own catalogue database (ADR-0005) and the source PDF file itself. Writing changes back into the source PDF is attractive for portability across other readers, but it mutates the user's original file, which is the highest-stakes operation in a product whose principle is that "files remain the user's files" and that "metadata is reversible — write-back must create backups, show diffs, and allow restore." PDF write-back also depends on the chosen PDF engine's write path (ADR-0004) and runs against untrusted, sometimes malformed documents (CTRL-OGMA-004), so getting it wrong risks corrupting irreplaceable originals. The vision places PDF write-back of highlight annotations out of MVP scope, with the internal database first and PDF write-back later, and the methodology sets the write-back decision deadline before Phase 3.

## Decision Drivers

- **Protect the user's original files** from any unsafe or premature mutation.
- **Deliver annotations, bookmarks, and metadata repair early** without depending on a write path.
- **Honour the "metadata is reversible" principle:** backup, diff, verify, restore.
- **Sequence the risky write-back capability** after the safe, reversible foundation is proven.
- **Keep write-back off by default** and contained to the validated library root (CTRL-OGMA-011).

## Considered Options

### Option A — Database-first now; optional, guarded PDF write-back later

- **Pros:** annotations, bookmarks, reading state, and metadata repair ship early and reversibly in the catalogue; the original files stay untouched in MVP; write-back is added later as an opt-in, backed by backup, diff, verify, and restore, contained to the validated root; matches the vision and methodology sequencing.
- **Cons:** until write-back ships, annotations live only inside Ogma and are not visible in other PDF readers.

### Option B — Write back to PDF from the start

- **Pros:** annotations are portable to other readers immediately.
- **Cons:** mutates irreplaceable originals before the reversible-edit safety net is built; couples MVP delivery to a high-risk write path against untrusted documents; a write bug can corrupt user files.

### Option C — PDF write-back only, no database annotation store

- **Pros:** single source of truth in the file.
- **Cons:** forfeits fast, reversible, searchable annotation storage; every edit touches the original; offline reading-state and AI history still need a database anyway.

## Decision Outcome

Store annotations, bookmarks, reading state, and metadata changes in the catalogue database first (ADR-0005), leaving source PDFs untouched in the MVP. PDF write-back is a later, opt-in capability, off by default. When enabled, every write-back creates a backup of the original, presents a diff of the intended change, verifies the written file, and supports restore from the backup, satisfying the reversibility principle and NFR-PROD-010. Write-back targets only files inside the validated library root, preserves the original, and records an audit entry (CTRL-OGMA-011). Whether highlight annotations specifically are written back is decided before Phase 3, the annotation-write-back deadline from design-report Section 17, based on the maturity of the PDF engine's write path (ADR-0004).

## Consequences

### Positive

- Users get reversible annotations and metadata repair early with zero risk to their original files.
- The high-risk write path is deferred until backup, diff, verify, and restore are in place and the PDF engine's write capability is proven.

### Negative

- Until write-back ships, Ogma annotations are not visible in third-party PDF readers.
- Write-back, when added, must reconcile database annotations with file-embedded ones to avoid duplication.

### Affects

- ADR-0005 (annotation and metadata records of record); ADR-0004 (PDF write path); CTRL-OGMA-011 and NFR-PROD-010 (write-back safety and reversibility); the Phase 3 write-back decision.
