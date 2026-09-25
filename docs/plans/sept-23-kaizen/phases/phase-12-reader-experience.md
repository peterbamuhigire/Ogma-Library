# Phase 12: Reader experience

## 1. Header

| Field | Value |
|---|---|
| Wave | C, Experience |
| Size | 8–12 engineering days |
| Depends on | Phase 04 (stable worker session, async geometry, no UI-thread IPC), Phase 09 (variants, TabStrip, tokens) |
| Owner decisions | None new |
| Primary defects | K34, K33 (visual acceptance), K15 (reader chrome) |
| Requirements | READ-002 (P), READ-003 (W), READ-004 (M), READ-005 (M), READ-006 (B), READ-007 (W), READ-008 (W), READ-009 (B), READ-011 (W), READ-012 (P), READ-013 (P), READ-014 (W), READ-015 (W), UX-005 (P) |

## 2. Why this phase exists

Reading is the product's core activity. After Phase 04 the reader no longer crashes or stalls
(K30, K32). This phase makes it pleasant and complete. The measured state
(`evidence/screens/j4-reader.png`, `sheet2.png`):

- **Chrome crowds the page.** There are three stacked toolbars (*Back to Library*; reader actions;
  page navigation), and **two duplicate sets of page navigation** (`< Previous page / Next page >`
  and `|< < [1] / 120 > >|`). The library sidebar and the catalogue toolbar stay visible, and the
  side panel's four tab headers render at display size. The page gets roughly 55 % of the window.
- **The page is blurry** at 100 % on a 2560×1440 display (K33). `ReaderViewModel` renders at
  `BasePageSurfaceWidth × zoom × PageRenderSupersample (2.0)` (`ReaderViewModel.cs:47,1782`), and
  the low-resolution preview remains visible or is scaled; the raster does not follow `RenderScaling`.
- **Missing requirements:** no spread or continuous layout (READ-004 M; only the enum exists at
  `IReaderSessionReadModel.cs:80-88`), no full-screen or focus mode (READ-005 M), in-document search
  registered but unused (`InDocumentSearchService.SearchAsync`, READ-006 B), navigation history not
  exposed (`NavigationHistory.GoBack/GoForward`, READ-002 P), no password unlock view
  (`PasswordUnlockViewModel` exists, READ-009 B), and plain-text citation export only
  (`CitationService.ExportAsync:83`, READ-013 P).
- **Split view is unusable:** the second pane needs a book ID typed into a `TextBox`
  (`SplitViewView.axaml:34-35`), and both panes share the singleton `ReaderSessionService`
  (`ReaderModule.cs:39-45`), so they cannot hold two documents independently.

## 3. Objectives and exit criteria

1. **Focus layout:** opening a book hides the library sidebar and catalogue toolbar. One slim reader
   toolbar holds back, title, page field `N / total`, zoom menu (fit width, fit page, percentages),
   layout menu, search, annotate tools (collapsed group), and the side-panel toggle. The page area is
   ≥ 85 % of the window at 1280×800. *Full screen* (F11) and Esc to exit (READ-005).
2. **One navigation set:** keyboard (PageUp/PageDown, arrows, Home/End, Ctrl+G go to page,
   Alt+←/→ history), a page field, and optional edge click zones. The duplicate buttons are removed.
3. **Layouts (READ-004):** single page, two-page spread (with a cover-alone option) and continuous
   vertical scroll with virtualised pages. The choice persists per book with a global default in Settings.
4. **Sharp rendering (K33):** the raster width = displayed width × `TopLevel.RenderScaling`
   (capped by `MaxPageRenderWidthPx` and memory budgets). The preview is replaced as soon as the full
   render arrives. Verified by the text-edge sharpness check in the harness (the screenshot text-stroke
   width measured at 100 %, 150 % and 200 % scaling).
5. **In-document search (READ-006):** Ctrl+F opens a find bar; matches are highlighted on the page,
   with next/previous and a count; page-level result list in the side panel.
6. **Navigation history (READ-002):** back and forward after TOC jumps, search jumps and link
   follows; visible buttons plus Alt+←/→.
7. **Password-protected PDFs (READ-009):** an unlock dialog bound to `PasswordUnlockViewModel`,
   with *Remember in system keychain* (existing `WindowsPasswordProvider` /
   `MacOsKeychainPasswordProvider`), and a clear wrong-password message. The synthetic
   `Locked Ledger (password secret).pdf` opens after entering `secret`.
