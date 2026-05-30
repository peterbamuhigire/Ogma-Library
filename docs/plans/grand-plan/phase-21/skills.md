# Phase 21 — Skills & Slash Commands

> Phase-scoped detail. The bird's-eye map is `SKILLS-INDEX.md`.

---

## Always-on (every phase)

| Skill / command | Task | Artifact |
| --- | --- | --- |
| `superpowers:writing-plans` → `superpowers:executing-plans` | Before WP1 | Execution plan across the 4-week phase |
| `superpowers:test-driven-development` | WP1, WP7 | A11y test harness and locale tests written before remediation |
| `superpowers:verification-before-completion` | End of each WP | Verification checklist confirming CI checks green before WP closes |
| `superpowers:systematic-debugging` | Any a11y regression or benchmark regression | Root-cause analysis before remediation |
| `superpowers:requesting-code-review` + `/code-review` | End of WP2, WP6, WP9 | Review a11y remediation changes, i18n resource files, comprehensive-review remediations |
| `superpowers:using-git-worktrees` | WP1–WP11 | `feature/P21-accessibility`, `feature/P21-i18n-final`, `feature/P21-comprehensive-review` |

---

## Phase-specific skills

### WP1 — Axe-style automated tooling

**`full-stack-orchestration:test-automator`**
- Tasks: P21-WP1-T1 through P21-WP1-T4
- Produce: `OgmaLibrary.Tests.Accessibility` project; `AvaloniaA11yTreeWalker`;
  `.github/workflows/a11y.yml`.
- Invocation: Use this skill to design the accessibility test runner: how to
  walk the Avalonia UI automation tree, map elements to WCAG success criteria,
  compute contrast ratios from design tokens, and integrate axe-core into the
  WebView surface via a JS injection bridge.

**`frontend-ux:ux-principles-101`**
- Tasks: P21-WP1-T1, P21-WP2-T1
- Produce: WCAG 2.2 criterion-to-control mapping document used as the triage
  guide in WP2.
- Invocation: Use this skill to map each WCAG 2.2 Level AA success criterion to
  the specific Avalonia control types and UI patterns in Ogma Library, so the
  automated check knows which elements to test for each criterion.

---

### WP2–WP4 — A11y remediation and audit

**`frontend-ux:ux-principles-101`** (continued)
- Tasks: P21-WP2-T2 through P21-WP4-T2
- Produce: All `AutomationProperties` additions; focus-ring token adjustments.
- Invocation: For each finding, use this skill to determine the correct ARIA
  role, name pattern, and live-region policy for Avalonia's UIA/NSAccessibility
  backends; confirm that the fix satisfies the criterion for both NVDA and
  VoiceOver.

**`frontend-ux:design-audit`**
- Tasks: P21-WP4-T1, P21-WP5-T1
- Produce: `docs/qa/COLOR-BLIND-AUDIT.md`; focus-ring design token changes.
- Invocation: Use this skill to run the visual design audit for focus appearance
  (SC 2.4.11) and color-blind simulation (1.4.1). The design-audit skill provides
  a systematic checklist for measuring pixel dimensions and contrast ratios
  without relying on subjective assessment.

---

### WP3 — Manual screen-reader passes

**`document-skills:webapp-testing`**
- Tasks: P21-WP3-T1 through P21-WP3-T5
- Produce: `docs/qa/SR-TEST-SCRIPT.md`; `docs/qa/A11Y-SIGNOFF.md`.
- Invocation: Use this skill to structure the manual SR test script: how to
  write step-by-step instructions for NVDA and VoiceOver that are reproducible
  by a non-specialist tester; how to record findings in a way that maps back to
  specific WCAG success criteria and specific UI elements.

---

### WP6 — es/it/de translation

**`ux-content-strategy`**
- Tasks: P21-WP6-T1, P21-WP6-T5
- Produce: Missing-key export; updated `docs/i18n/GLOSSARY.md`.
- Invocation: Use this skill to define the context notes and usage examples
  accompanying each missing key export (so native-speaker translators receive
  enough context to produce accurate, tone-consistent translations without
  back-and-forth).

