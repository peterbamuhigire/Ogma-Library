# ADR 0018: Multiple library roots and app-data derived assets

## Status

Accepted on 2026-09-25 under the owner-proxy decisions D-03 and D-04 recorded in
`docs/plans/sept-23-kaizen/EXECUTION-LOG.md` (Sept-23 Kaizen Phase 05).

## Context

The audit found four linked defects (`docs/plans/sept-23-kaizen/03-defect-register.md`):

- **K20.** Covers were generated but never shown: the catalogue resolved cover
  keys against the startup `LibraryRoot`, while the sidecar wrote them under
  whichever root the process started with. The 3D shelf had the same wrong root.
- **K22.** Only one library folder existed. *Choose library folder* relinked it,
  so the previous folder's books were flagged missing, and a flag was never
  cleared. `BookFiles` stored a relative path with no root, so every consumer
  resolved files through the single settings path.
- **K27.** Ogma wrote a hidden `.ogma/` folder of covers and spines into the
  user's library, including read-only, removable and cloud-synchronised folders.
- **K28.** *Open PDF* silently made the PDF's folder the library root.

A `LibraryRoots` table and `ILibraryRootService` (add, relink, enable) already
existed from the Aug-39 canonical identity work (ADR-0016), but the legacy
`BookFiles` table, the scanner and the UI did not use them.

## Options considered

1. **Keep one root; make *Choose folder* replace it.** Rejected: it keeps K22 and
   hides the user's first library every time they add another folder.
2. **Multiple roots, each file resolved through its own root (chosen).**
   `BookFiles.LibraryRootId` names the owning root; a null root means a loose,
   directly opened file stored by absolute path.
3. **Store absolute paths for every file.** Rejected: a moved or re-lettered
   drive would orphan every book; a root-relative path plus a relinkable root
   keeps identity when a whole folder moves.

For derived assets:

1. **Beside the PDFs (`<library>/.ogma/`).** Rejected as the default (K27).
2. **App-data store, one per root (`<DataDirectory>/assets/<rootId>/…`).** This
   was the plan's recommendation. Rejected on review: assets are
   content-addressed by SHA-256, so the same book in two folders would be stored
   twice, and removing a root would orphan shared files.
3. **One content-addressed app-data store (chosen).** The store root is the data
   directory, so the unchanged catalogue key `.ogma/<class>/<shard>/<hash>.jpg`
   resolves to `<DataDirectory>/.ogma/<class>/…`. Existing default installs
   already used exactly this layout, so they need no row changes.

## Decision

- **Roots (D-03).** Users can add, remove, show/hide (enable) and relink library
  folders. Adding a folder never relinks another. Removing a folder is a soft
  removal (`LibraryRoots.RemovedUtc`): its books are hidden, never deleted, and
  adding the same folder again restores them with their progress and notes.
  Relink stays an explicit action for a folder that moved.
- **Per-root identity.** `BookFiles` gains `LibraryRootId` (nullable) and
  `FileValidity`. Every resolver (reader locator, OCR queue, LAN streaming,
  metadata enrichment, write-back target check, missing-file flagging, identity
  tier 1) resolves a file through its own root with `PathGuard`. Scanning one
  folder flags only that folder's files missing, and an unreachable folder is
  marked offline instead of flagging its books.
- **Migration.** EF migration `Sept23Phase05LibraryRootsAndValidity` adds the
  columns and the `FileIssues` table. The legacy single-root path lives in
  `library-settings.json`, not in the database, so the backfill runs at the start
  of the first scan (`LibraryRootPaths.BackfillLegacyRootAsync`): it creates the
  root row for the settings folder and assigns it to every relative row in one
  transaction, and writes a `LibraryRootBackfilled` audit event. The migrator's
  existing verified backup is the rollback point; a rehearsal test migrates a
  copy of a 17-book catalogue, restores the backup side by side and proves every
  book still opens.
- **Assets (D-04).** `IAssetLocator` (Application) owns key-to-path resolution;
  `AssetLocator` and `SidecarService` are rooted at the data directory. The grid,
  list, detail panel and 3D shelf all use it; nothing depends on
  `OGMA_LIBRARY_ROOT`. At startup `LegacyAssetMigrationService` copies each file
  of the derived-asset folders (`covers`, `thumbnails`, `spines`, `ocr`, `text`,
  `embeddings`, `citations`, `export`) from `<root>/.ogma/`, verifies its SHA-256,
  and only then deletes the source; it removes a folder only when it is empty.
  Unknown files, write-back backups and write-back plans are never touched, and a
  conflicting target keeps the source in place. Each migrated root gets a
  `LegacyAssetsMigrated` audit event.
- **Portable mode** (assets beside the PDFs, per root) is deferred, as D-04 records.

## Consequences

### Positive

- Covers resolve the same way in every view, whatever folder a book came from.
- Libraries coexist; adding, hiding or removing a folder affects only its books.
- The user's folders are no longer written to by scanning or asset generation.

### Negative

- `BookFiles` keeps a non-unique `(LibraryRootId, RelativePath)` index: legacy
  catalogues can hold two rows for one path, and merging them is identity work
  owned by Phase 10. Uniqueness is enforced by the scanner instead.
- App-data disk use grows with the library; orphan collection for removed roots
  is left to the existing visual-asset garbage collector and Phase 24.
- Metadata write-back (an explicit, user-confirmed action) still keeps its PDF
  backups in `<root>/.ogma/backups`; moving them is Phase 11's call.

### Affects

ADR-0005 (sidecar asset folder: the default location moves to app data),
ADR-0016 (canonical identity: `LibraryRoots` becomes the live root model),
FR-LIB-001..007, CAT-001, NFR data integrity.
