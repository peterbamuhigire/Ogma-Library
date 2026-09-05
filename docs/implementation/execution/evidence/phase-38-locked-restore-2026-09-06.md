# Phase 38 locked-restore evidence — 2026-09-06

## Finding and correction

Adding the native WebView dependency to the application made the three
project-reference test lock files stale. A locked release restore correctly
failed with `NU1004`; `dotnet restore OgmaLibrary.sln --force-evaluate` refreshed
the test lock metadata without changing dependency versions outside the
application's selected MIT WebView package.

## Verification

- Locked restore completed successfully for all solution projects during the
  release-candidate run.
- A fresh unsigned `win-x64` candidate was published from the current commit.
- `Test-ReleaseCandidate.ps1` passed artifact and descriptor-integrity checks.
- Candidate SHA-256:
  `4e9f7201499f38abcc8c9577fb56409c57783fe51afdeda683ca15d28e13a807`.
- The temporary candidate directory was removed after verification. No signed
  or installed release is claimed by this record.

The signed-installer, Authenticode, clean-install, upgrade-recovery and
rollback gates remain open as specified by the Phase 38 and Phase 39 records.
