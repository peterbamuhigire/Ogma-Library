# Sept-23 Phase 12, slice 1 completion record

Status: slice 1 of 8 IMPLEMENTED; Phase 12 stays open.

Scope: one slim reader toolbar (back, Spectral title, page field N / total with tnum, zoom menu, layout menu stub, search toggle, one annotate group, more menu, side-panel toggle); the First/Last/Previous/Next buttons removed in favour of keyboard and edge zones; `IsReaderFocusMode` hides the rail, sidebar and catalogue toolbar; K127 fixed (`OpenCatalogue()` in `finally`, close observed); F11 full screen with Esc order palette, sheet, full screen.

Evidence (Release, this worktree):
- `dotnet build OgmaLibrary.sln -c Release --no-restore`: 0 warnings, 0 errors.
- `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore`: clean.
- `scripts/Test-Fast.ps1`: Architecture 52, Unit 1436, Ui 492 passed, 0 failed. E2E is filtered out of the fast run.
- New tests: `Sept23Phase12ReaderFocusTests` (single navigation set, focus mode, back button, fit at 1280 and 1440, accessible names, F11/Esc, Esc layering, K127).

NOT_ASSESSED: real-window runs of G3 and the toolbar at 1280 (no desktop lock taken); Arabic RTL and 200 % text for the new toolbar; keyboard focus inside the annotate flyout (headless cannot show it).

Deviations: see EXECUTION-LOG, Phase 12 slice 1. Decisions D-91 to D-96 are recorded there.
