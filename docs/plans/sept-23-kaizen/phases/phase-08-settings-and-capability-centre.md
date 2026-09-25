# Phase 08: Settings and capability centre

## 1. Header

| Field | Value |
|---|---|
| Wave | B, Core library |
| Size | 5–7 engineering days |
| Depends on | Phase 07 (a Settings destination exists in the new navigation) |
| Owner decisions | D-03 (multiple roots: this phase hosts the folder list UI), D-04 (cache location toggle) |
| Primary defects | K16, K17 |
| Requirements | UX-004 (P), UX-008 (P), META-002 (P), AI-001 (W), LIB-001 (W, single root), LIB-002 (P, exclusions have no UI), LIB-007 (P) |

## 2. Why this phase exists

A normal user cannot configure Ogma. Theme and density are reachable only through the hidden
command palette (Ctrl+Shift+P). Language cannot be chosen or persisted, so the French translation
(794 keys) is unreachable. Online metadata providers, the classroom Host and the 3D shelf turn on
**only** through the environment variables `OGMA_ENABLE_METADATA_PROVIDERS`,
`OGMA_ENABLE_CLASSROOM_HOST` and `OGMA_ENABLE_3D_SHELF`
(`src/OgmaLibrary.App/Configuration/OgmaRuntimeOptions.cs:33-64`), read once at start-up. The
audit measured that setting `OGMA_LIBRARY_ROOT` does not start a scan, and that `OGMA_ENABLE_3D_SHELF`
changes only a diagnostic string (K17). The UIA tree of the running app contains no settings route
at all (K16).

This phase creates one Settings surface where every user-controllable capability lives, persisted
per user. Environment variables stay only as administrator and test overrides.

## 3. Objectives and exit criteria

1. A Settings destination, reachable from navigation and from the command palette, with sections:
   **Library**, **Appearance**, **Language**, **Online services**, **Reading**, **Search and AI**,
   **Classroom**, **Privacy** (the mount point for Phase 15) and **Diagnostics and about**.
2. Every setting persists across restarts in `user-preferences.json` (atomic write), and changes
   apply live without a restart. Settings that genuinely need a restart say so and offer *Restart now*.
3. Language switch en ↔ fr applies at runtime and persists. The first launch follows the OS
   culture when it is supported.
4. Metadata providers, the 3D shelf and the classroom Host are enabled from Settings. The env vars
   become overrides, and the UI shows "Managed by administrator" when an override is active.
5. The library folder list (D-03) shows each root's status (available, missing, scanning),
   excluded subfolders, *Rescan now*, *Add folder* and *Remove*.
6. Diagnostics shows version, data location, worker status, the capability probe in plain words,
   *Open log folder* and *Export diagnostics* (the Phase 02 log bundle).
7. The golden journey "change theme, language and density, then restart" passes in the
   real-window harness, with every value preserved.

8. The screenshot set shows every Settings section in Light and Dark at 1280×800.
9. `CLAUDE.md` and the developer guide list the env vars as *overrides*, not the way to enable features.

## 4. Skills to load before starting

