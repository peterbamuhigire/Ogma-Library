# Phase 08: reader core and an unobstructed reading workspace

Status: planned. Owner: reader engineer; reviewer: native desktop QA and readers.
Dependencies:03/04/07. Estimate:8-13 person-days. Findings:F09/F10/F11/F22.
Requirements:FR-READ-001 through006,009 and012; exact mode/scope resolved in01.
Skills:E1/E4/D1/D2/D4; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

Reading becomes the strongest part of Ogma: the correct page renders, navigation is predictable, search stays within the document when requested, and the interface gives most available space to the page.

## Work slices

1. Trace `ReaderModule`, ReaderSessionService, PageRenderCache, ReaderViewModel and ReaderView. Preserve isolated PDF processing and catalogue identity. Establish book/page/render-generation ownership for async results.
2. Replace multiple competing chrome rows with a focused reader toolbar: book/back, page navigation, zoom, layout, in-book find and study-panel toggle. Collapse library chrome and catalogue inspector while reading; permit explicit reopening where useful.
3. Connect real single-page, two-page and continuous modes to rendering/layout and persisted per-book state. Do not satisfy the requirement by changing a view-model enum alone.
4. Implement/repair full-screen behavior with clear exit, OS-native conventions and Escape that respects topmost dialogs and unsaved edits. Support fit width/page/actual/custom zoom and rotation against real page dimensions.
5. Wire in-document search to text-layer/search services. Provide query, next/previous, match count, highlight and exact page target. Ctrl/Cmd+F in reader opens document find; library search remains explicitly scoped.
6. Replace silent render failure handling with loading/error/retry and safe stale-image policy. A failed new page must never appear under the old bitmap with a new page number. Handle rapid book change, canceled render, cache completion and window close.
7. Complete independent split reading through two sessions. Define active pane, keyboard routing, synchronized option only if explicitly chosen, and per-pane error/close behavior.

## Fixture and failure matrix

Original3-page PDF plus authorized text-heavy, scanned, rotated, mixed-size, landscape, long, malformed and password-protected files. Test wrong password/refusal, unavailable native worker, renderer timeout, corrupt page, cache eviction, file removal, cancellation and rapid navigation. No private source text enters test logs.

## Acceptance

- Page image and page label agree in normal, delayed, failed and superseded renders; injected failures show an actionable state.
- Layout modes visibly differ as specified and restore on reopen; full-screen hides nonessential chrome and exits through visible/keyboard paths.
- Known phrase search finds all expected occurrences and jumps/highlights correctly; scanned/no-text state explains OCR path.
- Zoom/rotation preserve selection geometry and do not show lower-resolution stale completions over newer content.
- Split panes navigate independently, including two locations in the same PDF; closing one does not corrupt the other.
- Native Windows/Mac tests meet canonical latency/memory budgets on reference hardware; source hashes unchanged.

## Recovery

Keep session/state migrations backward compatible, with a backed-up prior state if schema changes. Disable a newly added mode if it loses position or renders incorrect content; ordinary single-page reading must remain safe. Re-audit usable viewport, time to resume and navigation errors before starting broad study-tool polish.
