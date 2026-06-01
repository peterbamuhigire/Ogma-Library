# Phase 09 Manual Signoff Packet

Date prepared: 2026-05-31
Last updated: 2026-06-01

Purpose: collect the remaining human evidence required before Phase 09 can be
marked fully closed. Automated implementation, build, format, architecture, UI,
and backend test evidence is tracked in
`docs/plans/grand-plan/phase-09/evidence.md`.

## Reviewer Details

| Field | Value |
| --- | --- |
| Reviewer name | Pending |
| Windows device / OS build | Pending |
| macOS device / OS build | Pending |
| Ogma build or commit | Commit containing this packet or later |
| Review date | Pending |

## Evidence Recording Rules

Use exact dates in `YYYY-MM-DD` format. Every completed manual row must include
the reviewer initials, target OS, result, and a durable evidence reference such
as a screenshot path, exported citation path, audio note path, or short dated
reviewer note. If a check is intentionally waived, record `Waived` plus the
owner initials, waiver date, and reason. Do not replace `Pending` with `Passed`
unless the evidence exists in the repository or in the owner-controlled release
folder named in the row.

## Preflight

Run these from the repository root before manual review:

```powershell
.\scripts\Phase09-Preflight.ps1
```

Expected result: format passes, Release build has 0 warnings and 0 errors, and
Release tests pass 344 total tests: Core 236, UI 93, Architecture 15. Attach
the generated `docs/qa/evidence/phase09-preflight-*.md` file as the preflight
evidence reference for this packet.

## Manual Reader Walkthrough

Use a PDF with selectable text and at least one rotated page.

| Step | Expected result | Result | Reviewer/date | Evidence reference |
| --- | --- | --- | --- | --- |
| Create a highlight on a rotated page, restart, reopen the book. | Highlight appears at the same visual position. | Pending | Pending | Screenshot before/after restart. |
| Create five bookmarks across different pages, force-close the app, reopen. | All five bookmarks remain and navigate to their pages. | Pending | Pending | Screenshot of bookmark panel. |
| Add a note, type text, move focus away, wait 1 second, restart. | Note text is preserved and note anchor remains visible. | Pending | Pending | Screenshot of note editor/anchor. |
| Switch to French locale and open annotation, layer, citation, bookmark, and reading-memory panels. | User-facing Phase 09 labels render in French and accented layer names save correctly. | Pending | Pending | Screenshot of French panels. |
| Delete a non-default layer containing annotations. | Annotations move to the remaining default layer and remain visible after reload. | Pending | Pending | Screenshot before/after delete. |
| Select text and press Ctrl+Shift+C, then copy/export citation. | Citation uses selected text, title, author, and page format. | Pending | Pending | Pasted citation text plus exported sidecar file path. |
| Directly open a writable PDF outside the current library root, let metadata jobs run, then inspect its catalogue entry and PDF properties. | The book is registered without changing the library root, the shell reports metadata extraction/enrichment is queued, metadata/thumbnail jobs are queued for the selected file version, extracted PDF title/author are visible in the catalogue/detail surfaces, and permitted PDF metadata write-back updates DocInfo for the exact registered file. | Pending | Pending | Screenshot of catalogue entry, job state, status text, and PDF properties. |
| Select a book in the catalogue and click Enrich in the book-detail panel. | The button runs deterministic no-AI provider lookup, refreshes the detail panel, displays provider-sourced fields with provenance, and reports provider/refresh failures in-panel. | Pending | Pending | Screenshot of before/after metadata fields and provenance rows. |
| Select a book in the catalogue, edit reading-memory fields in the book-detail Reading tab, and save or move focus away. | Opened-because, key-insight, open-questions, and disposition save through the same reading-memory service and refresh the compact detail summary. | Pending | Pending | Screenshot of edited fields plus refreshed disposition/key-insight summary. |

## Assistive Technology Walkthrough

Update `docs/qa/PHASE-09-A11Y-SIGNOFF.md` with the dated result.

