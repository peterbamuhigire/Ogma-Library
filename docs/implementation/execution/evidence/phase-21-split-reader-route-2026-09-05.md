# Phase 21 split-reader route evidence — 2026-09-05

## Change

The desktop split-reader route already renders two independent
`ReaderViewModel` sessions. Its remaining repository-only defect was stale
“scaffold” language in the route method, comments, and accessibility label.
The production route now uses `OpenSplitView`; the former
`OpenSplitViewScaffold` method remains a compatibility forwarding alias.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShellReaderNavigationTests" --logger "console;verbosity=minimal" -m:1
```

Result: **14 passed, 0 failed, 0 skipped**.

This closes the terminology/route-consistency subgate only. Platform reader,
physical accessibility, crash-recovery, and cross-platform performance gates
remain open in the Phase 21 progress record.
