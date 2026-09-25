# Sept-23 Phase 08 completion: settings and capability centre

Status: **IMPLEMENTED; real-window proof pending** (2026-09-25). Code, unit, headless UI,
architecture and gate checks below pass. The real-window journeys were written but not run: the
coordinator suspended every real-window step while the owner was at the desk (the harness pins
the app window topmost). They are listed under NOT ASSESSED for re-dispatch.
Branch: `worktree-agent-a15a4c9aaf6c62d11` (lane worktree, not pushed), from `8950899`.
Rollback point: `8950899` (the last commit before any Phase 08 change).
Plan: [phase-08](../../plans/sept-23-kaizen/phases/phase-08-settings-and-capability-centre.md).
Defects: K16 (fixed in code), K17 (the Settings part; dead entries were removed in Phase 07).

## Typeface decision

Unchanged and compliant with the design engine: **Public Sans** (theme body: labels, help lines,
radio buttons, check boxes, buttons), **Spectral** (`ogma-display`: the section title and the
Diagnostics sub-heading only) and **JetBrains Mono** (`ogma-mono`: version, folder paths and the
environment-variable name in "managed by your administrator" lines). No new face and no banned
font. Every colour, size and radius in `SettingsView.axaml` is a token (`Brush.*`,
`Type.Size.*`, `Radius.*`); a headless guard fails on a numeric `FontSize`, a hex colour, a
`FontFamily` or literal `Text`/`Content` in that file.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 8.1 Preferences | `UserPreferences` adds `Culture`, `EnableMetadataProviders`, `EnableThreeDimensionalShelf`, `EnableClassroomHost`, `SchemaVersion` (2). The store migrates schema-1 files, ignores unknown fields, normalises the culture (`fr-CA` → `fr`, unsupported → system), keeps atomic temp+move writes, and on a damaged file returns defaults, keeps `user-preferences.json.corrupt` and raises a start-up notice | `d7e8be6` |
| 8.2 Capability resolver | `ICapabilitySettings` (Application) implemented by `RuntimeCapabilityState`: effective = `OGMA_ENABLE_*` override → user preference → off; `IsManagedByEnvironment` and the variable name per capability. `OgmaRuntimeOptions.Enable*` are now nullable overrides (unset = the user decides). One DI singleton backs navigation, Settings, the start-up probe and the metadata gate | `d7e8be6` |
| 8.3 Runtime toggles | Metadata provider adapters are always registered; `MetadataProviderAggregator` and `MetadataProviderGateway` consult `IMetadataProviderPolicy` on every lookup (no provider call and no catalogue write when off). `HostSharingViewModel` always exists; the capability decides reachability and switching it off stops a running Host. The 3D view follows `IsShelf3DAvailable` (Phase 18 adds the WebGL check). `CompositionRoot` and architecture tests updated deliberately (see below) | `d7e8be6` |
| 8.4 Runtime culture | `UserPreferencesController` applies the persisted culture on load and on change via `ILocalizationService.SetCulture`; first run follows the OS UI culture when it is English or French, otherwise English; the choice "Use the system language" is stored as null. The dead `MainWindowViewModel` path was already removed in Phases 05/07 | `d7e8be6` |
| 8.5 Settings view | `Views/Settings/SettingsView.axaml` + `ViewModels/Settings/SettingsViewModel.cs`: section list (ListBox, arrow keys) and one section on the right; every control has a label, a help line and an accessible name; tab order is list → section | `076ef88` |
| 8.6 Library | Hosts the Phase 05 `LibraryFoldersPanelView` (add, rescan, include, remove, needs attention). The Library drawer stays as a shortcut to the same view model | `076ef88` |
| 8.7 Appearance | Light / Dark / Match the system and Comfortable / Compact radio groups. The palette's *Cycle theme* / *Toggle density* and Settings call the same controller (headless test proves they stay consistent) | `d7e8be6`, `076ef88` |
| 8.8 Online services | *Look up book details online* switch (default off) with the disclosure: ISBN or title and author go to Google Books (`www.googleapis.com`) and Open Library (`openlibrary.org`); covers are downloaded; PDFs, text, notes and history are never sent; no connection while off | `076ef88` |
| 8.9 Diagnostics | Version, data folder, covers and caches folder (D-04), log folder (JetBrains Mono), *Open data folder*, *Open log folder*, *Export diagnostics* (the Phase 02 redacted bundle), and the capability probe in plain words. `StartupCapabilityProbe` now reports effective values with plain wording (`phase_27_required` replaced by `not_configured`) | `d7e8be6`, `076ef88` |
| 8.10 Dead entries | Already done by Phase 07 (Sharing and AI Smart Search hidden in standalone; `DeadEntries` journey). Settings → Optional features → Classroom Host is now the way to reach the Classroom destination | — |
| 8.11 Localisation | 66 new keys in en and fr (sections, controls, disclosure, managed text, palette commands, the preferences notice); the existing palette label test covers the 8 new `settings.*` commands in both languages | `076ef88` |

