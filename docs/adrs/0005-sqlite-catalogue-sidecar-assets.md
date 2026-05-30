# ADR-0005: Use a SQLite Catalogue of Record with a Sidecar Asset Folder

## Status

Accepted

> Ratified in Phase 00 by the project owner, 2026-05-30.

## Date

2026-05-30

## Context

Ogma Library is local-first: the catalogue, reading state, annotations, thumbnails, and indexes live on the user's machine. The product principle holds that "files remain the user's files" and that the database augments the folder rather than replacing it, so the storage design must keep source PDFs in place and never lock content into a proprietary container. The catalogue must hold structured metadata (titles, tags, reading progress, annotations, AI history) and must support full-text search (ADR-0006) and a portable export bundle for data portability (NFR-PROD-009). Derived assets — thumbnails, cover images, extracted text caches, embedding stores — are larger, regenerable, and do not belong in the same store as the structured catalogue of record. The choice is whether to store these large derived assets as binary blobs inside the database or as files in a sidecar folder alongside the database.

This ADR also resolves the Phase 1 thumbnail-storage open question from design-report Section 17: thumbnail storage uses the sidecar folder.

## Decision Drivers

- **Source PDFs stay in the user's folder**, untouched and unlocked.
- **A single, queryable, transactional catalogue of record** for structured metadata.
- **Efficient full-text search** without bloating the catalogue file.
- **Cheap regeneration and storage of large derived assets** (thumbnails, caches, embeddings).
- **A portable export bundle** that moves the library to another machine or tool without loss.

## Considered Options

### Option A — SQLite catalogue of record plus a sidecar asset folder

- **Pros:** SQLite is an embedded, transactional, single-file store ideal for local-first; structured metadata stays compact and fast; large derived assets live as files in a sidecar folder, kept regenerable and out of the catalogue; FTS5 with an external-content table indexes extracted text without duplicating it; the catalogue file plus sidecar folder form a clean portable export bundle.
- **Cons:** two coordinated locations (database and sidecar) must be kept consistent and exported together; orphaned assets must be garbage-collected.

### Option B — SQLite with all assets stored as BLOBs in the database

- **Pros:** one file holds everything, simplest to copy.
- **Cons:** thumbnails, caches, and embeddings bloat the catalogue file and slow backups and queries; regenerable data is mixed with the catalogue of record; vacuuming and write amplification grow with asset volume; the single file becomes large and fragile.

### Option C — A document or embedded NoSQL store

- **Pros:** schema flexibility.
- **Cons:** weaker relational querying and no mature embedded full-text equivalent to FTS5; heavier footprint than SQLite for a local-first desktop app; less portable as a single-file bundle.

## Decision Outcome

Adopt SQLite as the catalogue of record for structured metadata, reading state, annotations, and AI history, paired with a sidecar asset folder holding regenerable derived assets — thumbnails, cover images, extracted-text caches, and embedding stores. Full-text search uses an FTS5 external-content table so the index references the catalogue rows without duplicating their content (ADR-0006). The catalogue database plus the sidecar folder constitute the portable export bundle: exporting copies both, and importing on another machine reconstitutes the library. Source PDFs are never moved into the database; the catalogue augments the user's folder. Optional at-rest encryption applies to the catalogue and to backups per CTRL-OGMA-014 and CTRL-OGMA-015.

## Consequences

### Positive

- Structured catalogue stays compact and fast while large derived assets stay regenerable and out of the catalogue file.
- The catalogue-plus-sidecar pair is a clean, documented portable export bundle satisfying data portability (NFR-PROD-009).

### Negative

- The database and sidecar must be kept consistent and exported together; an asset garbage-collection routine is required.
- At-rest encryption must cover both the catalogue and the sidecar-derived backups, not the catalogue alone.

### Affects

- ADR-0006 (FTS5 external-content index); ADR-0008 (annotations and metadata stored DB-first); CTRL-OGMA-014 and CTRL-OGMA-015 (encryption and backup at rest); NFR-PROD-009 (portability).
