# Phase 21 — Accessibility, Full i18n & Comprehensive QA

One sentence: Achieve WCAG 2.2 AA as a release gate across every core flow,
complete the full Spanish/Italian/German localization tier, and execute the
comprehensive multi-dimensional review and golden-corpus E2E sign-off that
clear the product for store submission.

---

## 1. Status & metadata

| Field | Value |
| --- | --- |
| **Status** | Not started |
| **Tier** | MVP (a11y gate) + Final/V2 (es/it/de localization) |
| **Estimate** | 4 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD Phase 7 (polish/hardening); closes the accessibility and i18n tracks opened in Phase 03 |
| **Platforms** | Windows 10 1903+ (WebView2, UI Automation) + macOS 13 Ventura+ (WKWebView, NSAccessibility); both required |
| **Baseline date** | 2026-05-30 |

---

## 2. Objectives

1. Every core user flow passes WCAG 2.2 AA: keyboard operability (Success
   Criterion 2.1.1/2.1.2), screen-reader name/role/state exposure (4.1.2),
   AA contrast (1.4.3), focus appearance (2.4.11), and no keyboard trap (2.1.2)
   — verified by a combination of automated axe-style checks and manual
   screen-reader passes with NVDA (Windows) and VoiceOver (macOS).
2. Spanish (`es`), Italian (`it`), and German (`de`) are at 100% translation
   coverage with correct plurals, cultural date/number formatting, and a
   native-speaker QA pass for each locale — completing the I18N-STRATEGY.md
   final-language tier.
3. Every colorful icon in the master manifest has a localized accessible label
   in all five locales (en/fr/es/it/de); the `IconCatalog` build check passes.
4. The comprehensive multi-dimensional review (`comprehensive-review:full-review`)
   has been executed across architecture, security, performance, and testing
   dimensions; all critical and high findings are resolved.
5. The golden-corpus E2E suite passes in full across catalogue, reader, search,
   and AI flows on both Windows and macOS.
6. The 9-layer test matrix sign-off is complete: all test layers have been run,
   results recorded, and no open R1 or R2 defect remains.
7. The color-blind and icon-label verification audit confirms that no information
   is conveyed by color alone: every status (health, AI tier, file availability,
   reading progress) has a text label or icon-plus-label pairing accessible to
   colorblind users and screen-reader users.

---

## 3. Scope

### In scope

- **WCAG 2.2 AA accessibility audit — automated:**
  Integrate an axe-compatible accessibility testing library (e.g., Deque axe-core
  via WebView for the 3D shelf surface; a custom Avalonia UI-tree walker for
  native Avalonia controls) that runs in CI on every UI page; gate failures
  block the build. Covers: name/role/value on all interactive controls (buttons,
  menus, lists, dialogs, checkboxes, sliders); contrast ratio ≥ 4.5:1 for
  normal text / 3:1 for large text and UI components; no missing accessible name.

- **WCAG 2.2 AA accessibility audit — manual screen-reader passes:**
  Six core flows, each verified manually on NVDA + Windows and VoiceOver + macOS:
  1. Library scanning and catalogue browse (grid + list views).
  2. Book detail and metadata edit.
  3. PDF reader (open, page turn, zoom, search, bookmarks, annotations).
  4. Search (metadata + full-text + AI-assisted).
  5. Settings (Privacy Center, telemetry consent, AI provider setup).
  6. 3D bookshelf (accessible fallback verified; 3D itself is not SR-navigable
     but the grid/list fallback is fully SR-accessible).

- **Keyboard operability verification:** Tab order, focus ring, keyboard
  shortcuts for all primary actions; no keyboard trap in any modal or panel;
  Escape closes overlays; arrow keys navigate lists and 3D fallback.

- **Focus appearance (SC 2.4.11):** Focus indicator is at least 2 px and
  has ≥3:1 contrast against adjacent colors in both light and dark themes.

