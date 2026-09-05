# Current-worktree solution regression evidence — 2026-09-06

## Scope

This record covers the corrected native-shelf host, refreshed locked restore
metadata, and bounded worker-diagnostic remediation on commit
`5bef1cc209295b1da452ac342da64f92ef00b5075`. Existing user-owned dirty
catalogue/startup/image files remained unstaged.

## Command and result

```text
dotnet test OgmaLibrary.sln --configuration Release --no-restore --logger "console;verbosity=minimal" -m:1
```

Result: **1,125 passed, 0 failed, 0 skipped** — 41 architecture, 925 core,
and 159 UI tests. The run included a complete core suite and a complete UI
suite after lazy native-host activation and worker-boundary diagnostic
remediation were introduced.

## Gate interpretation

This closes the current automated regression and headless UI gates. It does
not establish physical WebView2/WKWebView rendering, WebGL2 capability,
assistive-technology behavior, reference-machine performance, signing,
installation, rollback, backup/restore, legal approval, or owner acceptance.
