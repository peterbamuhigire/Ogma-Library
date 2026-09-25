# Sept-23 Phase 05 completion: library roots, scanning and file validity

Status: **IMPLEMENTED; journey proof pending Phase 01** (2026-09-25). Code, unit, migration,
architecture and real-window checks below pass. G2/G6 and the `Category=Roots|Watcher` E2E
journeys run once the Phase 01 harness lands (see NOT ASSESSED).
Rollback point: `fd613bf` (the last commit before any Phase 05 change) plus the migrator's
verified `catalogue.db.<timestamp>.bak`.
Plan: [phase-05](../../plans/sept-23-kaizen/phases/phase-05-library-roots-scanning-validity.md).
Defects: K20, K21, K22, K27, K28 (scan-state and settings parts); K17 (dead scan logic).
Decision record: [ADR-0018](../../adrs/0018-library-roots-and-derived-assets.md) (D-03, D-04).

## Tasks

| Task | Result | Commit |
|---|---|---|
| T05.1 ADR | ADR-0018: multi-root with enable/remove/relink; one content-addressed app-data asset store; portable mode deferred | `99cfe79` |
| T05.2 Per-root identity | `BookFiles.LibraryRootId` + `FileValidity`, `LibraryRoots.RemovedUtc`, `FileIssues`; EF migration `Sept23Phase05LibraryRootsAndValidity`; transactional backfill of the legacy settings root at scan start (`LibraryRootPaths`); locator, OCR queue, LAN resolver, enrichment, write-back target check, missing flagging and identity tier 1 all resolve through the file's own root | `4e17b97` |
| T05.3 Add, not relink | *Choose library folder* and *Add folder…* call `AddAsync`; relink stays explicit; nested/parent roots rejected; dead `MainWindowViewModel` (duplicate scan logic) removed | `4e17b97`, `4839dfc` |
| T05.4 Asset root (K20) | `IAssetLocator`; grid, list, detail and 3D shelf use the app-data store; `_assetRootPath`/settings precedence removed | `a840bda`, `4839dfc` |
| T05.5 Asset location (K27) | Sidecar rooted at the data directory (`<DataDirectory>/.ogma/…`); `LegacyAssetMigrationService` copies `<root>/.ogma/{covers,thumbnails,spines,ocr,text,embeddings,citations,export}`, verifies SHA-256, then deletes the source; removes only empty Ogma folders; never touches unknown files or write-back backups; audit event per root | `a840bda` |
| T05.6 Validity (K21) | `PdfFileValidityClassifier` (0 bytes → Empty; no `%PDF-` in 1,024 B → NotAPdf; no `%%EOF` in the last 4 KB → Damaged; `/Encrypt` → Locked); invalid files go to `FileIssues`, not `Books`; catalogue projection hides invalid files and hidden/removed roots; Locked books get a badge and never show Indexing/Index failed; the metadata worker marks an encrypted PDF the byte check missed as Locked; Needs attention list with Retry / Show in folder / Ignore | `4e17b97`, `4839dfc` |
| T05.7 Restore | Fast path restores `FileStatus`/`Book.Status` and writes `BookRestored`; offline roots are marked Unavailable instead of flagging books | `4e17b97` |
| T05.8 Freshness | `LibraryMonitorService`: single-flight rescans, startup scan 5 s after first load, debounced (2 s) watcher per enabled root, overflow → incremental scan; *Rescan library* in panel, command palette and F5; `OGMA_LIBRARY_ROOT` is registered and scanned | `a840bda`, `4839dfc` |
| T05.9 Terminal states | `ScanRootsAsync` always ends Completed / CompletedWithIssues / Cancelled / Failed (new `ScanPhase.Failed`), returns a summary, logs `library.scan.finished`; the shell shows cancelled/failed text and "Added · Updated · Needs attention · Missing" | `4e17b97`, `4839dfc` |
| T05.10 Atomic settings | Temp file + flush + `File.Replace` with `.bak`; corrupt file recovered from `.bak` (and rewritten) without throwing. The other two JSON stores already wrote temp-then-move and tolerate `JsonException` | `5324eb5` |
| T05.11 Open PDF | Never sets roots; files inside an enabled root are recorded against it, others are loose books by absolute path; same content from another place becomes a second occurrence of one book; empty/non-PDF rejected with a reason; status bar offers "Add this folder to your library" | `4e17b97`, `4839dfc` |

## Tests (fail before, pass after where the plan asks)

- `Sept23Phase05LibraryRootScanTests` (15): second folder never marks the first missing (K22 fails
  before: relink); same relative path in two roots resolves to two files; remove hides only its
  books and re-add restores identity; disable/enable; rename away and back → Restored 2; offline
  root keeps books; 3 invalid files → Needs attention, locked → book; tracked file that becomes
  empty is hidden, not deleted; legacy backfill (idempotent, audited); cancel/failure/success
  terminal phases; watcher pickup ≤ 10 s; nested roots rejected.
- `FileValidityClassifierTests` (6), `LibrarySettingsAtomicWriteTests` (3; torn file fails
  before), `LegacyAssetMigrationTests` (4; `AssetStore_IsTheDataDirectory_NotTheLibraryRoot`
  fails before), `DirectPdfOpenServiceTests` (+2, 4 updated to the new root rules).
