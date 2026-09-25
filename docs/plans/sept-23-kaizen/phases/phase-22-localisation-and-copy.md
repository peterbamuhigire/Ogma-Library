# Phase 22: Localisation and product copy

## 1. Header

| Field | Value |
|---|---|
| Wave | F, Quality attributes |
| Size | 4–6 engineering days, plus French review |
| Depends on | Phase 08 (the Language setting exists), Phase 19 (the Sharing/Host dashboard is rebuilt; its literals are removed there and verified here), Phase 02 (typed errors replace raw exception text) |
| Owner decisions | None new. The owner, or a named French reviewer, approves French terminology. |
| Primary defects | K16, K61, K81 |
| Requirements | UX-004 (P, runtime English/French switch), UX-002 (P, states), CLAUDE.md rule "Do not hard-code user-facing strings" |

## 2. Why this phase exists

Ogma ships an English and a French dictionary of about 794 keys each (inventory), but a French-speaking
user can never see French:

- `InMemoryLocalizationService` always starts in English
  (`src/OgmaLibrary.Infrastructure/Localization/InMemoryLocalizationService.cs:1636-1639`), does not detect
  the OS culture, and nothing in the live shell calls `SetCulture` (`:1655`). The only caller is the dead
  `MainWindowViewModel`. The language is not persisted in `FileUserPreferencesService`, which stores only
  theme and density (K16).
- English literals bypass the dictionaries in live code:
  - `SharingSettingsView.axaml` and `HostSharingViewModel.cs:37-99` (about 50 strings, K61; rebuilt in Phase 19);
  - `MainShellViewModel.cs:219-221` shows `$"Catalogue load failed: {ex.Message}"`;
  - `App.axaml.cs:105` hard-codes the startup failure sentence;
  - `LocalEvidenceAnswerPipeline.cs:65-70` hard-codes answer text (Phase 16 fixes it);
  - 32 occurrences of `ex.Message` in `src/OgmaLibrary.App` can put raw, English, technical exception
    text in front of users (K81).
- The detail panel shows a raw SHA-256 to ordinary users (K81), and product copy mixes internal terms
  ("Relocation reviews", "Index Manager", "Semantic search active") with user language.

The dictionaries live in 1,680 lines of C#. That works, but it prevents translators from working without
touching code, and it hides missing-key drift.

## 3. Objectives and exit criteria

1. **Runtime switch.** Settings → Language offers *System default*, *English* and *Français*. Switching
   applies immediately to every open view (including the reader, dialogs and the palette) without a
   restart, and persists in `user-preferences.json`.
2. **OS culture detection.** On first launch, a supported OS UI culture (`fr-*`) selects French; otherwise
   English. *System default* re-evaluates at each start.
3. **Zero hard-coded user-facing strings** in `src/OgmaLibrary.App` views and view models, enforced by a
   CI scan (AXAML `Text`/`Content`/`Header`/`Watermark`/`ToolTip.Tip`/`AutomationProperties.Name`
   attributes with literal letters, and C# string literals assigned to UI-bound properties, with an allowlist
   for symbols and brand names).
4. **No raw exception text.** Every user-visible error is a localised message chosen from a typed failure
   code (Phase 02 mapping). `ex.Message` appears only in logs. The CI scan fails on `ex.Message` flowing
   into status or dialog properties.
5. **Key parity.** en and fr have identical key sets (currently one key is missing). A unit test enforces
   parity and placeholder consistency (`{0}` counts match).
6. **Pseudo-localisation.** The existing `qps-ploc` pseudo dictionary (`InMemoryLocalizationService.cs:1634,1658`)
   is selectable in a developer setting. The harness runs the golden journeys in pseudo-locale at 1280×800
   and fails on truncated or clipped text in primary controls and on untranslated (non-pseudo) strings.
7. **Terminology glossary.** `docs/localisation/glossary.md` defines each product term in en and fr
   (library, library folder, shelf, collection, catalogue, book detail, reading memory, annotation layer,
   Advisor, reading plan, classroom Host, join code, published, offline copy, OCR, searchable text), with
   banned internal terms and their replacements.
8. **Plain-language copy review.** All states (empty, loading, error, success) and primary labels are
   reviewed against the design engine's voice and microcopy guidance. Internal terms are replaced (for
   example *Index Manager* → *Search index*, *Relocation reviews* → *Moved files to confirm*, subject to the
   glossary). Technical identifiers (hashes, IDs) move behind a *Technical details* disclosure.
9. **French review.** A named reviewer approves the French strings and the glossary, and the approval is
   recorded in the completion record.

## 4. Skills to load before starting

- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\internationalization-and-rtl-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\ux-writing-and-microcopy\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\voice-tone-and-content-style-guide\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\error-empty-and-system-messaging\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`
- `C:\wamp64\www\srs-skills\08-end-user-documentation\` (glob `SKILL.md`; the terminology must match the Phase 28 user guide)

**Typeface decision:** no change. Verify that Spectral, Public Sans and JetBrains Mono cover the French
diacritics and the Unicode seen in the audit corpus (`ũ`, `ĩ`, `É`, `—`). Missing glyphs are a defect.

## 5. Scope

**In scope:** culture switching and persistence, OS detection, literal removal, the typed-error message
catalogue, key parity and placeholder tests, the pseudo-locale journey run, the glossary, the copy review,
externalising the dictionaries to resource files, and the French review.

**Out of scope:** adding more languages (the architecture must make it cheap, but only en and fr ship),
right-to-left layout (documented as not supported in v1), and translating user documentation (Phase 28
owns the docs and follows this glossary).

## 6. Work breakdown

1. **Persisted language preference.** Add `Language` (`system`, `en`, `fr`) to user preferences with an
   atomic write (the existing pattern). Apply it at start before the first view renders.
2. **OS detection.** Map `CultureInfo.CurrentUICulture` to a supported culture for `system`.
3. **Live switching.** Ensure every view model raises property changes on `CultureChanged` (the pattern
   exists in `SearchViewModel`). Audit the view models that cache strings in readonly fields (for example
   `HostSharingViewModel.cs:37-43`) and convert them to computed properties.
4. **Externalise dictionaries.** Move the en, fr and pseudo dictionaries to resource files (`.resx` or JSON
   under `src/OgmaLibrary.Infrastructure/Localization/Resources/`) loaded by the same service, keeping the
   `ILocalizationService` contract. This lets translators edit without C# changes. The key set is frozen by
   a test.
5. **Literal sweep.** Remove literals in AXAML and view models across all views. Phase 19 covers Sharing;
   this phase verifies it and covers the rest, including `MainShellViewModel.cs:220` and
   `App.axaml.cs:105`.
6. **Error catalogue.** Define a `UserFacingError` code → key mapping and replace the 32 `ex.Message` UI
   usages with it. Log the exception details through the Phase 02 logger.
7. **CI string scanner.** Add `scripts/Test-NoHardCodedStrings.ps1` (AXAML attribute scan plus a Roslyn
   analyzer or regex for UI-bound literals, with an allowlist file) to the verification list in `CLAUDE.md`.
8. **Parity and placeholder tests.** A unit test compares the en, fr and pseudo key sets and placeholder
   counts.
9. **Pseudo-locale journeys.** Run the harness in `qps-ploc` with the UIA audit (Phase 21) and a
   clipping check (text element desired width > arranged width on primary controls). Fix layouts.
10. **Glossary and copy review.** Write the glossary, review every screen's copy with the microcopy skill,
    apply the renames through keys, and hide technical identifiers behind a disclosure.
11. **French review.** Export en/fr pairs to a review sheet, obtain approval, and apply the corrections.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| French unreachable | No live `SetCulture` caller or persistence | Settings switch, persistence, OS detection | French users adopt Ogma | fr reachable: no → yes, persisted | Harness fr screenshots | Partial live refresh | Offer *Restart to apply* fallback |
| English literals | Views written without keys | Sweep plus CI scanner | Drift stops | Literal count ≈50+ → 0 | Scanner report | False positives | Allowlist file |
| Raw exception text | `ex.Message` in UI | Typed error catalogue | Users get actionable messages | UI `ex.Message` uses 32 → 0 | Grep gate | Lost detail | Details in log plus *Copy details* |
| Truncation in fr | Fixed widths | Pseudo-locale journeys | Layout survives 30–40 % expansion | Clipped primary controls: unknown → 0 | Pseudo run report | Layout churn | Wrap and tooltips |
| Jargon | No glossary | Glossary plus copy review | Faster task completion | Internal terms on primary UI: several → 0 | Copy review log | Doc mismatch | Phase 28 uses the glossary |

## 8. Test plan

- **Unit:** key parity; placeholder consistency; preference round-trip; OS culture mapping; error-code
  mapping completeness (every code has en and fr).
- **Headless UI:** switch en → fr → en and assert representative strings on each major view, with no
  restart; `CultureChanged` refresh on cached-string view models.
- **Real-window:** golden journeys in fr at 1280×800; pseudo-locale journeys with the clipping check.
- **Static:** `Test-NoHardCodedStrings.ps1`; the `ex.Message` UI-flow grep gate.
- **Human:** the French review sign-off.

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
./scripts/Test-NoHardCodedStrings.ps1
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "FullyQualifiedName~Localization|FullyQualifiedName~UserFacingError"
dotnet test tests/OgmaLibrary.Tests.Ui --configuration Release --no-build --filter "FullyQualifiedName~Culture|FullyQualifiedName~Language"
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -All -Culture fr -Sizes 1280x800
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -All -Culture qps-ploc -ClippingCheck
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Native French review | Owner-named reviewer | French ships labelled "beta" |
| Screen-reader behaviour in French | Phase 21 protocol, fr smoke run | Recorded as a limitation |
| macOS locale detection | Phase 27 | Verify on Mac hardware |

## 11. Risks and mitigations

- **The live switch misses cached strings.** Headless tests per view plus the pseudo-locale journey
  detect stale English.
- **Externalising dictionaries breaks lookups.** Key-set freeze test; load failures fall back to embedded
  English and are logged.
- **Renamed terms confuse existing users.** Record renames in the changelog and use the glossary in help.

## 12. Execution prompt

```
## Prompt 22 - Make Ogma bilingual at runtime and speak plainly
You are implementing Phase 22 in C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/phases/phase-22-localisation-and-copy.md
5. docs/plans/sept-23-kaizen/03-defect-register.md (K16, K61, K81)
Read the section 4 SKILL.md files. Confirm Phases 02, 08 and 19 are COMPLETE.
Order: 1-3 (preference, detection, live switch), 7-8 (scanner and parity tests first, to measure), 5-6 (sweep and error catalogue), 4 (externalise), 9 (pseudo run), 10-11 (glossary, copy, review).
Keep ILocalizationService stable; add languages only through resources.
Run section 9 commands. Record the French review as NOT ASSESSED until a named reviewer signs.
Write docs/implementation/execution/phase-sept23-22-completion.md and update the README status register.
```
