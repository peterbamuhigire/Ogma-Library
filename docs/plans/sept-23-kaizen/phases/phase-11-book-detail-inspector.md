# Phase 11: Book detail inspector overhaul

## 1. Header

| Field | Value |
|---|---|
| Wave | C, Experience |
| Size | 5–7 engineering days |
| Depends on | Phase 09 (TabStrip/segmented control, button variants, tokens) |
| Owner decisions | None new. The astra finding F14 ("right panel overhaul") records the owner's explicit request. |
| Primary defects | K15 (inspector part), K81, astra F02/F14 (partial) |
| Requirements | CAT-004 (W), META-001 (W), META-003 (W), META-004 (W), META-005 (W), META-006 (B, entry point), READ-010 (P, OCR entry), READ-014 (W) |

## 2. Why this phase exists

The owner explicitly asked for the right panel to be overhauled (astra audit, "Peter explicitly
requested this overhaul"). The Sept-14 tranche changed only width, title and cover (`bbb1ae0`).
The measured state on 25 September (`evidence/screens/j3-detail.png`):

- **Seven tabs** (`File`, `Bibliographic`, `Reading`, `Enrichment`, `AI`, `Contents`,
  `Provenance`; `BookDetailView.axaml:81-524`) render as **display-size headers wrapping onto three
  rows**, consuming a third of the panel before any content.
- **Two *Read* buttons** (one in the header, one in the footer), next to *Enrich* and *Run OCR*
  in three unrelated colours (blue, purple, green). There is no single primary action.
- The first thing shown is the **relative path, byte count and raw SHA-256 hash** (K81), which is
  technical data most readers never need.
- The cover area is a brown placeholder (fixed by Phases 05/09).
- Metadata review, enrichment proposals, write-back consent and provenance all exist and work
  (`BookDetailViewModel.cs:1103-1245, 1481-1554`), but they are spread across tabs without a clear flow.
- *Read* from detail uses fire-and-forget `_ = vm.OpenReaderAsync()` (`BookDetailView.axaml.cs:30`),
  so failures leave an empty reader (journeys J4).

## 3. Objectives and exit criteria

1. **Information hierarchy:** the header shows the cover (or generated cover), title (Spectral,
   Title size), author(s), year, one **primary action** (*Read* or *Continue reading · p. 43*),
   a rating, favourite, reading status, and an overflow menu (*Enrich metadata*, *Run OCR*,
   *Show in folder*, *Add to shelf*, *Write metadata to PDF…*, *Remove from library*).
2. **Sections, not seven tabs:** a compact segmented control with 3 or 4 segments: **Overview**
   (description, subjects, tags, shelves, reading progress, notes count), **Details** (bibliographic
   fields with inline edit and provenance on hover or tap), **Contents** (TOC with page jumps), and
   **Activity** (reading history, processing state, enrichment proposals). The AI entry moves to the
   Advisor (Phase 16). File and technical data (path, size, hash, fingerprint, extraction state,
   quality score) go behind a **Technical details** disclosure in Details.
3. **Metadata review flow:** enrichment proposals appear as a review card ("3 suggested
   changes") with per-field accept or reject, source and confidence, and *Accept all*. Every change
   is undoable (META-003/004).
4. **Write-back flow:** a two-step consent dialog (preview diff → confirm), with backup location
   shown and *Restore original* available (META-005). The copy is plain language.
5. **States:** loading skeleton, file missing (with *Locate file…* relink), password-protected
   (with *Unlock*, Phase 12), processing (live), failed processing (reason plus *Retry*), and no
   metadata (with *Enrich* if providers are enabled; otherwise a hint to enable them in Settings).
6. The panel closes with Esc and the close button, is resizable (320–560 px, persisted), and never
   covers the catalogue when nothing is selected (astra F02, verified in the real window).
7. *Read* awaits the open with error handling; failures show a message in the panel, and the shell
   does not switch to an empty reader.
8. Zero raw hashes or `ex.Message` strings shown outside Technical details (K81).

## 4. Skills to load before starting

- `C:\wamp64\www\design-system-skills\README.md`, `CLAUDE.md`
- `C:\wamp64\www\design-system-skills\governance\design-quality-gate.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\product-design-audit\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\ux-remediation-and-redesign\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\03-layout-grid-and-composition\composition-and-visual-hierarchy\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\form-ux-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\ux-writing-and-microcopy\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\design-ethics-and-anti-dark-patterns\SKILL.md` (consent dialogs)
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`

Typeface: title in Spectral (Title token), everything else in Public Sans; hashes and paths in
JetBrains Mono inside Technical details only.

## 5. Scope

**In scope:** `BookDetailView.axaml` restructure, the view-model command surface (primary-action
logic, awaited open), review and write-back dialogs, states, panel sizing and persistence, and
localisation.

**Out of scope:** enrichment provider behaviour (Phase 06/08), OCR engine (Phase 17), the AI tab
content (moves to Phase 16), and catalogue selection behaviour (Phase 10).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 11.1 | Click-path audit of every detail command (`click-path-audit`): trace each handler to state changes; list silent resets and fire-and-forget calls. | `BookDetailView.axaml.cs`, `BookDetailViewModel.cs` | Findings table in the evidence folder; every fire-and-forget either awaited or documented. |
| 11.2 | Header redesign with a primary-action state machine (Read / Continue reading · page N / Locate file / Unlock) and overflow menu. | `BookDetailView.axaml:1-80`, `BookDetailViewModel.cs:1089` | Headless UI: the primary action matches each state fixture. |
| 11.3 | Replace the 7-tab `TabControl` with the Phase 09 segmented control and 4 sections; move File content into a Technical details `Expander`. | `BookDetailView.axaml:81-524` | Screenshot at 360 px and 520 px widths: the header and segments fit on one row. |
| 11.4 | Inline field editing in Details with provenance popover (`LoadTocAsync`, provenance data already loaded lazily). | `BookDetailViewModel.cs:1288-1363` | Unit: edit → `UpdateMetadataFieldAsync` → provenance shows "Edited by you". |
| 11.5 | Enrichment review card: proposals with accept, reject, *Accept all*, and undo. | `BookDetailViewModel.cs:1481-1554` | Headless: accept all then undo restores the prior values. |
| 11.6 | Write-back two-step dialog with diff, backup location and restore. | `BookDetailViewModel.cs:1103-1245` | Real-window: write back to a synthetic fixture copy; restore returns the original hash. |
| 11.7 | States: skeleton, missing file with relink, password, processing, failed with retry, no metadata. | View plus view model | Each state has a fixture and a screenshot. |
| 11.8 | Await *Read* with error surfacing; remove `_ = vm.OpenReaderAsync()`. | `BookDetailView.axaml.cs:30` | Real-window: opening a deleted file shows an in-panel error; no empty reader. |
| 11.9 | Panel width persistence, Esc and close behaviour, and no panel when nothing is selected. | `CatalogueShellView.axaml:691-694`, preferences | Real-window journey J-DET-1. |
| 11.10 | Localise and name every control; keyboard order header → segments → content. | Localization | UIA dump plus the key parity test. |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Panel overwhelmed by tabs | One tab per backend feature | 4 task-oriented sections | Faster task completion | Header plus tabs height: ~35 % of panel → ≤ 18 % | Screenshot measurement | Features harder to find | Overflow menu plus command palette entries |
| No primary action | Buttons added per phase | State-driven primary action | "Read" found instantly | Primary action candidates: 3 competing → 1 | Owner walkthrough | Hidden secondary actions | Overflow always visible |
| Technical data first (K81) | File tab default | Technical details disclosure | Less intimidating | Hash visible by default: yes → no | Screenshot | Power users lose it | One click to expand, remembered |
| Silent read failures | Fire-and-forget | Awaited open with state | No empty reader | Empty-reader failures in journey: possible → 0 | Harness negative case | None | Revert |

## 8. Test plan

- **Unit:** the primary-action state machine; proposal accept, reject and undo; write-back restore.
- **Headless UI:** section switching; the Expander; accessible names; each state fixture renders.
- **Real-window:** J-DET-1 select → inspect → continue reading; J-DET-2 accept enrichment
  proposals and undo; J-DET-3 write back and restore on a fixture copy; J-DET-4 missing file →
  relink.
- **Negative:** a book deleted on disk while the panel is open; write-back to a read-only file
  (record the Windows ACL result); a very long title and 12 authors (text trimming and wrapping).

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test OgmaLibrary.sln --configuration Release --no-build --filter "Category!=Performance" -m:1
./scripts/Test-DesignTokens.ps1
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Detail -Sizes 1280x800,1920x1080 -Themes Light,Dark
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Owner acceptance of the new inspector | Owner (wave C walkthrough) | The phase stays IN PROGRESS until the verdict is recorded |
| Write-back on macOS permissions | Phase 27 | Windows only |
| Screen-reader pass | Phase 21 | UIA names only |

## 11. Risks and mitigations

- **Losing features the tests rely on.** Existing UI tests bind to tab names. Mitigation: update
  tests alongside; do not delete coverage.
- **Write-back is destructive.** Mitigation: fixtures are copies; backup and verified restore stay mandatory.
- **Scope drift into catalogue work.** Mitigation: the selection model belongs to Phase 10.

## 12. Execution prompt

```text
You are implementing Phase 11 (Book detail inspector overhaul) of the Sept-23 Kaizen plan in
C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md and AGENT_BRIEF.md
3. docs/plans/sept-23-kaizen/phases/phase-11-book-detail-inspector.md
4. docs/plans/sept-23-kaizen/03-defect-register.md rows K15, K81; evidence/screens/j3-detail.png
Load the skills in section 4 (design engine README/CLAUDE.md first). Confirm Phase 09 is COMPLETE.
Start with 11.1 (click-path audit) and save its table. Then 11.2–11.3 (structure), 11.4–11.6
(flows), 11.7–11.9 (states and behaviour), 11.10 (l10n and a11y). After each slice, run the
section 9 commands and capture Light/Dark screenshots at 360 and 520 px panel widths under
docs/implementation/execution/evidence/sept-23-kaizen/phase-11/. Do not remove any capability:
move it into a section or the overflow menu. Record the owner's walkthrough verdict and NOT
ASSESSED items in docs/implementation/execution/phase-sept23-11-completion.md.
```
