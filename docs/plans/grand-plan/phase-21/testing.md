# Phase 21 — Test Plan

> Which test layers apply, fixtures, oracles, and the Phase 21 slice of the
> golden-corpus, a11y, i18n, and multi-dimensional review gates.

---

## 1. Test layers in scope

| Layer | Applied | Notes |
| --- | --- | --- |
| 1. Domain | No | No domain-model changes |
| 2. Infrastructure | No | No infrastructure changes (i18n is resource-layer) |
| 3. PDF | Partial | Golden-corpus E2E exercises the PDF reader flow |
| 4. Search | Partial | Golden-corpus E2E exercises search flows |
| 5. AI | Partial | Golden-corpus E2E exercises AI-assisted search flow |
| 6. UI | Primary | A11y automated checks, SR scripts, i18n locale tests, focus/keyboard audit |
| 7. 3D | Partial | Axe-core check on WebView; accessible fallback SR verification |
| 8. Performance | Secondary | Re-run Phase 20 benchmarks after a11y/i18n changes |
| 9. Packaging | No | Phase 22 |

---

## 2. Accessibility tests (Layer 6 — primary)

### 2a. Automated axe-style checks

**Project:** `OgmaLibrary.Tests.Accessibility`

| Test class | Scope | Oracle |
| --- | --- | --- |
| `CatalogueViewA11yTests` | Grid view, list view, directory view, sort/filter controls | Zero `A11yViolation` records with severity Critical or High |
| `BookDetailA11yTests` | Book-detail panel, metadata editor, shelf selector | Zero violations |
| `ReaderViewA11yTests` | Reader toolbar, page navigator, zoom controls, search panel, annotation panel | Zero violations |
| `SearchViewA11yTests` | Search bar, results list, filter chips, AI-result cards | Zero violations |
| `SettingsViewA11yTests` | All Settings tabs including Privacy Center and Telemetry | Zero violations |
| `ThreeDShelfA11yTests` | WebView axe-core injection on 3D shelf; accessible fallback Avalonia tree | Zero violations in axe result; fallback grid passes `CatalogueViewA11yTests` |

**Contrast ratio sub-tests (within above classes):**
- Every text element: contrast ≥ 4.5:1 (normal) or ≥ 3:1 (large / UI component).
- Focus rings: ≥ 3:1 against adjacent color; ≥ 2 px outline.
- Design-token snapshot: the token values producing the tested colors are recorded
  in `docs/qa/A11Y-CONTRAST-SNAPSHOT.md` so a future token change triggers
  a re-run.

### 2b. Manual screen-reader passes

**Script:** `docs/qa/SR-TEST-SCRIPT.md`

| Flow | Platform | Screen reader | Pass criterion |
| --- | --- | --- | --- |
| 1. Catalogue browse (grid + list) | Windows | NVDA 2024.x + Chrome/Edge UIA | All books announced with title and author; filter controls have descriptive names; sort options read correctly |
| 1. Catalogue browse (grid + list) | macOS | VoiceOver + Safari | Same as above via NSAccessibility |
| 2. Book detail + metadata edit | Windows | NVDA | All fields announced; edit-in-place controls have role TextBox; save/cancel buttons have names |
| 2. Book detail + metadata edit | macOS | VoiceOver | Same |
| 3. PDF reader | Windows | NVDA | Reader opens; page number announced on turn; zoom level announced on change; search results list announced; annotation creation dialog fully accessible |
| 3. PDF reader | macOS | VoiceOver | Same |
| 4. Search | Windows | NVDA | Search bar has accessible name "Search library"; results list announced with count; AI-result cards announce title, author, and relevance explanation |
| 4. Search | macOS | VoiceOver | Same |
| 5. Settings + Privacy Center | Windows | NVDA | All Settings categories accessible; Privacy Center tier descriptions announced; telemetry toggle announces state change |
| 5. Settings + Privacy Center | macOS | VoiceOver | Same |
| 6. 3D shelf + fallback | Windows | NVDA | "Switch to Grid" button keyboard-reachable and announced; activating it moves focus to grid view |
| 6. 3D shelf + fallback | macOS | VoiceOver | Same |

Pass criterion for the full manual pass: zero unresolved findings in
`docs/qa/A11Y-SIGNOFF.md` after remediation.

---

## 3. i18n / localization tests (Layer 6 — secondary)

### 3a. Completeness check

| Check | Tool | Oracle |
| --- | --- | --- |
| `ResourceCompleteness` CI check | Custom MSBuild task | 100% of `en` keys present and non-empty in `fr`, `es`, `it`, `de` |

### 3b. Plural rule tests

**Class:** `PluralRuleTests`

Parameterized test cases (locale, count, key, expectedText):

| Locale | Count | Key | Expected output |
| --- | --- | --- | --- |
| en | 1 | `Catalogue.BookCount` | "1 book" |
| en | 2 | `Catalogue.BookCount` | "2 books" |
| fr | 1 | `Catalogue.BookCount` | "1 livre" |
| fr | 2 | `Catalogue.BookCount` | "2 livres" |
| de | 1 | `Catalogue.BookCount` | "1 Buch" |
| de | 2 | `Catalogue.BookCount` | "2 Bücher" |
| es | 1 | `Catalogue.BookCount` | "1 libro" |
| es | 0 | `Catalogue.BookCount` | "0 libros" |
| it | 1 | `Catalogue.BookCount` | "1 libro" |
| it | 5 | `Catalogue.BookCount` | "5 libri" |

(Full table covers all pluralized keys across all 5 locales in `testing.md`
appendix; summarized here.)

### 3c. Format tests

**Class:** `LocaleFormatTests`

