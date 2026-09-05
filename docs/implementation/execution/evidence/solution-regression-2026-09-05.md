# Current-Head Solution Regression Evidence

Date: 2026-09-05

Command:

```text
dotnet test OgmaLibrary.sln --configuration Release --no-restore --logger "console;verbosity=minimal"
```

Result: 1,093 passed, 0 failed, 0 skipped:

- 897 core tests
- 41 architecture tests
- 155 UI tests

The run included the current localization, spine scheduling, and sharded 3D
asset URI changes. An earlier concurrent run had one LAN catalogue P95 timing
outlier; the same test passed in isolation and the subsequent complete run
passed cleanly.
