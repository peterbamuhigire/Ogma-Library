# Phase 21 — Icon Manifest (Verification / Label Coverage)

> Phase 21 introduces **no new icons**. This manifest is a **verification
> manifest**: it confirms that every icon in the master manifest
> (`docs/plans/grand-plan/_icons/MASTER-MANIFEST.md`) has a localized
> accessible label in all **five locales** (en / fr / es / it / de).
>
> The `IconCatalog` build check is extended in Phase 21 to enforce 5-locale
> coverage. Any icon failing this check is listed below with its status.

---

## Verification scope

Phase 21 must confirm that every icon key produced by Phases 03 through 20
satisfies the following:

| Requirement | Check |
| --- | --- |
| Label resource key exists in `en` | `IconCatalog` build check (in place since Phase 03) |
| Label resource key exists in `fr` | `IconCatalog` build check (in place since Phase 03) |
| Label resource key exists in `es` | **New in Phase 21** — build check extended |
| Label resource key exists in `it` | **New in Phase 21** — build check extended |
| Label resource key exists in `de` | **New in Phase 21** — build check extended |
| Label is non-empty (not a placeholder stub) in all 5 locales | `AllIcons_LabelKeys_ExistInResources` test |

---

## Process

1. Run the `IconCatalogLabelTests` suite (see `testing.md` §4).
2. For any icon with a missing or stub label in `es`, `it`, or `de`:
   - Add the translation using the Phase 21 glossary (`docs/i18n/GLOSSARY.md`).
   - Verify the translation with the native-speaker reviewer for that locale.
   - Commit the updated resource file.
3. Re-run the test suite until all five `AllIcons_HaveLabel_<locale>` tests pass.
4. Mark each icon as ✅ in the MASTER-MANIFEST.md "5-locale complete" column.

---

## Known gaps at Phase 21 start

Icons introduced in Phases 03–20 have `en` and `fr` labels complete (enforced
by the Phase 03 CI gate). The `es`, `it`, and `de` label keys exist (as empty
strings or stubs, flagged by the pseudolocale check) but are not translated.

The Phase 21 translation workflow (WP8) will fill all gaps. The exact count of
missing labels will be determined by running the `ResourceCompleteness` check
at the start of WP8-T1.

---

## No new icons in Phase 21

Phase 21 adds no new interactive surfaces, buttons, menus, or empty-state
illustrations. The design-audit pass (WP5) may identify status indicators that
need differentiated icons (for color-blind accessibility), in which case new
icon keys may be required. If that occurs:

- A new row is added to this manifest with status `⬜ to procure`.
- The Owner procurement request block below is updated with the new icon.
- Peter is asked to procure the premium PNG set before Phase 21 closes.

At baseline, no new icons are anticipated.

---

## Owner procurement request

**To: Peter Bamuhigire**
**For: Phase 21 — Accessibility, Full i18n & Comprehensive QA**

No new icons are expected to be procured in Phase 21. This is a verification
and remediation phase.

**Exception:** If the color-blind audit (WP5) identifies any status surface
where color is the sole information carrier and no suitable existing icon
differentiates the states, we will identify the specific icons needed and submit
an updated procurement request at that time (estimated 0–3 additional icons).

**Standing reminder:** All icons from Phases 03–20 must be in their
`✅ premium PNG wired` state before Phase 22 (store submission) begins.
Any icon still at `🟨 placeholder in use` is a Phase 22 release blocker.
Please review the MASTER-MANIFEST.md at the start of Phase 21 to identify
any outstanding procurements.
