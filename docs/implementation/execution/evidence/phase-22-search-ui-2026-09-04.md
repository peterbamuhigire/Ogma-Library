# Phase 22 Search UI Evidence

Date: 2026-09-04

## Result

The desktop search panel's local UI sub-gate is evidenced. Search results expose
source/match chips with accessible labels and icons, show confidence and
degraded/no-index state, select a deterministic result, and open that result in
the reader with its validated page target when present. The panel also exposes
the keyboard entry path used by the shell and Enter-to-open behavior.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --no-restore -p:BaseOutputPath=tmp/phase22-ui-build/ --filter "FullyQualifiedName~SearchViewModelTests" --logger "console;verbosity=minimal" --results-directory tmp/phase22-search-results
```

Result: 14 passed, 0 failed.

The suite covers semantic and exact fallback indicators, result match chips,
confidence labels, no-index actionable state, stale-result suppression,
selected-result reader navigation with a page hint, and the shell's Ctrl+K/
Escape search-panel keyboard route.

## Gate disposition

Closed: locally verifiable search-panel chip, state, selection, and keyboard
open-path sub-gate.

Still open: reference-machine performance confirmation and physical
assistive-technology walkthroughs.