- **Color-blind/icon-label verification audit:** systematic review of every
  color-coded status surface (health badges, AI tier chips, availability flags,
  reading-progress rings) to confirm a text/icon label carries the same meaning
  independently of color (deuteranopia, protanopia, tritanopia simulation).

- **Full es/it/de localization:**
  - Extract 100% of `en` resource keys into `es`, `it`, `de` `.resx` files.
  - Correct ICU-style plural rules for each locale.
  - Culture-aware date/number/unit formatting verified by automated locale tests.
  - Native-speaker QA pass for each of the three locales (three separate
    reviewers; findings recorded and resolved before phase gate).
  - Translation memory / glossary updated for all three locales.
  - All five locales pass the pseudolocale truncation/overflow check.

- **Icon accessible label verification (all 5 locales):** the `IconCatalog`
  build check is extended to assert that every icon key has a non-empty label
  resource in all five locales; any missing label fails the build.

- **Comprehensive multi-dimensional review (`comprehensive-review:full-review`):**
  Covers architecture integrity (bounded-context discipline, dependency-graph
  correctness), security (STRIDE residual risks from Phase 19, CTRL-OGMA
  compliance), performance (NFR-OGMA/NFR-PROD gate status from Phase 20),
  and testing (9-layer matrix completeness). All Critical and High findings are
  resolved before Phase 22 begins.

- **Golden-corpus E2E sign-off:**
  All 11 golden-corpus documents exercised through the complete read path on both
  platforms:
  - Catalogue scan → metadata enrichment → display in all four views.
  - Reader: open, navigate, highlight, annotate, resume.
  - Search: metadata, FTS5, semantic, AI-assisted (where AI is enabled).
  - Export: annotation export (citation card).
  Full result recorded in `docs/qa/GOLDEN-CORPUS-SIGNOFF.md`.

- **9-layer test matrix sign-off:** each of the 9 test layers documented in
  SOURCE-SUMMARY.md has a sign-off entry in `docs/qa/TEST-MATRIX-SIGNOFF.md`
  with: test count, pass/fail count, any open tagged gaps, and owner or lead
  sign-off for that layer.

- **Store readiness pre-check (a11y + i18n):** App Store Review Guidelines
  checklist for macOS App Store and Windows Store accessibility/localization
  requirements (as a preparation step for Phase 22); findings actioned here.

### Explicitly out of scope

- New functional features (no FR additions in this phase).
- RTL layout (LTR-only languages in scope; RTL-readiness is a design-token
  property confirmed in Phase 03, not new work here).
- Cloud / online localization services (translations are handled offline
  by native-speaker reviewers and committed to the repo).
- Performance benchmark changes (owned by Phase 20; this phase only confirms
  gates remain green after accessibility and i18n changes).
- Phase 22 store submission artifacts (screenshots, store listing copy,
  notarization) — those are Phase 22 deliverables, though the localized
  store listing copy is drafted here.

---

## 4. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| NFR-PROD-007 | MVP | Keyboard operability of all core flows | Automated axe check + manual keyboard walkthrough; 6-flow verification record |
| NFR-PROD-008 | MVP | Screen-reader + AA contrast | Manual NVDA/VoiceOver pass on 6 flows; axe automated contrast check in CI |
| NFR-OGMA-* (all) | MVP/V1 | All NFR-OGMA budgets remain green after i18n/a11y changes | Phase 20 benchmark suite re-run; must not regress |
| I18N-es | Final | Spanish 100% coverage + native QA | `es` completeness check = 100%; native-speaker sign-off |
| I18N-it | Final | Italian 100% coverage + native QA | `it` completeness check = 100%; native-speaker sign-off |
| I18N-de | Final | German 100% coverage + native QA | `de` completeness check = 100%; native-speaker sign-off |
| I18N-ICON | Final | All icons have localized labels in all 5 locales | `IconCatalog` build check passes with 5-locale assertion |
| WCAG-2.2-AA | MVP | Full WCAG 2.2 AA on all core flows | Automated axe CI gate + manual SR record in `docs/qa/A11Y-SIGNOFF.md` |
| SC-2.4.11 | MVP | Focus appearance ≥2 px, ≥3:1 contrast | Visual design review + automated contrast check on focus rings |
| COLOR-BLIND | MVP | No info conveyed by color alone | Color-blind audit record; each status has text/icon label |
| G1-G8 | All | All 8 public-beta gates green | `docs/qa/BETA-GATES-STATUS.md` showing all green |
| comprehensive-review | MVP | Multi-dimensional review; all Critical/High resolved | `comprehensive-review:full-review` report committed |
| GOLDEN-CORPUS-E2E | MVP | Full golden-corpus E2E pass on Win + macOS | `docs/qa/GOLDEN-CORPUS-SIGNOFF.md` |
| TEST-MATRIX | MVP | 9-layer test matrix signed off | `docs/qa/TEST-MATRIX-SIGNOFF.md` |

