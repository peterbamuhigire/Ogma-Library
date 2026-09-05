# Current-worktree solution regression evidence — 2026-09-06

## Scope

This record covers the corrected native-shelf host and refreshed locked
restore metadata on the current `main` worktree. Existing user-owned dirty
catalogue/startup/image files remained unstaged.

## Command and result

```text
dotnet test OgmaLibrary.sln --configuration Release --no-build --logger "console;verbosity=minimal" -m:1
```

Result: **1,125 passed, 0 failed, 0 skipped** — 41 architecture, 925 core,
and 159 UI tests. The run included a complete core suite and a complete UI
suite after lazy native-host activation was introduced.

## Gate interpretation

This closes the current automated regression and headless UI gates. It does
not establish physical WebView2/WKWebView rendering, WebGL2 capability,
assistive-technology behavior, reference-machine performance, signing,
installation, rollback, backup/restore, legal approval, or owner acceptance.
