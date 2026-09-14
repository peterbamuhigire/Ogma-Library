# Phase 19: accessibility, localization and Windows/macOS parity

Status: planned. Owner: accessibility QA lead; reviewers: assistive-technology users and platform engineers.
Dependencies:all relevant surfaces01-18. Estimate:8-13 person-days.
Findings:F03/F05/F12/F22/F26. Requirements:UX and applicable accessibility/localization NFRs.
Skills:D4/D9/D10/E1/E4; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

Keyboard, low-vision and screen-reader users can complete the same library and reading tasks. Shared concepts feel appropriate on Windows and macOS without forcing identical shortcut or window behavior.

## Work slices

1. Build a criterion-by-journey matrix using the repository's WCAG2.2AA target plus native Narrator/VoiceOver semantics. Define applicability for native windows rather than blindly applying web-only breakpoints/CSS.
2. Traverse all real routes with keyboard: toolbar/menu/collection/grid/inspector/reader/search/dialog/privacy/school/3D fallback. Test visible focus, order, topmost Escape, focus restoration and no unreachable hidden controls.
3. Verify name/role/value/state and dynamic announcements with native screen readers. PDF page-image rendering needs a meaningful accessible text/navigation strategy; a named image alone is not accessible reading.
4. Verify Control/Command shortcut conventions, native menus, dialogs, window/fullscreen, OS theme and scaling. Resolve conflicts with text input and OS-reserved shortcuts.
5. Test Light/Dark/high-contrast/reduced-motion where supported, actual font weights and contrast, supported scaling100/150/200%, long strings and pseudo-localization. Cover English/French and other explicitly approved locales; do not invent translation completeness.
6. Run formative sessions with reader/librarian/admin and accessibility participants, fix identified issues in their owning phases, then execute the acceptance sample defined in phase01/master plan.

## Acceptance

- Every required critical journey passes keyboard-only on both platforms; no focus trap/obscured required control or non-dismissable overlay remains.
- Narrator and VoiceOver users identify/control the interface and access document text with the approved PDF accessibility approach; exceptions are explicit release decisions, never silently passed.
- Actual-state contrast, target usability, text scaling, localization and motion meet the baselined criteria.
- Windows/macOS shortcut/menu/dialog/state parity checklist has native evidence for every supported feature.
- Usability sample records task success/assistance/time/errors and failure severity by cohort; >=95% sample success target does not hide a failed critical task or accessibility group.
- No reviewer signs a pass based solely on screenshot existence, string presence or an automation tool's summary.

## Recovery

Feed failures back to owning phases before release. Preserve accessible simpler alternatives where advanced controls/3D cannot provide equivalent use. Roll back visual changes that reduce contrast/focus/text legibility. Re-run affected native criteria after any late shell, reader or packaging change.