---

## 5. Dependencies

### Depends on

- **Phase 03**: i18n scaffold (en/fr) and accessibility scaffold; the
  design-token system and focus-ring tokens; pseudolocale CI check.
- **Phase 06, 08, 10, 12, 14**: all UI surfaces must be implemented and stable
  before the screen-reader and keyboard passes can be executed.
- **Phase 19**: Security hardening and DPIA — the comprehensive review covers
  residual security findings from Phase 19.
- **Phase 20**: All performance gates must be green; this phase re-runs the
  benchmark suite to confirm i18n/a11y changes did not introduce regressions.
- **Native-speaker reviewers**: three external reviewers (es, it, de) must be
  available during this phase window.

### Unblocks

- **Phase 22**: Store submission requires WCAG 2.2 AA sign-off, all 5 locales
  complete, and the comprehensive review resolved.
- **Phase 23**: Beta readiness gates G1-G8 include the a11y and i18n gates
  confirmed here.

---

## 6. Architecture & approach

### Bounded contexts touched

- **All UI contexts** (Library Catalogue, Reader, Search Index, AI Advisor,
  Bookshelf Presentation, Settings & Security): accessibility attributes are
  added across all Avalonia views and view-models.
- **Domain / Application**: no logic changes. Accessibility is purely a
  presentation-layer concern except for the localized label resource additions.
- **Packaging & Updates**: the `IconCatalog` registry is updated to enforce
  5-locale label completeness.

### Accessibility implementation approach

Avalonia maps to platform accessibility trees:
- **Windows**: UI Automation (UIA) tree; Avalonia's `AutomationPeer` classes
  expose `ControlType`, `Name`, `IsEnabled`, and live-region patterns. NVDA
  consumes the UIA tree.
- **macOS**: NSAccessibility tree; Avalonia's macOS backend exposes
  `NSAccessibilityElement` roles and descriptions. VoiceOver consumes this tree.

For each interactive control added in prior phases, this phase:
1. Confirms the `AutomationProperties.Name` (or `aria-label` equivalent) is set
   via an Avalonia `AutomationProperties.Name` attached property, populated from
   the localized icon label resource key.
2. Confirms the control's `Role` is correct (e.g., `Button`, `ListItem`,
   `CheckBox`) — set implicitly by control type or overridden via
   `AutomationProperties.ControlType`.
3. For live regions (search results, health-dashboard updates, AI result cards):
   `AutomationProperties.LiveSetting = Polite` is set so screen readers announce
   updates without interrupting the user.

### 3D bookshelf accessibility

The 3D shelf is not navigable by screen reader (WebGL cannot expose semantic
elements to the UIA/NSAccessibility tree). The accessible contract is:
- When `prefers-reduced-motion` is active or when the platform accessibility
  setting is "reduce motion," the 3D view is replaced by the grid view
  automatically.
- The 3D shelf toolbar contains a "Switch to Grid" button that is always
  keyboard-reachable and screen-reader announced.
