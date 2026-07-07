# Phase 04 SAST and Secret Scan Report

Date: 2026-07-07
Stack: .NET 10 / Avalonia / SQLite / ASP.NET Core LAN Host

## Commands

| Command | Result | Notes |
| --- | --- | --- |
| `dotnet list OgmaLibrary.sln package --vulnerable --include-transitive` | Pass | No vulnerable packages reported for 10 solution projects. |
| `dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn --verbosity minimal` | Pass | Built-in .NET analyzer gate produced no required changes. |
| High-confidence PowerShell secret-pattern scan over `src` and `.github` | Pass | No source/workflow matches for live OpenAI-style keys, GitHub tokens, Google API keys, or private-key headers. |

## CI Gate

The three commands above are now wired in `.github/workflows/ci.yml` between
Release build and tests on both Windows and macOS runners.

## Findings

| Severity | Count | Disposition |
| --- | ---: | --- |
| Critical | 0 | None found by Phase 04 scan commands. |
| High | 0 | None found by Phase 04 scan commands. |
| Medium | 2 | P04-R3 and P04-R4 remain tracked in `phase-04-risk-register.md`. |
| Low | 0 | None recorded. |

## Limitations

The analyzer scan is the repository-available .NET analyzer gate, not a
third-party SARIF package. Phase 04 records this as a residual risk instead of
silently adding analyzer packages and suppressions outside the approved plan.