Additional: AI and privacy states the real state ("AI features are off. Ogma is working locally
only…") and is the mount point for the Phase 15 Privacy Center; Text recognition states current
behaviour and exposes `SettingsViewModel.TextRecognitionOptions`, the extension point for the
Phase 17 OCR policy (no fake toggle: Phase 17's setting type was not on `main`). The Advisor's
*Set up in Settings* route opens AI and privacy; `classroom`/`shelf3d` routes open Optional
features.

## Tests

- Unit (`tests/OgmaLibrary.Tests/App/Sept23Phase08SettingsTests.cs`, 26): every field round-trips
  at schema 2 with no temp file left; schema-1 file plus unknown fields migrates; culture
  normalisation (4 cases); corrupt file → defaults, `.corrupt` copy, recovery flag, next save
  clean; the resolution matrix env unset/true/false × preference true/false for all three
  capabilities (6 × 3); `Changed` only on an effective change; env parsing (unset → null); a
  closed metadata policy makes **0 provider calls** through both aggregator and gateway and opens
  without a restart; default composition registers the adapters behind a closed gate; an env
  override is managed and not changed by preferences; OS-culture first run (fr-FR → fr, en-GB →
  en, sw-KE → en); a French choice applies live and survives a restart.
- Headless UI (`tests/OgmaLibrary.Tests.Ui/SettingsViewTests.cs`, 7): all 8 sections at 1280×800
  and 1920×1080 with a named title and a non-empty, non-type, non-key accessible name on every
  visible button, radio button, check box and list item; Dark + Compact + Classroom Host apply
  live and persist across a restart, and the palette theme command stays in step; Français
  re-renders the section list, section title, form labels and the rail; an `OGMA_ENABLE_3D_SHELF`
  override renders a disabled, checked switch with the variable named and ignores changes;
  every section is in the palette and route aliases select the right section; AXAML token guard.
- Updated deliberately: `Phase02CompositionTests` (default and explicit matrices) and
  `Architecture_CompositionModules_AreOrderedAndExternallyDisabledByDefault` asserted "no
  `IMetadataProvider` registered by default". Registration no longer expresses the default;
  they now assert the adapters are registered and `IMetadataProviderPolicy` resolves to **off**
  with no preference and no override (and **on** with the override), which is the new
  enforcement point for "no external traffic by default".
- Fail-first: the new resolver, policy and controller APIs did not exist, so the unit and
  headless tests fail to compile against `8950899`; the E2E oracle for the Navigation journey
  (`Settings.Privacy.Status`, `Settings.Sections`) likewise does not exist on `8950899`.
- Real window (written, **not run**): `tests/OgmaLibrary.Tests.E2E/Journeys/SettingsTests.cs`,
  journey and tag `Settings`: (1) Dark + Compact + Français applied from Settings change the rail
  labels at once, are written to `user-preferences.json` and are restored after a relaunch;
  (2) with `OGMA_ENABLE_3D_SHELF=1` the 3D switch is checked, disabled and its visible
  explanation names the variable while the Classroom Host switch stays enabled.

## Gates (final commit)

`dotnet restore --locked-mode` pass; `dotnet format --verify-no-changes` exit 0 (CRLF per
`.gitattributes`); Release build **0 warnings, 0 errors**; `Test-RequirementAccountability.ps1`
pass (101 FRs, 29 NFRs, 32 controls); `scripts/Test-Fast.ps1` **1,373 passed, 0 failed**
(Architecture 52, core 1,111, UI 210; +33 over `main`'s 1,340).

## Measured and code results

| Check | Before (`8950899`) | After |
|---|---|---|
| Capabilities a user can switch on without a terminal | 0 of 3 | 3 of 3 (CODE + headless) |
| Runtime interface languages reachable | 1 (English) | 2, live, persisted (headless) |
| Clicks to change theme | palette only | 2 (Settings → Appearance → Dark) |
| Settings needing a restart | — | 0 (every setting applies live, so no *Restart now* is offered) |
| Env override visible to the user | no | disabled switch naming the variable (headless) |
| Provider calls with the switch off | registration-gated only | 0, call-time gate (unit) |

## Deviations and NOT ASSESSED

| Item | Owner | Note |
|---|---|---|
| Real-window runs: `Invoke-GoldenJourneys.ps1 -Journey G1,G7,Navigation,Settings -Sizes 1280x800,1920x1080` | Coordinator re-dispatch | Suspended by the coordinator (owner at the desk; topmost harness). No real-window result is claimed |
| Screenshots (every section, Light and Dark, 1280×800) to `evidence/sept-23-kaizen/phase-08/` | Coordinator re-dispatch | Taken with the journeys above |
| J-SET-2 (enable providers, enrich, disable, no network call against a mock endpoint) | Phase 11 (G9) / harness | The call-time gate is proven by unit test; the harness has no mock provider endpoint yet |
| J-SET-3 (add a second folder from Settings and rescan) | Re-dispatch | Same view model as the Phase 05 drawer, already proven there; Settings copy not driven in the real window yet |
| *Test connection* for metadata providers | Phase 11 | Omitted rather than faked; needs a provider health call behind the gate |
| Excluded sub-folders editor and scan-health summary (LIB-002, LIB-007) | Phase 10 | Folder list, rescan, show/hide per folder and needs-attention are present; per-root excluded paths and the `IScanHealthService` summary are not yet in the panel |
| Reading defaults (zoom mode, layout) | Phase 12 | Not persisted here; no Reading section is shown rather than an inert one |
| Reduced-motion preference | Phase 21 | Not added: nothing consumes it yet, and a switch with no effect would be fake |
| Worker path and status in Diagnostics | Phase 24 | The probe reports the PDF worker's availability; path/live status not shown |
| "First enable" disclosure dialog | Phase 23 | The disclosure is always visible beside the default-off switch instead of a modal |
| French copy native review | Owner / Phase 22 | Machine-consistent keys only; a pre-existing unaccented "Bibliotheque" (`Navigation.Library`, fr) was left for Phase 22 |
| Screen-reader announcement of section changes; macOS rendering | Phases 21, 27 | Not measured |
