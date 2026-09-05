# Current-worktree solution regression evidence

Date: 2026-09-05

## Scope

This record captures current-worktree automated verification after the latest
Phase 39 evidence commits and while pre-existing user-owned catalogue/startup
changes remain unstaged. It is not a signed release-acceptance record.

## Commands and results

### Core suite

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal" -m:1
```

Result: **925 passed, 0 failed, 0 skipped**, 15m30s test duration. The command
completed successfully; the terminal interrupt was delivered after xUnit had
already reported completion.

### Architecture suite

```text
dotnet test tests/OgmaLibrary.Tests.Architecture/OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore --logger "console;verbosity=minimal" -m:1
```

Result: **41 passed, 0 failed, 0 skipped**, 9s duration.

### Release UI suite

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --logger "console;verbosity=minimal" -m:1
```

Result: **159 passed, 0 failed, 0 skipped**, 37s duration.

## Aggregate result

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Core | 925 | 0 | 0 |
| Architecture | 41 | 0 | 0 |
| Release UI | 159 | 0 | 0 |
| **Total** | **1,125** | **0** | **0** |

The checks compiled the current production projects and exercised the
concurrent user-owned startup and catalogue worktree changes. No user-owned
files were staged or modified by this verification.

## Gate interpretation

This closes only the current automated regression gate. It does not establish
physical accessibility, cross-platform, reference-machine, signing,
installation, performance/soak, rollback, backup/restore, legal, security
approval, or owner-acceptance evidence. Those remain `NOT ASSESSED` or open in
their authoritative phase records.
