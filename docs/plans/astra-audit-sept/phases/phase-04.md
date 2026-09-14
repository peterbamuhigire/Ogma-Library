# Phase 04: visual system, typography and icon quality

Status: planned. Owner: design lead/desktop engineer; reviewer: Peter and accessibility QA.
Dependency:03 conceptual hierarchy. Estimate:4-7 person-days. Findings:F05/F29.
Skills:D1/D5 font pairing/D6 icon systems/D4 accessibility and E1; [routes](../06-skills-sources-and-kaizen.md).

## Design brief

Ogma is a scholarly library and reading tool used for long sessions, including school computers. The visual thesis is quiet controls around readable bibliographic content and the PDF page. Retain Public Sans for controls, Spectral for restrained editorial headings and JetBrains Mono for technical IDs unless measured font/render evidence supports a replacement. Verify exact font licenses/files before embedding any alternative.

## Work slices

1. Audit `Themes/Tokens.axaml`, `Controls.axaml`, bundled font metadata and rendered weight resolution. Compare regular/medium/semibold strings at real label sizes across both OSes. Investigate variable-font resolution without assuming it is the cause of faint text.
2. Define named control type sizes/weights/line heights. Use reading titles and metadata hierarchy deliberately; inspector tabs should not inherit giant default headings. Compact density should reduce spacing before shrinking text into illegibility.
3. Define semantic color roles for primary, secondary, danger, attention, success, unavailable and focus. Separate fill contrast from text-on-surface contrast in both themes. Remove accidental warning color from harmless primary actions.
4. Build production component specimens: toolbar button, icon toggle, collection row, cover card, metadata field, tab/section, status message, form validation, dialog and pagination. Include default/hover/focus/pressed/selected/disabled/loading/error states.
5. Replace glyph/emoji UI controls with approved local icons, consistent optical size/stroke/alignment, exact licenses and accessible names. Keep decorative icons out of the accessibility tree.
6. Apply components to the new shell and inspector prototype, using real long titles and representative content. Review before spreading styles across other screens.

## Acceptance

- Actual face and weight resolution is recorded for Windows/macOS; no missing glyphs or unexplained hairline rendering.
- Effective text/control contrast measured by state in Light/Dark; applicable WCAG target met, including focus visibility. Disabled appearance remains understandable.
- Typography stays readable at supported density and OS scaling; no text clipping or control-bound changes that cause overlap.
- Icon manifest covers every new asset; no unlicensed placeholder or platform-dependent emoji navigation remains.
- Peter reviews paired before/after screenshots of shell, inspector and reader with the same content; changes have a task/readability rationale.

## Recovery

Keep token changes separable from control behavior. If a font or palette change reduces legibility or breaks platform fallback, revert that token/component set and preserve the hierarchy work. Record the experiment, not an unsupported claim that a new font is inherently premium. Recheck when phases07/08 introduce dense controls.
