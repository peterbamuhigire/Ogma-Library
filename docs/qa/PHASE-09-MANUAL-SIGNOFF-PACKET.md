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
.\scripts\New-Phase09ManualEvidencePackage.ps1
```

Expected result: format passes, Release build has 0 warnings and 0 errors, and
Release tests pass 344 total tests: Core 236, UI 93, Architecture 15. Attach
the generated `docs/qa/evidence/phase09-preflight-*.md` file as the preflight
evidence reference for this packet. The manual evidence package creates a dated
folder with `screenshots`, `audio`, `exports`, and `notes` subfolders plus
per-row note templates for the remaining manual and accessibility rows.

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

Record the dated Narrator, VoiceOver, and keyboard-only results in
`docs/qa/PHASE-09-A11Y-SIGNOFF.md`. That file is the authoritative table for
assistive-technology evidence; this packet intentionally does not duplicate its
pending rows.

## Visual Accessibility Review

Record the dated color, contrast, and pseudolocale visual review in
`docs/qa/PHASE-09-A11Y-SIGNOFF.md`. That file is the authoritative table for
visual accessibility evidence; this packet intentionally does not duplicate its
pending rows.

## Owner Decisions

| Decision | Current proposal | Owner response | Owner/date | Evidence reference |
| --- | --- | --- | --- | --- |
| Premium icon procurement | 22 premium SVG icons listed in `docs/plans/grand-plan/phase-09/icons.md`. | Delivered, copied into runtime asset paths, rendered on reader surfaces, and covered by UI/resource tests | Peter / 2026-05-31 | `docs/plans/grand-plan/phase-09/icons.md`; `docs/plans/grand-plan/phase-09/evidence.md` |
| Annotation layer palette | Amber, sage, clay, plum. | Adopted as the Phase 09 V1 default palette. | Implementation default / 2026-06-01 | `docs/plans/grand-plan/phase-09/icons.md`; `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames`; `IconCatalogPhase09Tests` |
| Citation export V1 scope | Plain-text export; BibTeX/RIS/Markdown deferred unless requested. | Adopted as the Phase 09 V1 export scope. | Implementation default / 2026-06-01 | `CitationService_CaptureAndExport_UsesCatalogueMetadata`; `ReaderView_ExportCitation_WritesClipboardAndExportsCapturedCard`; `src/OgmaLibrary.Application/Catalogue/ISidecarService.cs` |
| Reading-memory disposition wording | UI label currently renders as `Disposition (1-5)`; semantic scale is 1 to 5, where 1 is not useful/did not finish and 5 is transformative. | Adopted as the Phase 09 V1 label and scale. | Implementation default / 2026-06-01 | `src/OgmaLibrary.App/Assets/Strings/annotations.en.resx`; `ReadingMemoryService_Save_UpsertsAndValidatesDisposition`; `ReaderView_Phase09InteractiveControls_AcceptKeyboardFocusAndNames` |

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
| `.\scripts\Test-Phase09EvidenceTooling.ps1` | Passed: preflight temp evidence generation, manual evidence package generation matching the current pending-row count, and signoff evidence-shadow protection |
| `.\scripts\Phase09-Preflight.ps1` | Generates a dated cross-platform `docs/qa/evidence/phase09-preflight-*.md` record with commit, OS, worktree state, app process state, and preflight command output |
| `.\scripts\New-Phase09ManualEvidencePackage.ps1` | Generates a dated reviewer evidence package with per-row note templates and screenshot/audio/export folders for the remaining manual and accessibility rows |
| `docs/qa/evidence/phase09-preflight-20260601-104948.md` | Current preflight evidence for commit `45ad1cb3da3d868b06e3496d0c0af335c7fc2388`: format passed, Release build passed with 0 warnings/errors, Release tests passed Core 236, UI 93, Architecture 15 |
| `docs/qa/evidence/phase09-signoff-gate-20260601-105152.md` | Current signoff-gate report for commit `45ad1cb3da3d868b06e3496d0c0af335c7fc2388`: required files, automated preflight, and CI workflow shape passed; manual packet, accessibility, and remote CI rows remain pending |
| `docs/qa/evidence/phase09-remote-ci-20260601-105205.md` | Current remote-CI collection attempt for commit `45ad1cb3da3d868b06e3496d0c0af335c7fc2388`: GitHub Actions API returned 404, so remote CI remains pending |

## Closure Rule

Phase 09 can be marked fully closed only after every row above has dated
evidence or an explicit owner waiver. Run the signoff gate before changing the
phase status:

```powershell
.\scripts\Test-Phase09Signoff.ps1
```

The gate exits `0` only when preflight evidence is valid and no manual,
owner-decision, accessibility, visual-review, or remote-CI evidence remains
pending. When it exits nonzero, the generated report includes a `Pending Detail
Rows` section listing the exact table rows that still need evidence or waiver.
Preflight and passing remote-CI evidence are accepted for the current commit, or
for an ancestor commit when no verification-impacting files changed after that
evidence run. Verification-impacting files include product source, tests,
workflow files, solution/project files, props/targets, `.editorconfig`,
`global.json`, `NuGet.config`, and `Directory.Build.*` or
`Directory.Packages.*` files. Evidence files used by the gate must also be
tracked by git and clean in `HEAD`; untracked, staged-only, or locally edited
evidence cannot satisfy a release gate.

To collect remote CI evidence when GitHub Actions is readable from the
workstation, run:

```powershell
.\scripts\Get-Phase09RemoteCiEvidence.ps1
```

The signoff gate accepts the latest `docs/qa/evidence/phase09-remote-ci-*.md`
only when it records `Status` as `Pass` for the current commit, or for an
ancestor commit with no verification-impacting changes afterward.