- No book or action is reachable *only* through the 3D shelf (per principle 5).

### i18n finalization approach

- **Phase 21 starts with a completeness audit:** run the
  `ResourceCompleteness` CI check for `es`, `it`, `de`; output a list of
  all missing keys. This list becomes the translation backlog.
- **Translation workflow:** missing keys exported to a spreadsheet with
  context screenshots and `en` reference text. Native-speaker translators
  fill in translations. Translations reviewed against the glossary. Keys
  committed to `.resx` files. CI check confirms 100% coverage.
- **Plural rule tests:** automated test per locale asserting that `book_count`
  format string produces correct output for counts 0, 1, 2, 5, 11, 21 in each
  locale (German/Italian/Spanish plural rules differ from English).
- **Format tests:** date format, number format, file-size format in each locale
  verified by unit tests parameterized over `CultureInfo`.

### Comprehensive review approach

The `comprehensive-review:full-review` skill is invoked once to cover:
- Architecture: bounded-context dependency graph check; ADR compliance review.
- Security: CTRL-OGMA-001..025 checklist; residual STRIDE findings.
- Performance: NFR-OGMA/NFR-PROD status table; benchmark regression status.
- Testing: 9-layer matrix completeness; coverage gaps tagged; R1/R2 open defects.

Findings are classified: Critical (must resolve before Phase 22), High (must
resolve before Phase 22), Medium (tracked items, may defer), Low (future work).
The review report is committed to `docs/qa/COMPREHENSIVE-REVIEW-P21.md`.

### Cross-platform approach

- NVDA 2024.x on Windows; VoiceOver on macOS 13+.
- Both platforms require separate manual SR passes because UI Automation and
  NSAccessibility expose different tree structures for the same Avalonia controls.
- Axe-style automated checks run in CI on both platforms.
- The `es/it/de` locale tests run on both CI runners.

---

## 7. Work breakdown (summary)

| WP | Work package | Estimate |
| --- | --- | --- |
| P21-WP1 | A11y automated tooling: axe-compatible CI check for Avalonia UI tree; contrast-ratio check; integrate into CI | 2 d |
| P21-WP2 | A11y remediation: fix all automated-check findings across all UI surfaces; `AutomationProperties.Name/Role/LiveSetting` additions | 3 d |
| P21-WP3 | Manual SR passes: 6 flows × 2 platforms (NVDA + VoiceOver); record findings; remediate | 3 d |
| P21-WP4 | Focus appearance and keyboard trap audit; fix findings | 1 d |
| P21-WP5 | Color-blind audit: simulate deuteranopia/protanopia/tritanopia across all status surfaces; fix any color-only carriers | 1 d |
| P21-WP6 | es/it/de translation: completeness audit → translation → native-speaker QA → commit | 4 d |
| P21-WP7 | es/it/de locale tests: plural rules, date/number format, pseudolocale truncation | 1 d |
| P21-WP8 | Icon label completeness: extend `IconCatalog` check to all 5 locales; fill all missing labels | 1 d |
| P21-WP9 | Comprehensive multi-dimensional review (`comprehensive-review:full-review`); resolve Critical/High findings | 3 d |
| P21-WP10 | Golden-corpus E2E sign-off: all 11 documents × both platforms; record in `GOLDEN-CORPUS-SIGNOFF.md` | 2 d |
| P21-WP11 | 9-layer test matrix sign-off; beta-gate status (G1-G8); `TEST-MATRIX-SIGNOFF.md` | 1 d |

Detail in `tasks.md`.

---

## 8. Cross-cutting checklist

- [x] **Colorful icons + manifest:** `icons.md` is a verification manifest
  for this phase — confirming every existing icon has localized labels in all
  5 locales and that the icon-label CI check passes. No new icons are procured
  in this phase (all icons from Phases 03–20 are already in the master manifest).