| Check | Windows Narrator | macOS VoiceOver | Reviewer/date | Evidence reference |
| --- | --- | --- | --- | --- |
| Annotation overlay announces type, layer, and page. | Pending | Pending | Pending | Audio note or reviewer note. |
| Note anchor announces a note-specific label. | Pending | Pending | Pending | Audio note or reviewer note. |
| Bookmark panel announces list count and item labels. | Pending | Pending | Pending | Audio note or reviewer note. |
| Layer visibility, active marker, and delete controls announce action-specific labels. | Pending | Pending | Pending | Audio note or reviewer note. |
| Citation card copy/export buttons announce distinct actions. | Pending | Pending | Pending | Audio note or reviewer note. |
| Reading-memory fields announce field purpose. | Pending | Pending | Pending | Reviewer note. Expected disposition label: `Disposition (1-5)`. |
| Keyboard-only walkthrough reaches every Phase 09 control. | Pending | Pending | Pending | Reviewer note. |

## Visual Accessibility Review

| Check | Expected result | Result | Reviewer/date | Evidence reference |
| --- | --- | --- | --- | --- |
| Highlight colors are not the only signal. | Annotation labels and note anchors remain visible/understandable without relying on color alone. | Pending | Pending | Reviewer note. |
| Highlight contrast matches automated gate in real rendering. | Highlights remain legible over page content in the target platform renderer. | Pending | Pending | Screenshot set. |
| Pseudolocale screenshot polish. | `artifacts/screenshots/reader-qps-ploc.png` has no unacceptable clipping or overlapping text. | Pending | Pending | Reviewer note. |

## Owner Decisions

| Decision | Current proposal | Owner response | Owner/date | Evidence reference |
| --- | --- | --- | --- | --- |
| Premium icon procurement | 22 premium SVG icons listed in `docs/plans/grand-plan/phase-09/icons.md`. | Delivered, copied into runtime asset paths, rendered on reader surfaces, and covered by UI/resource tests | Peter / 2026-05-31 | `docs/plans/grand-plan/phase-09/icons.md`; `docs/plans/grand-plan/phase-09/evidence.md` |
| Annotation layer palette | Amber, sage, clay, plum. | Pending | Pending | Pending |
| Citation export V1 scope | Plain-text export; BibTeX/RIS/Markdown deferred unless requested. | Pending | Pending | Pending |
| Reading-memory disposition wording | UI label currently renders as `Disposition (1-5)`; semantic scale is 1 to 5, where 1 is not useful/did not finish and 5 is transformative. | Pending | Pending | Pending |

## Automated Evidence Snapshot

Latest recorded Release verification for this signoff packet:

| Command | Expected result |
| --- | --- |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~PdfWriteBackTests\|FullyQualifiedName~Metadata\|FullyQualifiedName~JobManagementTests"` | Passed: 78 direct-PDF, metadata, write-back, and job regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BookDetailViewModelTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~Metadata"` | Passed: 79 selected-book enrichment, metadata, and direct-PDF regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~Metadata"` | Passed: 76 direct-PDF and metadata regression tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShellReaderNavigationTests"` | Passed: 3 shell reader/direct-PDF/background-refresh navigation tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MigrationTests\|FullyQualifiedName~DirectPdfOpenServiceTests"` | Passed: 14 migration/direct-PDF regression tests including production-DI missing-`BookFiles` direct-open repair |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~IngestionPipelineTests\|FullyQualifiedName~MigrationTests\|FullyQualifiedName~ApplicationStartupTests"` | Passed: 23 direct-PDF, folder-scan, migration repair, and startup tests including production-DI missing-`BookFiles` repair for direct open and Choose Library Folder, plus same-hash unregistered-path folder-scan registration |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Core 236, UI 93, Architecture 15 |
| `.\scripts\Phase09-Preflight.ps1` | Generates a dated `docs/qa/evidence/phase09-preflight-*.md` record with commit, OS, worktree state, app process state, and preflight command output |
| `docs/qa/evidence/phase09-preflight-20260601-072040.md` | Current preflight evidence for commit `f4c09954bdeab37a59cdcc3350560eff133c919b`: format passed, Release build passed with 0 warnings/errors, Release tests passed Core 236, UI 93, Architecture 15 |

## Closure Rule

Phase 09 can be marked fully closed only after every row above has dated
evidence or an explicit owner waiver.