8. **Split view (READ-012):** a book picker (search as you type over the catalogue) for the second
   pane; two independent reader sessions (scoped `IReaderSessionService` per pane); synchronised
   scrolling optional.
9. **Citations (READ-013):** export the captured citation or selection as BibTeX, RIS, CSL-JSON and
   Markdown, with a copy-to-clipboard and save-as option.
10. **Side panel:** the Phase 09 TabStrip with Annotations, Bookmarks, Contents, Layers and Reading
    memory; collapsible; width persisted.
11. **Keyboard map:** a shortcut sheet (`?`) listing every reader shortcut; all actions are reachable
    without a mouse (UX-005).
12. The golden journey "open a 900-page book, read 200 pages, search, annotate, export a citation,
    close and resume" passes in the real window with no stall over 250 ms per page turn (p95).

## 4. Skills to load before starting

- `C:\wamp64\www\design-system-skills\README.md`, `CLAUDE.md`
- `C:\wamp64\www\design-system-skills\governance\design-quality-gate.md`
- `C:\wamp64\www\design-system-skills\skills\03-layout-grid-and-composition\editorial-and-long-form-layout\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\interaction-design-patterns\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\component-states-and-interaction-fidelity\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\08-motion-and-interaction\micro-interactions-and-feedback\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\accessibility-wcag-2-2-compliance\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`

Typeface: reader chrome in Public Sans (Small/Body tokens); the book title in the toolbar in
Spectral at Subtitle size; the page field in tabular figures (Public Sans `tnum`). PDF page content
is rendered as-is.

## 5. Scope

**In scope:** `ReaderView`, `ReaderViewModel`, `SplitViewView`, reader side panel, render resolution
logic, layouts, find bar, history, the password dialog, citation formats, and reader keyboard
handling.

**Out of scope:** worker process lifecycle and IPC (Phase 04), OCR (Phase 17), annotation data model
changes (only presentation), and AI answers in the reader (Phase 16).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 12.1 | Reader chrome restructure: focus layout, one toolbar, remove duplicate navigation; the shell hides sidebar and catalogue toolbar while the reader is active (coordinate with the Phase 07 navigation state). | `Views/Reader/ReaderView.axaml` (908 lines), `CatalogueShellView.axaml:495-516` | Page area ≥ 85 % at 1280×800 (UIA bounds). |
| 12.2 | DPI-correct render sizing: include `TopLevel.RenderScaling`; swap preview for full render; cap by budget. | `ViewModels/Reader/ReaderViewModel.cs:47-50,1782` | Harness sharpness check passes at 100/150/200 %. |
| 12.3 | Layout modes: spread and continuous with a virtualised page host; persist per book. | `IReaderSessionReadModel.cs:80-88`, `ReaderViewModel.cs`, preferences | Real-window: 900-page handbook in continuous mode scrolls with bounded memory. |
| 12.4 | Full-screen and Esc behaviour. | `DesktopShellWindow.axaml.cs` | F11 enters; Esc exits; state restored. |
| 12.5 | Find bar wired to `InDocumentSearchService.SearchAsync`, with highlight overlays and result list. | `Reader/TextLayer/InDocumentSearchService.cs:32`, `ReaderModule.cs:50` | Search "caravan" in *Trade Routes of East Africa* highlights every page match. |
| 12.6 | History buttons plus shortcuts via `NavigationHistory`. | `Reader/Navigation/NavigationHistory.cs:32-88` | TOC jump → back returns to the prior page. |
| 12.7 | Password dialog for `PasswordUnlockViewModel`; keychain opt-in; error mapping (not "file not found"). | `ViewModels/Reader/PasswordUnlockViewModel.cs`, `ShellModule.cs:35` | The locked fixture opens with `secret`; wrong password shows the correct message. |
| 12.8 | Split view: catalogue picker, scoped sessions per pane (factory instead of singleton), independent toolbars. | `SplitViewView.axaml:34-35`, `ReaderModule.cs:39-48`, `SplitViewViewModel.cs` | Two different books open side by side; turning pages in one does not affect the other. |
| 12.9 | Citation formats: BibTeX, RIS, CSL-JSON, Markdown with golden-file tests. | `Reader/Annotations/CitationService.cs:83-103` | Golden files validate (CSL-JSON schema check). |
| 12.10 | Side panel on the Phase 09 TabStrip; collapsible; width persisted. | `ReaderView.axaml` side panel | Screenshot: headers on one row at 320 px. |
| 12.11 | Keyboard map and shortcut sheet; macOS Cmd equivalents (`e7d83ab` groundwork). | `ReaderView.axaml.cs:662-700` | Every toolbar action has a shortcut or an access key; the sheet lists them. |
| 12.12 | Flush the reading position on close and app exit (journeys J4 "nothing calls `Reader.CloseAsync`"). | `ReaderViewModel`, shell closing handler (Phase 02) | Kill-free exit then reopen resumes the exact page. |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Crowded chrome | Features added as toolbars per phase | Focus layout, one toolbar | More reading area, less distraction | Page area share: ~55 % → ≥ 85 % | UIA bounds plus screenshots | Hidden tools | Overflow and shortcuts; sheet `?` |
| Blurry text (K33) | Render width ignores DPI scaling | Scale-aware raster | Sharp text | Sharpness check: fail → pass at 3 scales | Harness | Memory growth | Budget cap |
| Missing layouts, full screen (READ-004/005) | Enum only | Implement modes | Parity with mainstream readers | READ M count: 2 → 0 | Inventory re-run | Virtualisation bugs | Keep single page default |
| Split view needs IDs | Singleton session plus TextBox | Picker plus scoped sessions | Comparison reading possible | Journey success: 0 → 1 | Harness | Two workers' memory | Limit to 2 panes |
| Password PDFs unusable | View never built | Unlock dialog | Locked books readable | Locked fixture opens: no → yes | Harness | Credential leakage | OS keychain only, opt-in |

