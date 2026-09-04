# Phase 21 — Independent Split View Evidence

Date: 2026-09-04

## Scope

This evidence closes the repository-level split-view placeholder gate. It does
not claim physical crash recovery, assistive-technology, cross-platform
performance, coordinate-version fallback, or system-viewer evidence.

## Implemented controls

| Control | Evidence |
| --- | --- |
| Two reader sessions | The shell creates the primary reader and a separate reference `ReaderViewModel`; `SplitViewViewModel` exposes both sessions to two embedded `ReaderView` controls. |
| Independent reference opening | The reference pane accepts a bounded non-empty book ID and calls only the right reader’s `OpenAsync`; the primary session is not reused for the reference action. |
| Existing route remains safe | The compatibility constructor still supports the headless route test and the split surface renders without a live reader session. |
| Accessible reference action | The reference input and action have bound automation names and the action is disabled until a book ID is entered. |

## Verification

```text
dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration Debug --no-restore
```

Result: **0 warnings, 0 errors**.

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~SplitView_Route_Exists_ShowsIndependentReferenceEntry" --logger "console;verbosity=minimal"
```

Result: **1 passed, 0 failed, 0 skipped**.

## Remaining gates

- Coordinate-version fallback, physical crash/accessibility, cross-platform
  performance, and system-viewer actions: **NOT ASSESSED**.
