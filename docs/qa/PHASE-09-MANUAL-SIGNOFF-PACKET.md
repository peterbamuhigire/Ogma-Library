# Phase 09 Manual Signoff Packet

Date prepared: 2026-05-31

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
| Ogma build or commit | `3404b0c` or later |
| Review date | Pending |

## Preflight

Run these from the repository root before manual review:

```powershell
dotnet format OgmaLibrary.sln --verify-no-changes --no-restore
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test OgmaLibrary.sln --configuration Release --no-build
```

Expected result: format passes, Release build has 0 warnings and 0 errors, and
Release tests pass 306 total tests: Core 226, UI 65, Architecture 15.

## Manual Reader Walkthrough

Use a PDF with selectable text and at least one rotated page.

| Step | Expected result | Evidence |
| --- | --- | --- |
| Create a highlight on a rotated page, restart, reopen the book. | Highlight appears at the same visual position. | Screenshot before/after restart. |
| Create five bookmarks across different pages, force-close the app, reopen. | All five bookmarks remain and navigate to their pages. | Screenshot of bookmark panel. |
| Add a note, type text, move focus away, wait 1 second, restart. | Note text is preserved and note anchor remains visible. | Screenshot of note editor/anchor. |
| Switch to French locale and open annotation, layer, citation, bookmark, and reading-memory panels. | User-facing Phase 09 labels render in French and accented layer names save correctly. | Screenshot of French panels. |
| Delete a non-default layer containing annotations. | Annotations move to the remaining default layer and remain visible after reload. | Screenshot before/after delete. |
| Select text and press Ctrl+Shift+C, then copy/export citation. | Citation uses selected text, title, author, and page format. | Pasted citation text plus exported sidecar file path. |
| Directly open a writable PDF outside the current library root, let metadata jobs run, then inspect its PDF properties. | The book is registered without changing the library root, metadata/thumbnail jobs are queued for the selected file version, and permitted PDF metadata write-back updates DocInfo for the exact registered file. | Screenshot of catalogue entry, job state, and PDF properties. |
| Select a book in the catalogue and click Enrich in the book-detail panel. | The button runs deterministic no-AI provider lookup, refreshes the detail panel, displays provider-sourced fields with provenance, and reports provider/refresh failures in-panel. | Screenshot of before/after metadata fields and provenance rows. |

## Assistive Technology Walkthrough

Update `docs/qa/PHASE-09-A11Y-SIGNOFF.md` with the dated result.

| Check | Windows Narrator | macOS VoiceOver | Evidence |
| --- | --- | --- | --- |
| Annotation overlay announces type, layer, and page. | Pending | Pending | Audio note or reviewer note. |
| Note anchor announces a note-specific label. | Pending | Pending | Audio note or reviewer note. |
| Bookmark panel announces list count and item labels. | Pending | Pending | Audio note or reviewer note. |
| Layer visibility and delete controls announce action-specific labels. | Pending | Pending | Audio note or reviewer note. |
| Citation card copy/export buttons announce distinct actions. | Pending | Pending | Audio note or reviewer note. |
| Reading-memory fields announce field purpose. | Pending | Pending | Audio note or reviewer note. |
| Keyboard-only walkthrough reaches every Phase 09 control. | Pending | Pending | Reviewer note. |

## Visual Accessibility Review

| Check | Expected result | Evidence |
| --- | --- | --- |
| Highlight colors are not the only signal. | Annotation labels and note anchors remain visible/understandable without relying on color alone. | Reviewer note. |
| Highlight contrast matches automated gate in real rendering. | Highlights remain legible over page content in the target platform renderer. | Screenshot set. |
| Pseudolocale screenshot polish. | `artifacts/screenshots/reader-qps-ploc.png` has no unacceptable clipping or overlapping text. | Reviewer note. |

## Owner Decisions

| Decision | Current proposal | Owner response |
| --- | --- | --- |
| Premium icon procurement | 22 premium SVG icons listed in `docs/plans/grand-plan/phase-09/icons.md`. | Delivered and copied into runtime asset paths |
| Annotation layer palette | Amber, sage, clay, plum. | Pending |
| Citation export V1 scope | Plain-text export; BibTeX/RIS/Markdown deferred unless requested. | Pending |
| Reading-memory disposition wording | 1 to 5 integer scale, where 1 is not useful/did not finish and 5 is transformative. | Pending |

## Automated Evidence Snapshot

Latest recorded Release verification for this signoff packet:

| Command | Expected result |
| --- | --- |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~PdfWriteBackTests\|FullyQualifiedName~Metadata\|FullyQualifiedName~JobManagementTests"` | Passed: 69 direct-PDF, metadata, write-back, and job regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BookDetailViewModelTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~Metadata"` | Passed: 74 selected-book enrichment, metadata, and direct-PDF regression tests |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Core 226, UI 65, Architecture 15 |

## Closure Rule

Phase 09 can be marked fully closed only after every row above has dated
evidence or an explicit owner waiver.