| Test | Locale | Input | Expected output |
| --- | --- | --- | --- |
| Date format | de | 2024-03-15 | "15.03.2024" |
| Date format | fr | 2024-03-15 | "15/03/2024" |
| Date format | es | 2024-03-15 | "15/3/2024" |
| Number format (thousands) | de | 50000 | "50.000" |
| Number format (decimal) | fr | 3.14 | "3,14" |
| File size | en | 1536000 | "1.5 MB" |
| File size | de | 1536000 | "1,5 MB" |
| AI cost estimate | fr | 0.0012 USD | "0,001 $" |

### 3d. Pseudolocale truncation check

**Class:** `PseudolocaleLayoutTests`

Runs the app under `qps-ploc` pseudolocale (strings expanded to ~130% of `en`
length with bracket markers); asserts that no view clips or overflows any text
element. Fails on any `DesiredSize > ActualSize` discrepancy on a text control.

---

## 4. Icon label completeness tests

**Class:** `IconCatalogLabelTests`

| Test | Oracle |
| --- | --- |
| `AllIcons_HaveLabel_En` | Every icon key in `IconCatalog` has non-empty `en` label |
| `AllIcons_HaveLabel_Fr` | Every icon key has non-empty `fr` label |
| `AllIcons_HaveLabel_Es` | Every icon key has non-empty `es` label (new in Phase 21) |
| `AllIcons_HaveLabel_It` | Every icon key has non-empty `it` label (new in Phase 21) |
| `AllIcons_HaveLabel_De` | Every icon key has non-empty `de` label (new in Phase 21) |
| `AllIcons_LabelKeys_ExistInResources` | Every label resource key referenced by `IconCatalog` exists in all 5 locale resource files |

---

## 5. Keyboard operability tests

**Class:** `KeyboardOperabilityTests` (automated where possible; documented
as manual for complex flows)

| Test | Method | Oracle |
| --- | --- | --- |
| Tab order in main navigation | Automated (tab-key simulation via Avalonia test input) | Focus visits every interactive control exactly once in logical order |
| Escape closes all modals | Automated | Pressing Escape from any open modal returns focus to the triggering element |
| No keyboard trap in confirmation dialog | Automated | Tab key cycles within the dialog; Escape dismisses it |
| Arrow-key navigation in grid view | Automated | Left/Right/Up/Down move focus between book cards |
| Keyboard shortcut registry | Automated | All registered shortcuts in the command palette are exercisable without a mouse |

---

## 6. Performance regression check (after a11y/i18n changes)

After all a11y and i18n changes are committed, re-run the Phase 20 benchmark
suite in its `ShortRun` configuration:
- All NFR-OGMA-001..009 budgets must remain within 10% of the Phase 20 baseline.
- If any metric regresses, the Phase 21 change set is bisected to find the
  introducing commit; the regression is fixed before Phase 21 closes.

Oracle: Phase 20 `baseline-windows.json` and `baseline-macos.json` are the
reference. The regression-check script (`scripts/benchmark-compare.py`)
is reused from Phase 20.

---

## 7. Comprehensive review verification

The `comprehensive-review:full-review` report (`docs/qa/COMPREHENSIVE-REVIEW-P21.md`)
is verified by checking:
- Every Critical finding has a resolution entry with a commit SHA.
- Every High finding has a resolution entry with a commit SHA.
- No new Critical or High finding was introduced by Phase 21 remediation work
  (verified by running the architecture tests and SAST scan on the post-remediation
  codebase).

---

## 8. Golden-corpus E2E sign-off matrix

All 11 golden-corpus documents × all flows × both platforms.

| Document | Scan | Browse | Read | Search | Annotate | AI search |
| --- | --- | --- | --- | --- | --- | --- |
| `gc-simple-text.pdf` | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS |
| `gc-scanned-imageonly.pdf` | Win/macOS | Win/macOS | Win/macOS | N/A (no text layer) | Win/macOS | N/A |
| `gc-password-protected.pdf` | Win/macOS | Win/macOS | Win/macOS (password prompt) | N/A | N/A | N/A |
| `gc-large-1000pp.pdf` | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS |
| `gc-two-column.pdf` | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS |
| `gc-bad-metadata.pdf` | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS |
| `gc-outline-toc.pdf` | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS |
| `gc-rotated-pages.pdf` | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS |
| `gc-nonenglish.pdf` | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS |
| `gc-forms-unusual-fonts.pdf` | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS | Win/macOS |
| `gc-synthetic-perf-marker.pdf` | Win/macOS | Win/macOS | Win/macOS | Win/macOS | N/A | N/A |

"N/A" = not applicable for that document/flow combination; not a failure.
All applicable cells must show "Pass" before the sign-off is submitted.

---

## 9. Test artifacts committed by Phase 21

| Artifact | Location |
| --- | --- |
| `OgmaLibrary.Tests.Accessibility` project | `tests/OgmaLibrary.Tests.Accessibility/` |
| Locale test classes | `tests/OgmaLibrary.Tests.Localization/` |
| SR test script | `docs/qa/SR-TEST-SCRIPT.md` |
| A11y sign-off | `docs/qa/A11Y-SIGNOFF.md` |
| Contrast snapshot | `docs/qa/A11Y-CONTRAST-SNAPSHOT.md` |
| Color-blind audit | `docs/qa/COLOR-BLIND-AUDIT.md` |
| Comprehensive review | `docs/qa/COMPREHENSIVE-REVIEW-P21.md` |
| Golden-corpus sign-off | `docs/qa/GOLDEN-CORPUS-SIGNOFF.md` |
| Test matrix sign-off | `docs/qa/TEST-MATRIX-SIGNOFF.md` |
| Beta-gate status | `docs/qa/BETA-GATES-STATUS.md` |
| Store pre-check | `docs/qa/STORE-PRECHECK-P21.md` |
