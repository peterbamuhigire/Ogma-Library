# Phase 05: Library roots, scanning and file validity

## 1. Header

| Field | Value |
|---|---|
| Wave | B: Core library |
| Size | 6–8 engineering days |
| Depends on | Phase 02 (safety net, logging, UI-thread discipline), Phase 03 (visible catalogue) |
| Owner decisions | **D-03** (multiple library folders), **D-04** (where derived assets live) |
| Primary defects | K20 (covers never shown), K21 (invalid files become books), K22 (single root, relink, no rescan), K27 (writes into the user's library), K28 (scan state and settings defects) |
| Requirement IDs | LIB-001..007, CAT-001 (covers), NFR data integrity |

## 2. Why this phase exists

After scanning the audit corpus the user sees brown placeholders instead of covers, although
12 real cover images were generated. The covers were written to `<library>/.ogma/covers/…`, but
`CatalogueViewModel.LoadAsync` (`src/OgmaLibrary.App/ViewModels/Catalogue/CatalogueViewModel.cs:268-276`)
prefers an injected `_assetRootPath` (the startup `LibraryRoot`, which defaults to the app-data
folder, `OgmaRuntimeOptions.cs:19,45`) over the folder the user chose. The 3D shelf has the same
wrong root (K20).

The catalogue also shows a 0-byte file, an HTML file named `.pdf` and a truncated download as
normal books badged *Indexed* (K21). A `%PDF-` signature check exists
(`src/OgmaLibrary.Infrastructure/Pdf/PdfInputBroker.cs:81`), but it runs only later in worker
operations, never at discovery.

There is only one library folder. *Choose library folder* **relinks** the existing root
(`MainShellViewModel.cs:868`, `RelinkAsync`), so every book from the previous folder is flagged
missing by `UnavailableFileFlagService`. The flag is never cleared on a later scan, because
the fast path only updates `LastSeenUtc` (`IngestionOrchestrator.cs:196-203`). New PDFs never
appear, because nothing rescans: no Rescan command, no startup scan, no watcher. Setting
`OGMA_LIBRARY_ROOT` does not trigger a scan either (K22).

Finally, the app silently writes a hidden `.ogma` folder into the user's library (K27). The
scan can stay in "Scanning" forever after a cancel or failure. `library-settings.json` is
written in place (`LibrarySettingsService.cs:121`, `FileMode.Create`), so a torn write breaks
the next *Choose folder*. And *Open PDF* silently makes the PDF's folder the library root when
none is set (`DirectPdfOpenService.cs:100`) (K28).

The backend already has most of what is needed: `ILibraryRootService.AddAsync`,
`SetEnabledAsync` and `RelinkAsync` (`Application/Ingestion/LibraryRootContracts.cs:36-59`),
and canonical occurrence rows that carry `LibraryRootId` (`CanonicalIdentityRows.cs:7,72`).
The legacy `BookFileRow` (`RelativePath` only) and the UI are the gap.

## 3. Objectives and exit criteria

1. **Covers:** in G2, ≥ 95 % of valid corpus books show their real cover in grid, list,
   detail and 3D, and none depend on `OGMA_LIBRARY_ROOT`.
2. **Validity:** 0-byte, non-PDF and truncated files never appear as ordinary books. They are
   listed under *Needs attention* with a reason (Empty file, Not a PDF, Damaged PDF, Password
   required) and an action (Retry, Show in folder, Ignore). Password-protected PDFs are valid
   books, marked *Locked*.
3. **Roots (per D-03, recommended multi-root):** users can add, remove, disable and relink
   folders. Adding a second folder never marks books from the first as missing. Every file row
   resolves through its own root.
4. **Freshness:** a startup incremental scan runs after launch (deferred until the UI is idle),
   a *Rescan library* command exists, and a debounced `FileSystemWatcher` picks up new, renamed
   and deleted PDFs within 10 s while the app is open. A file that reappears is restored to
   *Available*.
5. **Asset location (per D-04, recommended app-data):** derived covers, spines and caches live
   under `<DataDirectory>/assets/<rootId>/…` by default, with a migration for existing `.ogma`
   folders. A "portable library" option keeps them beside the PDFs, with disclosure text.
6. **Scan state:** every scan ends in a terminal state (Completed, CompletedWithIssues,
   Cancelled or Failed), shown truthfully. Cancel works during discovery and during registration.
7. **Settings safety:** `library-settings.json` uses temp-file-plus-atomic-replace, and a
   corrupt file is recovered from the last good copy without crashing.
8. **Open PDF** never changes library roots silently. It opens the file as a *Loose book* (or
   offers "Add this folder to your library").

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\backend-databases\database-reliability\SKILL.md`: migrations, backfill and atomic writes.
- `C:\wamp64\www\chwezi-dev-engine\skills\architecture\system-architecture-design\SKILL.md`: the root and identity ownership decision (record an ADR).
- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\reliability-engineering\SKILL.md`: terminal states and recovery.
- `C:\wamp64\www\chwezi-dev-engine\skills\security\vibe-security-skill\SKILL.md`: path validation for roots, traversal and symlinks.
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`: the Needs-attention list and scan states.
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\click-path-audit\SKILL.md`: the Choose-folder → scan → refresh chain.

## 5. Scope in / scope out

**In:** root model and UI (a *Library folders* panel; Phase 08 later hosts it in Settings),
per-root path resolution, cover and asset root fix, asset location policy and migration,
discovery-time validity checks, needs-attention state, rescan, startup scan, watcher,
missing-status restore, terminal scan states, atomic settings, Open PDF behaviour, and removal
of the dead duplicate scan logic in `MainWindowViewModel`.

**Out:** job retries, pipeline throughput and progress counters (Phase 06); duplicate and
edition UI (Phase 10); OCR of image-only PDFs (Phase 17).

## 6. Work breakdown

**T05.1: ADR and D-03/D-04.** Record the decisions. Recommended: multi-root with per-root
enablement; assets in app data by default. Write
`docs/adrs/ADR-00NN-library-roots-and-derived-assets.md`, including the migration of existing
single-root installs and `.ogma` folders.

**T05.2: Per-root file identity.** Add `LibraryRootId` to `BookFileRow`
(`src/OgmaLibrary.Infrastructure/Catalogue/Entities/BookFileRow.cs`) with an EF migration that
backfills from the current single root. Unique index on `(LibraryRootId, RelativePath)`.
Update `BookFileLocator.LocateCoreAsync` (`Pdf/BookFileLocator.cs:75-80`), the incremental fast
path, `UnavailableFileFlagService` (scoped to the root being scanned), OCR, sidecar and LAN
resolvers to resolve through the file's own root, not `GetLibraryRootAsync()`.
*Acceptance:* the migration test on a copy of the audit DB keeps all 17 books openable.

**T05.3: Choose/add folder semantics.** Replace the relink branch in
`MainShellViewModel.ChooseFolderAsync` (lines 806-920) with `ILibraryRootService.AddAsync`.
Relink stays a separate, explicit action ("This folder moved") on a missing root. Remove the
duplicate scan logic in the dead `ViewModels/MainWindowViewModel.cs` and `Views/MainWindow.axaml`
(K17).
*Acceptance:* E2E adds `lib/Science` and then `lib/History`; both sets stay available.

**T05.4: Asset root fix (K20).** Introduce `IAssetLocator.GetAbsolutePath(rootId, relativeAssetPath)`
in the Application layer, used by `CoverImageView`, `CatalogueViewModel`, `BookDetailViewModel`
and `Shelf3DHostCoordinator`. Remove the `_assetRootPath` precedence from `CatalogueViewModel`
(lines 268-276). The catalogue projection carries the asset's root id, or an absolute URI
resolved by the read model.
*Acceptance:* G2 cover assertion (≥ 95 % real covers) passes without any env var.

**T05.5: Asset location policy (K27, D-04).** Write derived assets under
`<DataDirectory>/assets/<rootId>/{covers,spines,thumbs}/`. A one-time migration moves existing
`<library>/.ogma/*` content (verify hashes before deleting the source) and records it in the
audit log. Portable mode, when enabled per root, keeps the current layout.
*Acceptance:* after a fresh scan, the library folder is byte-identical to before (hash
manifest before and after).

**T05.6: Discovery-time validity (K21).** In discovery and registration
(`PdfDiscoveryService.cs`, `IngestionOrchestrator.ScanAsync` at line 109,
`BookRegistrationService.cs`):
- size 0 → `EmptyFile`;
- first 1,024 bytes lack `%PDF-` → `NotAPdf` (reuse the `PdfInputBroker` check);
- missing `%%EOF` in the last 2 KB, or a structure parse failure in the validation worker →
  `Damaged`;
- encrypted → a valid book with `Locked`.

Store the state in a `FileValidity` column and exclude invalid files from the catalogue
projection. Add a *Needs attention* filter and count badge in the sidebar, localised.
*Acceptance:* G6 passes: 3 invalid files in Needs attention; the locked file is shown as Locked, not Indexing.

**T05.7: Missing-status restore.** When the fast path in `IngestionOrchestrator` (lines 187-208)
sees a tracked file present again, set `FileStatus = Present` and `Book.Status = Active`, and
emit an audit event.
*Acceptance:* E2E renames a folder away and back; the books return to Available after rescan.

**T05.8: Rescan, startup scan, watcher.** Add a `LibraryMonitorService` (hosted): an
incremental scan of enabled roots 5 s after the UI is idle at startup; a debounced (2 s)
`FileSystemWatcher` per root for `*.pdf` create, rename and delete, with overflow falling back to
an incremental scan; and a *Rescan library* command (toolbar/menu, command palette, and per root
in the Library folders panel). Respect exclusions (`SetExcludedFoldersAsync`, LIB-002) and
expose them in the panel.
*Acceptance:* E2E copies a new PDF into a root; it appears within 10 s without user action.

**T05.9: Terminal scan states (K28).** Wrap `ScanAsync` so every exit sets a terminal phase in
`finally` (cancel → Cancelled; exception → Failed with a logged reason). Make Cancel work
during discovery and registration. Show a summary: "Added 12 · Updated 0 · Needs attention 4 ·
Missing 0".
*Acceptance:* unit tests for each path; E2E cancels mid-scan and the UI leaves Scanning within 1 s.

**T05.10: Atomic settings (K28).** In `LibrarySettingsService.SaveLockedAsync`
(`Infrastructure/Ingestion/LibrarySettingsService.cs:119-128`), write to `*.tmp`, flush to
disk, then `File.Replace` with a `.bak`. `LoadLockedAsync` (line 103) catches `JsonException`,
falls back to `.bak`, and logs. The same applies to any other JSON settings writer found.
*Acceptance:* a corrupted-file test (G8 part) recovers without a crash.

**T05.11: Open PDF behaviour (K28).** In `DirectPdfOpenService.RegisterOrUpdateAsync`
(around lines 96-153), never call `SetLibraryRootAsync`. Files outside every root become *Loose
books*, tracked by absolute path and identified by hash. Deduplicate by content hash before
registering. Offer "Add the folder to your library" in the reader info bar.
*Acceptance:* opening the same PDF from two locations yields one book with two occurrences.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| No covers (K20) | Startup root preferred over chosen root | Root-aware asset locator | Covers appear | 0/17 → ≥ 95 % valid books | G2 screenshots | Old installs with `.ogma` | Migration keeps a fallback lookup |
| Junk books (K21) | No validity check at discovery | Signature, EOF and parse checks; Needs attention | Only real books listed | 3 junk "Indexed" → 0 | G6 | False "Damaged" on odd PDFs | Retry and "Treat as valid" override |
| Second folder hides first (K22) | Relink instead of add; path via single root | Multi-root with per-file root id | Libraries coexist | all old books Missing → 0 | E2E two roots | Migration defect | DB backup before migration; restore on failure |
| Stale library (K22) | No rescan or watcher | Startup scan + watcher + command | New files appear | never → ≤ 10 s | E2E | Watcher overflow on huge trees | Fallback to incremental scan |
| Writes into user library (K27) | Assets beside PDFs | App-data assets by default | User folders untouched | `.ogma` created → none | hash manifest | Disk usage in app data | Portable mode per root |

## 8. Test plan

- **Unit:** validity classifier (fixtures for every class); the path resolver per root;
  missing-restore; terminal states; atomic settings write and recovery.
- **Migration:** apply to the audit DB copy and to a synthetic 2k-book DB; verify counts and openability.
- **E2E:** G2 (covers), G6 (invalid files), two-root journey, rename away and back, watcher
  pickup, cancel scan, corrupted settings.
- **Negative:** root on a removable drive that is unplugged (root shows Offline, books not
  deleted); read-only root (no writes attempted); a root path containing a symlink loop; a
  UNC/network path (NOT ASSESSED if unavailable).

## 9. Acceptance commands

```powershell
./scripts/Test-Fast.ps1
dotnet test tests/OgmaLibrary.Tests -c Release --no-build --filter "FullyQualifiedName~LibraryRoot|FullyQualifiedName~FileValidity|FullyQualifiedName~LibrarySettings"
dotnet test tests/OgmaLibrary.Tests.E2E -c Release --no-build --filter "Journey=G2|Journey=G6|Category=Roots|Category=Watcher"
Get-FileHash -Algorithm SHA256 (Get-ChildItem $lib -Recurse -File) | Export-Csv before.csv   # compare after a scan: identical
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| D-03 and D-04 answers | Owner | Block T05.2-T05.5; validity, rescan and settings tasks can proceed |
| Network share and cloud-sync folders (OneDrive placeholders) | Engineering | Record behaviour manually; NOT ASSESSED in CI |
| macOS FSEvents watcher behaviour | Phase 27 | NOT ASSESSED |

## 11. Risks and mitigations

- *Schema migration on real user data.* The migrator already takes a backup; add a verified
  restore rehearsal to the migration test, and refuse to start if the backup fails.
- *Watcher storms on large copies.* Debounce, batch and bound the channel; fall back to a scan.
- *Existing `.ogma` folders may be synced to cloud storage.* Move them only after hash
  verification, and log every move.

## 12. Execution prompt

```
## Prompt 05 - Correct library roots, covers, validity and freshness
You are fixing library-root handling and scanning in Ogma Library in C:\wamp64\www\Ogma-Library.
Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/03-defect-register.md (K17, K20, K21, K22, K27, K28)
5. docs/plans/sept-23-kaizen/phases/phase-05-library-roots-scanning-validity.md
Load skills (read SKILL.md): database-reliability, system-architecture-design, reliability-engineering,
vibe-security-skill, empty-error-and-loading-states, click-path-audit.
Work plan: A. T05.1 ADR with owner decisions D-03/D-04 (serial). B. T05.2 schema + resolver, then T05.3-T05.5.
C. In parallel after B: T05.6-T05.11.
File scope: src/OgmaLibrary.Infrastructure/{Catalogue,Ingestion,Pdf}/**, src/OgmaLibrary.Application/Ingestion/**,
src/OgmaLibrary.App/ViewModels/Catalogue/**, src/OgmaLibrary.App/Views/Catalogue/**, Shelf3D host coordinator,
localization resources, migrations, tests.
Never write into the user's library folder unless portable mode is enabled. Never delete user files.
Acceptance: section 9 commands; G2 and G6 green; migration rehearsal recorded in
docs/implementation/execution/phase-sept23-05-completion.md.
Recovery point: the last Phase 04 commit plus a DB backup.
```
