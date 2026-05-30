# Internationalization & Localization Strategy

> Owner directive: *"We must develop the app to be multilingual. MVP should be
> full English / French; the final product must have Spanish, Italian and German
> too."*

Ogma Library is multilingual **by construction** — localization is designed in
from Phase 03, not retrofitted. This document defines the languages, the
architecture, the governance, and the gates.

---

## 1. Language tiers

| Locale | Code | Tier | When complete |
| --- | --- | --- | --- |
| English | `en` | MVP | Phase 03 (source language) |
| French | `fr` | MVP | Phase 03 → full coverage gate at MVP |
| Spanish | `es` | Final / V2 | Phase 21 |
| Italian | `it` | Final / V2 | Phase 21 |
| German | `de` | Final / V2 | Phase 21 |

"Full" means: 100% of user‑facing strings translated, plurals correct, dates/
numbers/units formatted per locale, and a native‑speaker QA pass for `fr` at MVP
and `es/it/de` at final.

## 2. Architecture

- **Source language is English (`en`)**; it is the key set of record. Strings
  are referenced by stable resource keys, never by literal text.
- **No hard‑coded user‑facing strings.** A Roslyn analyzer / lint rule
  (introduced Phase 03) fails the build on a literal string in a view or
  view‑model that reaches the UI. This is part of warnings‑as‑errors.
- **Resource format:** `.resx` (or a structured `.json`/`.po` pipeline chosen in
  Phase 03) per locale, compiled into satellite assemblies. The choice is
  recorded in an ADR; Avalonia data‑binding consumes a localization service
  (`ILocalizationService`) so the active culture can switch at runtime without
  restart.
- **Culture‑aware formatting:** all dates, times, numbers, file sizes, reading
  statistics, and currency (AI cost estimates, FR‑AI‑010) format via
  `CultureInfo`; no manual string concatenation of formatted values.
- **Pluralization & gender:** use ICU‑style plural rules (or .NET equivalents),
  not `count == 1 ? "book" : "books"` hand‑rolled logic, so `fr/es/it/de` plural
  categories are correct.
- **RTL readiness:** layouts are mirror‑safe (logical start/end, not left/right)
  even though the five launch languages are LTR, so a future Arabic/Hebrew
  locale is cheap. This is a design‑token rule, not extra work.
- **Icons are language‑neutral** but every icon's **accessible label is
  localized** (see `ICON-SYSTEM.md`); an icon without a label in the active
  locale fails the IconCatalog build check.

## 3. Content that must be localized

UI chrome, menus, command‑palette entries, tooltips, empty states, error and
job‑failure messages, onboarding, the Privacy Center labels and payload‑preview
copy, AI explanations' *scaffolding* (the fixed wording around model output),
notification/update copy, classroom/admin console (Phases 16–18), and the Store
listing metadata. **Not localized:** user‑authored data, book metadata from
providers, and raw model output (which follows the user's query language).

## 4. Governance & workflow

- A **translation memory / glossary** keeps domain terms consistent (e.g.
  "shelf", "catalogue", "annotation", "privacy tier") across all five locales.
- New strings are added in `en` with a screenshot/context note; `fr` is updated
  in the same PR for MVP‑tier surfaces (so `fr` never drifts behind `en`).
- `es/it/de` are batched for Phase 21 but their keys exist from creation (empty
  → flagged by the pseudolocale check) so no surface is structurally English‑only.
- The `ux-content-strategy` and `content-writing` skills inform tone and the
  source `en` copy; native‑speaker reviewers validate each locale before its
  gate.

## 5. CI gates

| Gate | Phase | Check |
| --- | --- | --- |
| No hard‑coded strings | 03 → all | Analyzer fails build on UI literals. |
| Pseudolocale render | 03 → all | App runs under a `qps`‑style pseudolocale; truncation/overflow/clipping in the colorful‑icon layouts surface before translation. |
| `en`/`fr` completeness | each UI phase | Missing key in `fr` for an MVP surface fails the phase DoD. |
| Full `es/it/de` completeness | 21 | 100% keys translated + native QA pass. |
| Format/locale correctness | 20–21 | Tests assert dates/numbers/plurals per culture. |

## 6. Relationship to the cross‑cutting DoD

Every phase's Definition of Done includes: *"New user strings externalized and
present in en + fr; pseudolocale check passes."* Phase 21 adds the `es/it/de`
completeness gate. This keeps localization a continuous obligation rather than an
end‑of‑project scramble.
