# Phase 21 — Tasks

> Work packages → tasks. Read `README.md` first for scope and objectives.

---

## Work Package 1: Axe-Style A11y Automated Tooling

**Goal:** CI gate that fails on any WCAG 2.2 AA automated violation.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP1-T1 | Create `OgmaLibrary.Tests.Accessibility` project; implement `AvaloniaA11yTreeWalker` that traverses the Avalonia UI automation tree and returns a list of `A11yViolation` records (missing name, missing role, contrast failure, keyboard-trap indicator). | 1 d | Phase 03 a11y scaffold | NFR-PROD-007/008 |
| P21-WP1-T2 | Integrate axe-core (via WebView JS injection) for the 3D shelf WebView surface; extract `violations[]` from axe result; map to `A11yViolation` records; route into the same test runner. | 0.5 d | Phase 14, P21-WP1-T1 | NFR-PROD-008 (3D surface) |
| P21-WP1-T3 | Add contrast-ratio check: for each text element in the Avalonia tree, compute foreground/background contrast ratio from design tokens; fail if < 4.5:1 (normal text) or < 3:1 (large text / UI components). | 0.5 d | P21-WP1-T1 | WCAG 1.4.3 |
| P21-WP1-T4 | Wire the accessibility CI check into `.github/workflows/a11y.yml`; run on both Windows and macOS CI runners; fail PR on any Critical violation; post a violation summary as a PR comment. | 0.5 d | P21-WP1-T1..T3 | CI gate |

---

## Work Package 2: A11y Remediation (Automated Findings)