**`content-writing`**
- Tasks: P21-WP6-T2 through P21-WP6-T4
- Produce: Review of the `en` source copy for any strings that are idiomatically
  difficult to translate (e.g., "calm control," "durable," "spine texture");
  suggested rewordings in `en` that preserve meaning while being more
  translation-friendly, before the translation is commissioned.
- Invocation: Run this skill on the full `en` resource file before the
  translation export to identify any problematic strings. Fixes to `en` copy
  must be reviewed for consistency with existing translated `fr` strings.

---

### WP7 — Locale tests

**`sdlc-meta:advanced-testing-strategy`**
- Tasks: P21-WP7-T1 through P21-WP7-T3
- Produce: `PluralRuleTests`, `LocaleFormatTests`, pseudolocale truncation check.
- Invocation: Use this skill to design the parameterized plural-rule test cases:
  identify the ICU plural categories for each locale (en: one/other; fr:
  one/other; de: one/other; it: one/other; es: one/other with irregular zero
  handling) and generate test inputs that exercise each category.

---

### WP8 — Icon label completeness

**`frontend-ux:design-audit`** (continued)
- Tasks: P21-WP8-T1, P21-WP8-T2
- Produce: Updated `IconCatalog` build check; filled `es/it/de` label resources.
- Invocation: Use the design-audit skill's icon inventory pass to systematically
  enumerate every icon in the master manifest and cross-reference against the
  5-locale resource files; generate a gap report that becomes the WP8-T2 task list.

---

### WP9 — Comprehensive review

**`comprehensive-review:full-review`**
- Tasks: P21-WP9-T1 through P21-WP9-T4
- Produce: `docs/qa/COMPREHENSIVE-REVIEW-P21.md`; remediation commits.
- Invocation: This is the primary invocation of the `comprehensive-review:full-review`
  skill for the project. Set the review scope to: (1) architecture — bounded-context
  dependency graph, ADR compliance (ADR-0001..0020); (2) security — CTRL-OGMA-001..025
  checklist, residual STRIDE threat list from Phase 19; (3) performance — NFR-OGMA
  and NFR-PROD gate status from Phase 20 benchmarks; (4) testing — 9-layer matrix
  completeness, R1/R2 open defects. Request a written report with findings
  classified as Critical/High/Medium/Low.

---

### WP10 — Golden-corpus E2E

**`sdlc-meta:e2e-testing`**
- Tasks: P21-WP10-T1 through P21-WP10-T3
- Produce: `docs/qa/GOLDEN-CORPUS-SIGNOFF.md`; E2E test run artifacts.
- Invocation: Use this skill to structure the E2E execution plan: which test
  scenarios map to which golden-corpus documents, how to record a pass/fail
  verdict for each (scenario, document, platform) combination, and how to
  generate the sign-off document automatically from the test runner output.

---

### WP11 — Test matrix & beta gates

**`sdlc-meta:sdlc-testing`**
- Tasks: P21-WP11-T1 through P21-WP11-T3
- Produce: `docs/qa/TEST-MATRIX-SIGNOFF.md`; `docs/qa/BETA-GATES-STATUS.md`;
  `docs/qa/STORE-PRECHECK-P21.md`.
- Invocation: Use this skill to structure the test matrix sign-off document:
  for each of the 9 test layers, define the sign-off lead, the pass criteria,
  and the open-gap tagging convention. The skill also provides the checklist
  template for beta-gate status.

---

## Slash commands

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review` (medium effort) | End of WP2, WP6 | Review a11y remediation changes and i18n resource files |
| `/code-review ultra` | End of WP9 | Deep review of comprehensive-review remediations |
| `/security-review` | WP9 | Confirm CTRL-OGMA compliance after remediations |
| `/verify` | After WP3-T5, WP6-T6, WP10-T3 | Run SR test script manually; confirm locale correctness on device; confirm E2E pass |
| `/run` | WP3-T2..T4 | Launch app with NVDA/VoiceOver active; navigate using keyboard only |
| `superpowers:finishing-a-development-branch` | Phase gate | Decide merge/PR strategy before closing Phase 21 |
