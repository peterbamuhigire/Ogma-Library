# Phase 09 Continuation Audit

Date: 2026-06-01

Scope: Phase 09 closeout evidence through the active-layer and direct-PDF
hardening pass on 2026-06-01.

## Current Position

Phase 09 remains locally implementation-complete for code, automated tests, and
repository documentation. The remaining closure gates are still manual or
owner-gated:

| Gate | Status |
| --- | --- |
| Narrator and VoiceOver walkthrough | Pending manual evidence |
| Color accessibility visual review | Pending manual evidence |
| Pseudolocale visual review | Pending manual evidence |
| Owner confirmations | Pending palette, citation export, and final reading-memory wording decisions |

## Local Evidence Reviewed

| Area | Evidence |
| --- | --- |
| Latest reviewed implementation scope | Bookmark sorting, reading-memory disposition label, active writable layer marker, and direct-PDF open hardening |
| Worktree | Expected to remain clean after this pass except unrelated `docs/developer-guide/images/scan-en.png` |
| CI workflow definition | `.github/workflows/ci.yml` includes Windows and macOS matrix jobs for restore, format, Release build, and Release tests |
| Phase 09 evidence | `docs/plans/grand-plan/phase-09/evidence.md` dated 2026-06-01 with current focused and full-suite local test counts |
| Bookmark sorting | `23abfaa feat: add bookmark panel sorting`; `ReaderViewModel_BookmarkSortOptions_ReorderByPageOrCreationDate` covers page/date sorting |
| Reading-memory disposition label | `29fa18d fix: expose reading memory disposition range`; focused reader UI tests cover the announced `Disposition (1-5)` label and existing validation |
| Active writable layer marker | `ReaderViewModel_ActiveWritableLayerMarker_FollowsFirstVisibleLayer`; `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames` covers the visible and announced active-layer marker |
| Direct PDF open hardening | `DirectPdfOpen_FuzzyMatch_RegistersSelectedPdfAsNewBook`; migration/direct-open focused slice covers missing-table repair and explicit selected-file registration behavior |

## Remote CI Status Check

Remote CI result evidence is not available from this local environment:

| Check | Result |
| --- | --- |
| GitHub connector combined status for prior Phase 09 audit commit | GitHub API returned `404 Not Found` |
| GitHub CLI | `gh` is not installed in this environment |
| Public Actions URL | No usable Actions status page was available from this environment |

The workflow configuration itself is present and documented, but this audit does
not claim a remote CI pass.

## Locally Actionable Findings

The continuation pass found four locally actionable mismatches and fixed
them:

| Finding | Resolution |
| --- | --- |
| Bookmark panel did not expose the planned page/date sort selector. | Added a localized bookmark sort selector with page-number default and creation-date alternate ordering. |
| Reading-memory disposition field did not visibly include the planned 1-5 range in its label. | Updated English/French runtime strings and accessibility tests to announce `Disposition (1-5)`. |
| Layer sidebar did not visibly identify which layer receives new annotations. | Added a localized active writable layer marker and automation label that follows the first visible layer. |
| Direct PDF open could still surface `no such table: BookFiles` in damaged catalogues and weak/fuzzy selected-file matches could update an existing book. | Forced direct-open DI to receive the catalogue migrator, retries repair on SQLite missing-table errors, and registers weak/fuzzy selected PDFs as new books unless there is an exact path/hash match. |

No further locally actionable Phase 09 implementation gaps were found in this
pass.
Do not mark Phase 09 fully closed until the manual and owner-gated rows above
have dated evidence or explicit waivers.
