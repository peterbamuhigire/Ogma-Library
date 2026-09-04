# Phase 27 Payload Preview Wiring Evidence

Date: 2026-09-04

## Implementation

`App` now composes the interactive desktop shell with `AvaloniaPreviewGate`.
The gateway's provider-neutral `IAiPreviewGate` registration remains the
fail-closed infrastructure default; the desktop app adds the modal gate with
the active shell window as owner. This keeps worker and test composition from
ever assuming a UI is available.

The bridge creates `PayloadPreviewViewModel` from the exact gateway preview,
waits for the user's Send, Remember-for-session, or Cancel decision, and
disposes the view model after the dialog closes.

## Verification

```powershell
dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration Debug --no-restore
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-build --filter "FullyQualifiedName~Phase02CompositionTests|FullyQualifiedName~PayloadPreviewViewModelTests" --verbosity normal -m:1
```

Results: desktop build 0 warnings/0 errors; focused test slice 8 passed, 0
failed.

## Scope boundary

This closes only the desktop payload-preview wiring subgate. Provider profile
editing, durable token/cost budgets, persisted connection health, retention and
erasure journeys, cloud-provider conformance, and physical UI walkthroughs
remain open.