## 8. Test plan

- **Unit:** layout page mapping (spread pairing, cover-alone); history stack; citation formatters (golden files); render-size calculator with scaling.
- **Headless UI:** toolbar composition; find bar states; password dialog states; side-panel tabs; keyboard shortcuts routed.
- **Real-window:** J-READ-1 the 200-page reading session (p95 page-turn latency recorded); J-READ-2 find and history; J-READ-3 locked fixture; J-READ-4 split view two books; J-READ-5 citation export in all formats; J-READ-6 exit and resume.
- **Negative:** a truncated PDF (error state with *Show in folder*); a password PDF cancelled; a 900-page continuous scroll with rapid scrolling; 200 % Windows scaling.

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test OgmaLibrary.sln --configuration Release --no-build --filter "Category!=Performance" -m:1
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Reader -Sizes 1280x800,1920x1080 -Themes Light,Dark
./tests/OgmaLibrary.Tests.E2E/Invoke-ReaderSoak.ps1 -Pages 200 -Book "Big Reference Handbook"
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Screen-reader access to page text | Phase 21 | Text layer exposure evaluated there |
| macOS keychain unlock flow | Phase 27 | Windows only |
| Reference-machine page-turn budget | Phase 24 | Local measurement only |

## 11. Risks and mitigations

- **Continuous mode memory.** Mitigation: virtualise pages, bound the render cache by bytes, and evict off-screen rasters.
- **Two worker sessions in split view** double resource use. Mitigation: cap panes at 2; share the render cache by content hash.
- **Keyboard conflicts** with the global palette. Mitigation: a single shortcut registry checked by a unit test for duplicates.

## 12. Execution prompt

```text
You are implementing Phase 12 (Reader experience) of the Sept-23 Kaizen plan in
C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md and AGENT_BRIEF.md
3. docs/plans/sept-23-kaizen/phases/phase-12-reader-experience.md
4. docs/plans/sept-23-kaizen/03-defect-register.md rows K30–K34; evidence/screens/j4-reader.png
Confirm Phases 04 and 09 are COMPLETE; if not, stop and report. Load the skills in section 4.
Slices: A) 12.1–12.2 chrome and sharpness; B) 12.3–12.4 layouts and full screen; C) 12.5–12.6 find
and history; D) 12.7 password; E) 12.8 split view; F) 12.9–12.12 citations, side panel, keyboard,
resume. Write tests first for each slice. After each slice run the section 9 commands, including
the soak, and store screenshots plus latency numbers under
docs/implementation/execution/evidence/sept-23-kaizen/phase-12/. Never block the UI thread on
worker IPC (Phase 04 invariant). Record results and NOT ASSESSED items in
docs/implementation/execution/phase-sept23-12-completion.md.
```
