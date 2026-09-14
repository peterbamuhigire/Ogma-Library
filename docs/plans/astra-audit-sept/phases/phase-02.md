# Phase 02: restore dismissal and overlay control

Status: planned. Owner: desktop engineer; reviewer: UX/QA lead and Peter.
Dependency:01. Estimate:2-4 person-days. Findings:F01/F02/F26.
Requirements: FR-UX-002/003/005, FR-CAT-004; reconcile exact wording in phase01.
Skills: E1 Avalonia, E4 test seams, D1 product audit, D2 remediation; [routes](../06-skills-sources-and-kaizen.md).

## Outcome and hypothesis

The library and PDF page remain unobstructed until a user explicitly opens a panel. A single, correctly scoped visibility owner will remove the persistent palette/inspector defect. Peter's dismissal complaint is the primary acceptance scenario.

## Work slices

1. Add a failing regression at the actual DesktopShellWindow + StartupShellViewModel + MainShellViewModel boundary. Assert palette and inspector hidden after startup before any input. Capture binding errors and visual bounds.
2. Inspect `Views/DesktopShellWindow.axaml` and `Views/Catalogue/CatalogueShellView.axaml:687`. Correct double-MainShell/BookDetail lookup caused by changing DataContext on the same element. Prefer explicit typed binding source/one owner; do not repair this by forcing visibility in code-behind after every event.
3. Treat the command palette as optional. Keep ordinary commands in normal navigation. Open only through an explicit menu/shortcut; add visible Close, Escape, outside click and success dismissal. Do not show on launch, resume or route transition.
4. Restore focus to the invoking control, or a predictable library/reader fallback if it disappeared. Palette query is transient; closing must not reset library query, selection, reading progress or theme.
5. Inspector opens for a selected book only. Close clears visibility, restores space/focus and prevents hidden descendants from remaining in the keyboard path. Test rapid select-close-select and route changes.
6. Review overlapping Escape handlers. Topmost transient UI handles Escape first; a subsequent Escape may dismiss the next layer. Do not exit the application or discard unsaved notes.

## Required tests

Fresh launch; explicit open; Escape from input/item/focused child; visible Close; outside click; command completion; command failure; repeated shortcut; route change; slow book load; inspector close while load is pending. Repeat through pointer, keyboard and native UI Automation on Windows and equivalent native input on Mac.

## Exit evidence

- All prior native failure steps now dismiss the intended surface, with no binding warnings on those paths.
- Palette is absent on100% of20 fresh/restart cycles; this is a deterministic regression loop, not a population estimate.
- Every open/close path restores expected focus and catalogue/reader state.
- Before/after default, narrow and wide screenshots are reviewed by Peter. No overlay hides the PDF after dismissal.
- No unrelated data/schema/feature change in the diff; existing relevant tests pass.

## Recovery

Use a small isolated change that can be reverted without migrating data. If optional palette behavior still fails acceptance, disable its user-facing entry until resolved under an explicit FR-UX-003 scope decision; do not ship an always-visible workaround. Re-measure during phase07 and every composed-window release test.