- `C:\wamp64\www\design-system-skills\README.md` and `CLAUDE.md` (router, banned-font doctrine)
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\form-ux-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\navigation-and-information-architecture\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\error-empty-and-system-messaging\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\internationalization-and-rtl-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\governance\design-quality-gate.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`

**Typeface decision (restated):** Settings uses the existing licensed stack. Public Sans is the
body and control face, Spectral is for section titles only, and JetBrains Mono is for paths and
version strings. No new fonts.

## 5. Scope

**In scope:** the Settings view and view model, preference persistence, runtime culture switching,
the capability-flag bridge (env override versus user setting), the library folder list UI,
diagnostics, and removing the env-only gating for the three flags.

**Out of scope:** the Privacy Center content (Phase 15), the classroom Host configuration forms
(Phase 19), the visual redesign of controls (Phase 09 supplies the variants; this phase uses
current styles and must not add new hard-coded colours), and OCR language packs (Phase 17).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 8.1 | Extend `UserPreferences` with `Culture`, `ThemeVariant` (Light/Dark/System), `Density`, `EnableMetadataProviders`, `EnableThreeDimensionalShelf`, `EnableClassroomHost`, `ReaderDefaults` (zoom mode, layout), and `SchemaVersion` with tolerant migration. | `src/OgmaLibrary.Application/UserPreferences.cs`, `src/OgmaLibrary.Infrastructure/Ingestion/FileUserPreferencesService.cs` | Unit: round-trip, unknown-field tolerance, corrupt-file fallback, atomic write (temp+move) retained. |
| 8.2 | Introduce `ICapabilitySettings`: effective value = env override if set, otherwise the user preference, otherwise the default. Expose `IsManagedByEnvironment` per flag. Replace direct reads of `OgmaRuntimeOptions.Enable*`. | `src/OgmaLibrary.App/Configuration/OgmaRuntimeOptions.cs`, `src/OgmaLibrary.App/Composition/ShellModule.cs` (the `HostSharingViewModel` gating at `:144`), `MetadataServiceExtensions.cs:45-82` | Unit matrix: env unset/true/false × preference true/false. |
| 8.3 | Make gated modules toggleable at runtime. Metadata providers: register the gateway always and gate calls on the effective flag. Classroom Host: always construct `HostSharingViewModel` and gate start. 3D: route the flag to the Phase 18 capability check. | `ShellModule.cs`, metadata and LAN module registrations | Toggling in Settings changes behaviour without a restart (headless UI test); the `CompositionRoot` tests are updated deliberately, not deleted. |
| 8.4 | Runtime culture: persist `Culture`; call `ILocalizationService.SetCulture` from Settings; detect the OS culture on first run; delete the dead `MainWindowViewModel` path (coordinate with Phase 07). | `src/OgmaLibrary.Infrastructure/Localization/InMemoryLocalizationService.cs`, `src/OgmaLibrary.App/App.axaml.cs:110-144` | Real-window: switch to French, and the navigation, catalogue labels and status bar change; restart keeps French. |
| 8.5 | `SettingsView.axaml` with a left section list and right content, using existing tokens only. Every control has a label, a help line and an accessible name. | `src/OgmaLibrary.App/Views/Settings/`, `ViewModels/Settings/` | Design gate (heuristic): no hard-coded `FontSize`/`Foreground`; tab order follows visual order. |
| 8.6 | Library section: the root list via `ILibraryRootService` (add, remove, enable, relink), excluded folders via `ILibrarySettingsService.SetExcludedFoldersAsync`, and scan-health summary via `IScanHealthService` (LIB-007, currently 0 App references). *Rescan now* calls the Phase 05 command. | `src/OgmaLibrary.Application/Ingestion/ILibrarySettingsService.cs`, `IScanHealthService.cs` | Real-window: add a second folder and both remain available (depends on Phase 05 D-03 outcome). |
| 8.7 | Appearance: Light/Dark/System theme, density, and a reduced-motion preference. The command-palette entries call the same commands. | `MainShellViewModel.cs:648-660` | The palette and Settings stay consistent (single source of truth). |
| 8.8 | Online services: a metadata provider toggle with a plain-language disclosure (what is sent: ISBN, title, author; which hosts), the attribution links already built in Phase 13 of Aug-39, and *Test connection*. | Metadata gateway | Privacy text reviewed against CTRL items; the network stays off until the user opts in. |
| 8.9 | Diagnostics: version, commit, data directory, library cache location (D-04), worker path and status, a plain-language capability probe (replace `phase_27_required` wording in `StartupTasks.cs`), *Open logs*, *Export diagnostics*. | `src/OgmaLibrary.App/Startup/StartupTasks.cs` | No raw exception text is shown; the export contains no book content. |
| 8.10 | Remove or hide dead entries once Settings owns them: the always-visible *Sharing* button becomes Settings → Classroom; *AI Smart Search* is shown only in classroom client mode (K17). | `CatalogueShellView.axaml:277-284`, `MainShellViewModel.cs:275` | UIA dump in standalone mode has no Sharing or AI Smart Search entry. |
| 8.11 | Localise every new string (en, fr) and add keys to the localization completeness test. | Localization resources | The key-parity test passes. |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Features unreachable (K16) | Capability flags were designed as deployment configuration, not user choices | Preference-backed flags with env override | Users can enable any shipped capability without a terminal | Capabilities user-toggleable: 0 of 3 → 3 of 3 | Real-window journey plus screenshots | Enabling a network feature by mistake | Defaults stay off; a disclosure dialog appears on first enable |
| Language fixed to English | `SetCulture` has no live caller | Settings plus persisted culture | French users can use the app | Runtime languages: 1 → 2 | FR screenshots of 5 key screens | Missing keys show raw ids | Fallback to English per key; parity test |
| Hidden theme and density | Palette-only commands | Visible Appearance section | Discoverability | Clicks to change theme: palette-only → 2 clicks | UIA path | None | Revert the view only |
| Dead navigation entries (K17) | Views always rendered regardless of mode | Mode-aware visibility | Fewer dead ends | Dead entries in standalone: 2 → 0 | UIA dump | Hiding something a tester needs | The env override still shows it |

## 8. Test plan

- **Unit:** preference serialisation and migration; capability resolution matrix; culture persistence.
- **Headless UI (`OgmaLibrary.Tests.Ui`):** the Settings view renders every section; toggles
  update view-model state; the French culture switch re-renders labels; accessible names exist on
  every interactive control.
- **Real-window (Phase 01 harness):** J-SET-1 change theme, density and language, then restart and
  verify; J-SET-2 enable metadata providers, enrich a book, disable, and verify no network call
  (mock provider endpoint in the harness); J-SET-3 add a second library folder and rescan.
- **Negative:** corrupt `user-preferences.json` → defaults plus a notice, no crash; env override
  active → the control is disabled and shows "Managed by administrator".

## 9. Acceptance commands

```powershell
./scripts/Test-RequirementAccountability.ps1
dotnet restore OgmaLibrary.sln --locked-mode
dotnet format OgmaLibrary.sln --verify-no-changes --no-restore
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test OgmaLibrary.sln --configuration Release --no-build --filter "Category!=Performance" -m:1
# Phase 01 harness, journeys tagged Settings
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Settings -Sizes 1280x800,1920x1080
```

(The E2E path is defined by Phase 01. If Phase 01 chose a different location, use that.)

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Live metadata provider behaviour and terms | Owner / Phase 23 legal review | The toggle ships default-off with disclosure |
| French copy quality (native review) | Owner | Machine-consistent keys only until reviewed (Phase 22) |
| macOS Settings rendering | Phase 27 | Windows evidence only |

## 11. Risks and mitigations

- **Composition churn.** Always constructing previously gated view models can surface latent
  start-up exceptions. Mitigation: Phase 02 global handlers first; construct lazily on first navigation.
- **Test assertions that "AI is disabled".** Two composition and two architecture tests assert the
  disabled state (inventory §5). Update them deliberately and record why in the commit.
- **Restart-only settings.** Keep them to the minimum and label them.

## 12. Execution prompt

```text
You are implementing Phase 08 (Settings and capability centre) of the Sept-23 Kaizen plan in
C:\wamp64\www\Ogma-Library. Read these first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md (invariants: window is the oracle, NOT ASSESSED is never a pass)
4. docs/plans/sept-23-kaizen/phases/phase-08-settings-and-capability-centre.md (this file)
5. docs/plans/sept-23-kaizen/03-defect-register.md rows K16, K17
Then read the skills listed in section 4 (design-system-skills README/CLAUDE.md first).
Work in slices: A) preferences model + capability resolver (8.1–8.2) with unit tests;
B) runtime toggles + culture (8.3–8.4); C) Settings view sections (8.5–8.9); D) dead entries + l10n (8.10–8.11).
After each slice run the section 9 commands and the Settings journeys in the real window; save
before/after screenshots under docs/implementation/execution/evidence/sept-23-kaizen/phase-08/.
Do not add hard-coded colours, font sizes or English strings. Conventional Commits with DCO
sign-off. Record NOT ASSESSED items with owners in the completion record
docs/implementation/execution/phase-sept23-08-completion.md. Stop and report if a slice's
real-window journey fails after two fix attempts.
```
