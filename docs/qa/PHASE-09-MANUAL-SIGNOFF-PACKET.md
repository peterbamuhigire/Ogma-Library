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
| Ogma build or commit | Pending |
| Review date | Pending |

## Preflight

Run these from the repository root before manual review:

```powershell
dotnet format OgmaLibrary.sln --verify-no-changes --no-restore
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test OgmaLibrary.sln --configuration Release --no-build
```

Expected result: format passes, Release build has 0 warnings and 0 errors, and
Release tests pass 289 total tests.

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
| Premium PNG icon procurement | Procure the 22 icons listed in `docs/plans/grand-plan/phase-09/icons.md`. | In progress - owner buying icons |
| Annotation layer palette | Amber, sage, clay, plum. | Pending |
| Citation export V1 scope | Plain-text export; BibTeX/RIS/Markdown deferred unless requested. | Pending |
| Reading-memory disposition wording | 1 to 5 integer scale, where 1 is not useful/did not finish and 5 is transformative. | Pending |

## Closure Rule

Phase 09 can be marked fully closed only after every row above has dated
evidence or an explicit owner waiver.
