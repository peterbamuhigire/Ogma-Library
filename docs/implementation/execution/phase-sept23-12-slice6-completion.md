# Sept-23 Phase 12, slice 6 completion record

Status: slice 6 of 8 IMPLEMENTED; Phase 12 stays open.

Scope (READ-013, UX-005): citation exports (BibTeX, RIS, CSL-JSON, Markdown, plus the existing plain text) chosen by a format argument on `ICitationService`; a Copy as / Save as menu on the citation card; a collapsible, resizable side panel (`Reader.SidePanel.Toggle`, F4) whose width is saved with the D-82 debounce; every reader action registered as a `ShellCommand` in group Reader; progress flush and session close on shutdown, bounded by a time limit.

Decisions carried from earlier slices (restated as requested): **D-92** the unlock dialog is an in-app Avalonia dialog; **D-96** split view is capped at 2 panes. Each pane's `ReaderView` keeps its own toolbar, so the AutomationIds inside the two split panes still repeat. Panes share `PageRenderCache`.

New decisions:
- **D-97**: the side-panel shortcut is F4, because Ctrl+B toggles the bookmark.
- **D-98**: two gestures are shared by context, not duplicated. Ctrl+F is `nav.search` in the library and the find bar in the reader; Alt+Left/Right is `nav.back`/`nav.forward` in the library and page history in the reader. The shell already offers a gesture to the open book first, so `reader.find` carries no gesture and `reader.history-*` carry Primary+[ and Primary+] as their own. `Registry_NoDuplicateGestures` passes with this.
- **D-99**: shutdown works on `IReaderSessionService` and `IReadingProgressService`, not on `ReaderViewModel.CloseAsync`. Exit waits on the UI thread and the view model's continuations return to it, so awaiting it there risks deadlock. Budget 1.5 s; failure never blocks exit.
- **D-100**: no CSL-JSON schema is vendored and the build has no network, so the test checks the required keys and value types (`id`, `type`, and the types of `title`, `author[].literal`, `page`, `note`), not the full schema. A schema validation stays open.

Other facts: the Contents tab already existed, so the panel has five tabs (headers go icon-only below 560 px, one row at 320 px via a `UniformGrid`); the toggle id was renamed from `Reader.SidePanelToggle` (no other users found).

Tests added: `CitationFormatterTests` (four golden files, culture independence, CSL-JSON keys, plain text unchanged), `Registry_NoDuplicateGestures`, `EveryReaderToolbarAction_HasShortcutOrAccessKey`, `ReaderCommands_AllHaveEnglishAndFrenchLabels`, `Shutdown_FlushesPendingProgress`, `Shutdown_StopsWaitingWhenTheBudgetIsSpent`, `SidePanel_WidthPersists`. Golden files are rewritten by running the test with `OGMA_UPDATE_GOLDEN=1`; review the diff before committing.

Evidence: see the commit message for the build, format, analyzer and test results of this slice.

NOT_ASSESSED: real-window check of the side panel, resize handle and the citation menus (no desktop lock taken); Arabic RTL and 200 % text for the new panel; macOS Cmd gestures; screen-reader walkthrough; split-view real-window journey; the advanced-testing skill.