**Goal:** Zero automated violations across all UI surfaces.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP2-T1 | Run P21-WP1 checks against all Avalonia views; triage findings into Critical (missing name on interactive control), High (missing role, contrast fail), Medium (live-region missing). | 0.5 d | P21-WP1-T4 | NFR-PROD-007/008 |
| P21-WP2-T2 | Fix all Critical findings: add `AutomationProperties.Name` (bound to the icon's accessible label resource key) to all buttons, menu items, toggle controls, and list items across all views in Phases 03–20. | 1 d | P21-WP2-T1 | WCAG 4.1.2, NFR-PROD-007 |
| P21-WP2-T3 | Fix all High findings: set explicit `AutomationProperties.ControlType` where Avalonia does not infer it; fix contrast failures in design tokens (adjust foreground or background token values). | 1 d | P21-WP2-T1 | WCAG 1.4.3, 4.1.2 |
| P21-WP2-T4 | Add `AutomationProperties.LiveSetting = Polite` to: search-results list, health-dashboard update region, AI-result cards, job-progress notifications. | 0.5 d | P21-WP2-T1 | WCAG 4.1.3 |
| P21-WP2-T5 | Re-run axe CI check; confirm zero violations; commit. Re-run Phase 20 benchmark suite; confirm no regression from automation-property additions. | 0.5 d | P21-WP2-T2..T4, Phase 20 benchmarks | Regression gate |

---

## Work Package 3: Manual Screen-Reader Passes (6 flows × 2 platforms)

**Goal:** NVDA (Windows) and VoiceOver (macOS) passes on all 6 core flows.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP3-T1 | Define the 6-flow SR test script with exact steps, expected announcements, and pass/fail criteria. Commit to `docs/qa/SR-TEST-SCRIPT.md`. | 0.5 d | P21-WP2-T5 | NFR-PROD-007/008 |
| P21-WP3-T2 | Execute flows 1-3 (catalogue browse, book detail/metadata, PDF reader) with NVDA on Windows; record all violations in `docs/qa/A11Y-SIGNOFF.md`. | 0.5 d | P21-WP3-T1 | WCAG 2.1.1, 4.1.2 |
| P21-WP3-T3 | Execute flows 4-6 (search, Settings/Privacy Center, 3D fallback) with NVDA on Windows; record findings. | 0.5 d | P21-WP3-T1 | WCAG 2.1.2, 4.1.2 |
| P21-WP3-T4 | Execute all 6 flows with VoiceOver on macOS; record findings. | 1 d | P21-WP3-T1 | NFR-PROD-008 |
| P21-WP3-T5 | Remediate all findings from NVDA and VoiceOver passes; re-test each remediated flow; update `docs/qa/A11Y-SIGNOFF.md` to show pass status per flow per platform. | 1 d | P21-WP3-T2..T4 | Phase-specific DoD |

---

## Work Package 4: Focus Appearance & Keyboard Trap Audit

**Goal:** SC 2.4.11 and 2.1.2 compliance verified and documented.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP4-T1 | Audit focus rings across all views in both light and dark themes: measure pixel size (≥2 px) and contrast ratio (≥3:1 against adjacent color). Use design-token contrast calculator. Fix any failing focus rings by adjusting the `focus-ring` design token. | 0.5 d | Phase 03 design tokens | WCAG 2.4.11 |
| P21-WP4-T2 | Tab through every modal, dialog, fly-out, and context menu in the app; confirm focus is trapped within the overlay (no leaking to background content) and that Escape always closes the overlay and returns focus to the triggering element. | 0.5 d | All UI phases | WCAG 2.1.2 |

---

## Work Package 5: Color-Blind Audit

**Goal:** No information conveyed by color alone.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP5-T1 | Generate deuteranopia, protanopia, and tritanopia simulations of all key status surfaces (health badges, AI-tier chips, availability flags, reading-progress rings, performance meter chips from Phase 20). Use a color-simulation tool or browser extension on screenshot captures. | 0.5 d | All UI phases | ICON-SYSTEM.md principle |
| P21-WP5-T2 | For each surface where a status is distinguishable ONLY by color (not by shape, icon, or label) in any simulation, add a text label or icon differentiation. Record in `docs/qa/COLOR-BLIND-AUDIT.md`. | 0.5 d | P21-WP5-T1 | NFR-PROD-008, WCAG 1.4.1 |

---

## Work Package 6: es/it/de Translation

**Goal:** 100% translation coverage with native-speaker QA for all three locales.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP6-T1 | Run `ResourceCompleteness` CI check for `es`, `it`, `de`; export missing keys to `docs/i18n/missing-keys-<locale>.csv` with context screenshots and `en` reference text. | 0.5 d | I18N-STRATEGY.md; all prior UI phases | I18N completeness |
| P21-WP6-T2 | Translate all missing `es` keys using the glossary; submit to native-speaker reviewer; apply review feedback; commit `es/*.resx`. | 1 d | P21-WP6-T1 | I18N-es |
| P21-WP6-T3 | Translate all missing `it` keys using the glossary; native-speaker QA; commit `it/*.resx`. | 1 d | P21-WP6-T1 | I18N-it |
| P21-WP6-T4 | Translate all missing `de` keys using the glossary; native-speaker QA; commit `de/*.resx`. | 1 d | P21-WP6-T1 | I18N-de |
| P21-WP6-T5 | Update `docs/i18n/GLOSSARY.md` with the final canonical terms for all 5 locales; record any locale-specific term exceptions. | 0.5 d | P21-WP6-T2..T4 | I18N governance |
| P21-WP6-T6 | Record native-speaker QA sign-offs in `docs/i18n/QA-RECORDS-es.md`, `...-it.md`, `...-de.md` with reviewer name, date, and scope. | 0.5 d | P21-WP6-T2..T4 | Phase-specific DoD |

---

## Work Package 7: Locale Tests (Plurals, Formats, Pseudolocale)

**Goal:** Automated verification that locale-specific rules are correct.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP7-T1 | Write `PluralRuleTests` parameterized over `(locale, count, expectedOutput)` for all resource keys using plural forms (e.g., `book_count`, `annotation_count`, `chapter_count`). Locales: en, fr, es, it, de. Counts: 0, 1, 2, 5, 11, 21. | 0.5 d | P21-WP6-T2..T4 | I18N correctness |
| P21-WP7-T2 | Write `LocaleFormatTests`: assert that date, time, number, and file-size formatting produces the expected locale output for each of the 5 locales (e.g., German decimal comma, French space thousands separator). | 0.5 d | I18N-STRATEGY.md §2 | I18N correctness |
| P21-WP7-T3 | Run pseudolocale truncation check for all 5 locales: render each view under `qps-ploc` pseudolocale; assert no text element is clipped or overflows its container. Fix any layout issues. | 0.5 d | P21-WP7-T1..T2 | I18N-STRATEGY.md §5 |

---

## Work Package 8: Icon Label Completeness (5 Locales)

**Goal:** `IconCatalog` build check enforces 5-locale label coverage.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP8-T1 | Extend `IconCatalog` build check to assert that every icon key has a non-empty label resource in `en`, `fr`, `es`, `it`, `de`; update the build check to fail on any missing label in any locale. | 0.5 d | ICON-SYSTEM.md §6 | I18N-ICON, Phase 21 DoD |
| P21-WP8-T2 | Audit every icon in `docs/plans/grand-plan/_icons/MASTER-MANIFEST.md` against the 5-locale resource files; fill all missing `es/it/de` labels using the glossary-consistent terminology. | 0.5 d | P21-WP8-T1, P21-WP6-T5 | I18N-ICON |

---

## Work Package 9: Comprehensive Multi-Dimensional Review

**Goal:** All Critical and High findings from `comprehensive-review:full-review` resolved.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP9-T1 | Invoke `comprehensive-review:full-review` across architecture, security, performance, and testing dimensions; save report to `docs/qa/COMPREHENSIVE-REVIEW-P21.md`. | 1 d | Phases 19, 20 complete | comprehensive-review |
| P21-WP9-T2 | Triage findings: Critical (must fix before Phase 22), High, Medium, Low; owner reviews Critical list and confirms priority. | 0.5 d | P21-WP9-T1 | Phase-specific DoD |
| P21-WP9-T3 | Remediate all Critical findings; document each resolution in `docs/qa/COMPREHENSIVE-REVIEW-P21.md`; re-run relevant tests to confirm. | 1.5 d | P21-WP9-T2 | Phase-specific DoD |
| P21-WP9-T4 | Remediate all High findings; document resolutions. | 1 d | P21-WP9-T2 | Phase-specific DoD |

---

## Work Package 10: Golden-Corpus E2E Sign-Off

**Goal:** All 11 golden-corpus documents pass the full flow on both platforms.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP10-T1 | Run the full golden-corpus E2E suite on Windows CI runner; record pass/fail per document per flow (scan, display, read, search, annotate); capture any failures. | 1 d | All feature phases complete | GOLDEN-CORPUS-E2E |
| P21-WP10-T2 | Run the same E2E suite on macOS CI runner; record results. | 1 d | P21-WP10-T1 | GOLDEN-CORPUS-E2E |
| P21-WP10-T3 | Remediate any E2E failures; re-run; commit `docs/qa/GOLDEN-CORPUS-SIGNOFF.md` with all-pass status for both platforms; owner countersignature. | 1 d (variable) | P21-WP10-T1..T2 | Phase-specific DoD |

---

## Work Package 11: 9-Layer Test Matrix Sign-Off & Beta Gates

**Goal:** Formal sign-off on each test layer and all 8 beta gates.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P21-WP11-T1 | Compile `docs/qa/TEST-MATRIX-SIGNOFF.md`: for each of the 9 test layers, record test count, pass count, open gaps (tagged by R-tier), and the sign-off lead. | 0.5 d | All test phases | TEST-MATRIX |
| P21-WP11-T2 | Compile `docs/qa/BETA-GATES-STATUS.md`: for each of the 8 public-beta gates (G1-G8), record the test method that covers it, the current status (green/amber/red), and any remediation needed. | 0.5 d | P21-WP10, P20-WP5 | G1-G8 |
| P21-WP11-T3 | Compile `docs/qa/STORE-PRECHECK-P21.md`: review Mac App Store and Windows Store guidelines for a11y and localization requirements; record any gaps that would block Phase 22. | 0.5 d | P21-WP3, P21-WP6 | Phase 22 unblock |