- **Migration rehearsal** `LibraryRootMigrationRehearsalTests`: a 17-book catalogue at the last
  pre-Phase-05 schema is copied, migrated with the production migrator, its verified backup is
  restored side by side (17 books, no `LibraryRootId` column, previous migration applied), and
  after the backfill all 17 books locate to existing files and are listed. Down migration
  round-trips (existing migration tests). The schema freeze gate was deliberately advanced
  (41 → 42 migrations).
- UI (`LibraryFoldersPanelTests`, 5): accessible names at 1280×800 and 1920×1080, add-not-relink,
  cancelled/failed/with-issues status text.

## Real window (Windows 11, Release, isolated data dir, synthetic 17-file corpus)

Driver: a copy of `Invoke-OgmaUia.ps1` scoped to this worktree's exe (never stops other lanes'
processes); the folder dialog was moved TOPMOST to a free screen area before the *Select Folder*
click, after the original offset click landed on another lane's window.

| Check | Before (audit) | After (MEASURED) |
|---|---|---|
| Real covers after choosing the folder | 0/17 (placeholders) | 13/13 renderable books show page covers in the grid; the Locked book keeps its placeholder until Phase 12 unlock (13/14 = 93 % of catalogued books) ([g2](evidence/sept-23-kaizen/phase-05/g2-covers-1280.png)) |
| 0-byte, HTML-as-PDF, truncated | 3 books badged *Indexed* | 0 books; *Needs attention (3)*: Empty file, Not a PDF, Damaged PDF ([g6](evidence/sept-23-kaizen/phase-05/g6-needs-attention-1280.png)) |
| Password-protected PDF | *Indexing* → *Index failed* | Book with *Locked* badge |
| Second folder | relink; first folder's books missing | 2 folders, 14 + 2 = 16 books, 0 *Unavailable* ([two roots](evidence/sept-23-kaizen/phase-05/two-roots-1280.png)) |
| Remove second folder | — | 14 books; first folder unaffected ([after remove](evidence/sept-23-kaizen/phase-05/after-remove-1920.png)) |
| New PDF copied into the folder while running | never appears | scan finished 2.7 s after the copy; 15 books |
| `History` renamed out of the folder and back | never restored | missing 3 → restored 3 (log) |
| Writes into the library | `.ogma/` with 24 JPGs | none: every original file hash unchanged, no `.ogma` in either folder; 12 covers + 12 spines in `<data>/.ogma` |
| Scan log | — | [real-window-scan-log.txt](evidence/sept-23-kaizen/phase-05/real-window-scan-log.txt) (counts only, no paths) |

Typeface: unchanged theme (Public Sans body, Spectral headings); the new panel uses theme tokens only.

## Gates

`dotnet restore --locked-mode` pass; `dotnet format --verify-no-changes` 0 changes; Release build
0 warnings, 0 errors; `Test-Fast.ps1`: Architecture 48/48, core 1,049/1,049, UI 180/180
(1,277 tests, 0 failures); `Test-RequirementAccountability.ps1` pass (101 FRs, 29 NFRs, 32 controls).

## Deviations from the plan

- Asset store is `<DataDirectory>/.ogma/<class>/…` (one content-addressed store), not
  `<DataDirectory>/assets/<rootId>/…`: per-root partitions duplicate shared content and the
  existing default layout needed no row changes (ADR-0018).
- `(LibraryRootId, RelativePath)` is indexed but not unique: legacy catalogues can hold duplicate
  rows; merging is Phase 10's identity work. The scanner enforces one row per root path.
- The backfill runs at scan start, not inside the EF migration, because the legacy root path
  lives in `library-settings.json`, not in the database.
- Structural damage beyond truncation is not probed at discovery (one worker process per file
  would slow large scans); the metadata job's worker open marks encrypted files Locked, and
  deeper parse failures stay job failures (Phase 06/17).
- Invalid files are recorded in a `FileIssues` table rather than as `BookFiles` rows so they never
  create books or jobs; a tracked file that becomes invalid is hidden via `BookFiles.FileValidity`.
- Write-back backups stay in `<root>/.ogma/backups` (explicit, user-confirmed write-back; Phase 11).
- `Views/MainWindow.axaml` is kept: it is the thin host UI tests use; only the dead view model
  with duplicate scan logic was removed.
- Library folders UI lives in the sidebar; Phase 08 moves it into Settings. Folder exclusions
  (`SetExcludedFoldersAsync`) are honoured by every scan but not yet editable in the panel.

## NOT ASSESSED

| Item | Owner | Why |
|---|---|---|
| G2, G6, `Category=Roots`, `Category=Watcher`, cancel-mid-scan and corrupted-settings E2E journeys | Phase 01 harness | Harness not merged; covered here by unit/UI tests and the prototype real-window run |
| Cancel mid-scan in the real window (UI leaves Scanning within 1 s) | Phase 01 | The 17-file scan finishes in ~1 s, too fast to cancel by hand; unit-tested |
| Open PDF loose-book offer in the real window | Phase 01 | Unit/UI-tested only |
| Unplugged removable drive, UNC/network share, OneDrive placeholders | Engineering | No such media on this machine; offline-root behaviour unit-tested |
| Synthetic 2k-book migration | Phase 24 | The 17-book rehearsal ran; scale rehearsal left to performance work |
| macOS FSEvents watcher | Phase 27 | No Mac |
| Portable (in-library) asset mode | Owner (D-04) | Deferred by decision |
