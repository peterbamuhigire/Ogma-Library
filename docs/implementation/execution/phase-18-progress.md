# Phase 18 Progress - Ogma Design System and Application Shell

Date: 2026-09-05

## Delivered in this increment

- Added a shared Avalonia control resource dictionary for Buttons, TextBoxes and
  ComboBoxes, including tokenized type sizing and a visible keyboard focus ring.
- Added explicit body/display/monospace font roles and theme-aware focus colors.
- Included the shared controls in the application resource graph so all existing
  views inherit the same interaction baseline.
- Added headless UI proof for resource availability and the 36-pixel button
  target-size baseline.
- Extracted book-detail tab and section headings into English/French resources;
  the pseudo-locale now expands the same keys automatically.
- Extracted the catalogue shell's filter heading, filter watermarks, clear
  action, and result-count copy into the English/French resource surface.
- Added a validated `UserPreferences` contract and atomic app-data persistence
  for light/dark/system theme choice and comfortable/compact density.
- Applied persisted appearance preferences before the ready shell is shown;
  density updates tokenized type and spacing resources without changing the
  catalogue data or PDF files.
- Added a window-level searchable command palette on Ctrl+Shift+P. Commands
  execute typed shell navigation, search, reader, advisor, reading-plan,
  theme, and density actions, and Escape closes the transient surface.
- Extracted the Student Smart Search surface's headings, actions, accessibility
  names, statuses, privacy/cost/quota summaries, citation labels, and grounding
  notice into English/French resources with pseudo-locale coverage.
- Extracted the book-detail tag hint and reading summary formats into
  English/French resources; detail and smart-search view models now notify their
  bindings when the culture changes.
- Extracted Advisor interpreted-intent labels and separators into the shared
  English/French resource surface; pseudo-locale expansion follows the same
  resource path.
- Extracted startup canonical-identity migration progress copy into localized
  resources and retained the progress counters so an in-flight migration can
  update safely when culture changes.
- Extracted book-detail metadata field/provenance formatting, including missing
  values, default catalogue source, and manual-override labels, into localized
  resources without changing the English rendering contract.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase18DesignSystemTests`: 1 passed.
- Current detail localization/design slice: 7 passed; current full solution:
  893 core + 41 architecture + 151 UI = 1,085 passed, 0 failed, 0 skips.
- Isolated Release application build after catalogue-shell localization: passed
  with 0 warnings and 0 errors.
- Focused culture-switch UI proof: 1 passed.
- Phase 18 preference persistence and corrupt/invalid-file recovery proof:
  2 passed; command-palette filtering/execution and compatibility Ctrl+K
  search proof: 2 passed.
- Full solution verification after this increment: 893 core + 41 architecture +
  151 UI = 1,085 passed, 0 failed, 0 skipped.
- Release application build after appearance and palette wiring: passed with
  0 warnings and 0 errors.
- Evidence: `evidence/phase-18-appearance-palette-2026-09-05.md`.
- Focused localization/design slice: 16 passed, 0 failed, 0 skipped.
- Focused Advisor quality/localization slice: 6 passed, 0 failed, 0 skipped.
- Focused startup/design-system localization slice: 6 passed, 0 failed,
  0 skipped.
- Focused detail-format localization slice: 11 passed, 0 failed, 0 skipped.
- Full solution regression after the localization increment: 895 core passed,
  1 core timing-sensitive LAN P95 test failed, and 41 architecture + 155 UI
  tests passed. The failed catalogue-load P95 was 2,558 ms versus 2,000 ms;
  the same test passed in an isolated rerun (1/1).
- Evidence: `evidence/phase-18-classroom-copy-2026-09-05.md`.

## Remaining phase gate

Application-wide hard-coded copy extraction, all-view route inventory,
contrast snapshots, and Narrator/VoiceOver journeys remain before phase 18
closure. Theme/density persistence, command-palette execution, the
  detail-panel/catalogue-shell/Advisor/startup/detail-format copy extraction,
  and the named Student Smart Search copy finding are closed locally; physical
  accessibility and full application coverage remain open.