- [x] **i18n (en/fr strings externalized):** `en` + `fr` have been complete
  since Phase 03; this phase finalizes `es`, `it`, `de`. All five locales
  reach 100% coverage. Pseudolocale check passes for all.
- [x] **Accessibility (keyboard + SR):** This phase is the primary a11y gate.
  WCAG 2.2 AA is the exit criterion. Manual NVDA + VoiceOver passes on 6 flows.
- [x] **Privacy/egress:** The comprehensive review covers CTRL-OGMA-001..025
  compliance and residual STRIDE findings. No new egress paths introduced.
- [x] **Reversibility:** The comprehensive review includes a check of all R1
  paths; no regression allowed.
- [x] **Performance budgets:** The Phase 20 benchmark suite is re-run after
  all a11y/i18n changes; any regression is resolved before the phase closes.
- [x] **Bounded-context tests:** Architecture tests run as part of the
  comprehensive review; any violation is a Critical finding.
- [x] **Documentation:** `docs/qa/` is populated with all sign-off documents;
  `CHANGELOG.md` updated; `CLAUDE.md` refreshed.

---

## 9. Definition of Done

### Global DoD (Phase 21 slice)

- [ ] WCAG 2.2 AA automated check (axe-style) passes in CI on both platforms;
  zero automated violations.
- [ ] Manual screen-reader passes (NVDA + VoiceOver) on 6 core flows recorded
  in `docs/qa/A11Y-SIGNOFF.md`; zero unresolved findings.
- [ ] Focus appearance (SC 2.4.11) passes: all focus rings ≥2 px, ≥3:1 contrast.
- [ ] No keyboard trap in any modal, dialog, or panel.
- [ ] Color-blind audit complete: no information conveyed by color alone in any
  status surface.
- [ ] `es`, `it`, `de` at 100% translation coverage; native-speaker sign-off
  for each; plural rules correct; format tests pass.
- [ ] `IconCatalog` build check passes with 5-locale label assertion; zero
  missing labels.
- [ ] Comprehensive review report committed; all Critical and High findings
  resolved.
- [ ] Golden-corpus E2E: all 11 documents pass on both Windows + macOS;
  `docs/qa/GOLDEN-CORPUS-SIGNOFF.md` committed with owner countersignature.
- [ ] 9-layer test matrix sign-off: `docs/qa/TEST-MATRIX-SIGNOFF.md`
  committed; no open R1 or R2 defect.
- [ ] All 8 public-beta gates (G1-G8) are green; status recorded in
  `docs/qa/BETA-GATES-STATUS.md`.
- [ ] Phase 20 benchmark suite re-run; no regression from a11y/i18n changes.
- [ ] `dotnet format --verify-no-changes`, `dotnet build` (warnings = errors),
  `dotnet test`, architecture tests all pass on both CI runners.
- [ ] `/code-review` and `comprehensive-review:full-review` completed;
  all Critical/High findings resolved.

### Phase-specific exit criteria

- The `ResourceCompleteness` CI check reports 100% for all 5 locales.
- The `IconCatalog` build check is extended to 5-locale assertion and passes.
- The store-readiness pre-check for a11y and localization (Mac App Store +
  Windows Store guidelines) produces zero blockers for Phase 22.
- Owner countersignature on `docs/qa/GOLDEN-CORPUS-SIGNOFF.md`.

---

## 10. Skills to use

See `skills.md` for full invocation guidance. Summary:

- `frontend-ux:ux-principles-101` — WCAG 2.2 AA mapping to Avalonia controls.
- `frontend-ux:design-audit` — icon coherence and color-blind simulation audit.
- `full-stack-orchestration:test-automator` — axe-style CI check integration.
- `sdlc-meta:e2e-testing` — golden-corpus E2E harness execution.
- `sdlc-meta:sdlc-testing` — 9-layer test matrix sign-off structure.
- `comprehensive-review:full-review` — multi-dimensional review.
- `document-skills:webapp-testing` — test result documentation.
- `ux-content-strategy` + `content-writing` — native-speaker QA coordination
  for `es/it/de` translations.
