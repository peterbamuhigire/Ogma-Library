# Phase 18 Evidence - Appearance Preferences and Command Palette

Date: 2026-09-05

## Scope

This record closes the repository-verifiable appearance-preference and command
execution sub-gates for Phase 18. It does not claim physical screen-reader,
cross-platform, or contrast-snapshot acceptance.

## Implementation evidence

- `src/OgmaLibrary.Application/UserPreferences.cs` defines the validated theme,
  density, preference, and persistence contracts.
- `src/OgmaLibrary.Infrastructure/Ingestion/FileUserPreferencesService.cs`
  persists preferences through a temporary file and replace operation, with
  safe defaults for missing, corrupt, or unsupported values.
- `src/OgmaLibrary.App/App.axaml.cs` applies theme and density before the ready
  shell is exposed and reapplies them after a palette change.
- `MainShellViewModel` exposes a bounded searchable command set and executes
  only known command identifiers.
- `DesktopShellWindow` hosts the palette above all shell routes, provides an
  accessible query field, and supports Escape dismissal.

## Verification

- `dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration
  Release --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj
  --configuration Release --no-restore --filter
  "FullyQualifiedName~Phase18PreferencesTests|FullyQualifiedName~SearchBar_CtrlK_Opens"`:
  3 passed, 0 failed, 0 skipped.

## Residual gates

Full copy extraction, all-route inventory, contrast snapshots, physical
keyboard/screen-reader journeys, and cross-platform evidence remain open in the
canonical status ledger.