- `superpowers:verification-before-completion` — no phase close without
  running the full suite on both platforms.

---

## 11. Deliverables

| Artifact | Location |
| --- | --- |
| Axe-compatible a11y CI check | `src/OgmaLibrary.Tests.Accessibility/` |
| A11y sign-off document | `docs/qa/A11Y-SIGNOFF.md` |
| Color-blind audit report | `docs/qa/COLOR-BLIND-AUDIT.md` |
| `es` resource files | `src/OgmaLibrary.App/Localization/es/*.resx` |
| `it` resource files | `src/OgmaLibrary.App/Localization/it/*.resx` |
| `de` resource files | `src/OgmaLibrary.App/Localization/de/*.resx` |
| Translation memory / glossary | `docs/i18n/GLOSSARY.md` |
| Native-speaker QA records | `docs/i18n/QA-RECORDS-es.md`, `...-it.md`, `...-de.md` |
| 5-locale `IconCatalog` build check | `src/OgmaLibrary.App/Icons/IconCatalog.cs` (updated) |
| Comprehensive review report | `docs/qa/COMPREHENSIVE-REVIEW-P21.md` |
| Golden-corpus E2E sign-off | `docs/qa/GOLDEN-CORPUS-SIGNOFF.md` |
| 9-layer test matrix sign-off | `docs/qa/TEST-MATRIX-SIGNOFF.md` |
| Beta-gate status | `docs/qa/BETA-GATES-STATUS.md` |
| Store pre-check record | `docs/qa/STORE-PRECHECK-P21.md` |

---

## 12. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Native-speaker reviewers unavailable within the 4-week window | R5 | Identify and engage three reviewers (es, it, de) before Phase 21 starts; sign NDA/review agreements in Phase 20. Translations can proceed in parallel using the completeness-audit export. |
| Avalonia UI Automation gaps on macOS (NSAccessibility coverage incomplete) | R3 | Identify gaps in WP2; file upstream issues if needed; apply `AccessibilityOverride` workarounds for the MVP release; document gaps as tracked items if unresolvable in 4 weeks. |
| i18n changes cause layout overflow in de/it (German/Italian strings 30-50% longer than en) | R3 | Pseudolocale check in WP7 catches truncation before translation; design tokens use flexible layouts (no fixed-width text containers) since Phase 03. |
| Comprehensive review uncovers a Critical architectural finding requiring significant rework | R3 | The review is scheduled at the start of WP9 (week 3); two weeks remain for remediation. If a rework takes >2 weeks, escalate to owner and extend the phase by owner sign-off. |
| Benchmark regression from a11y changes (automation-property additions have measurable overhead on Avalonia) | R3 | Benchmark re-run at end of WP2; if regression found, profile and optimize before WP3. |

---

## 13. Owner asks

1. **Native-speaker reviewer engagement:** Authorize engagement of three
   native-speaker QA reviewers for `es`, `it`, and `de`. Provide budget
   approval and any NDA requirements. Deadline: before Phase 21 starts.
2. **Golden-corpus E2E countersignature:** Review and countersign
   `docs/qa/GOLDEN-CORPUS-SIGNOFF.md` after the team records the results.
3. **Comprehensive review Critical/High findings:** Review the list of
   Critical and High findings from `comprehensive-review:full-review` and
   confirm priority and remediation approach before WP9 remediation work begins.
4. **Color-blind audit findings:** Review the `docs/qa/COLOR-BLIND-AUDIT.md`
   and confirm any design changes to color-only status indicators are acceptable
   within the icon system aesthetic.
5. **Store pre-check blockers (Phase 22 prep):** Review
   `docs/qa/STORE-PRECHECK-P21.md` and sign off that no Phase 22-blocking
   a11y or i18n issue remains.

---

## 14. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand Plan authoring | v1.0 baseline created |
