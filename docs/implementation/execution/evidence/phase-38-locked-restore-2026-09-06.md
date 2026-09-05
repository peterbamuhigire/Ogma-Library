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
- A fresh unsigned `win-x64` candidate was published from commit
  `5bef1cc209295b1da452ac342da64f92ef00b5075`.
- `Test-ReleaseCandidate.ps1` passed artifact and descriptor-integrity checks.
- Candidate SHA-256:
  `d2f11a4fb222992adc30272943c55657acaefd56a6b3ea46f57c10bb07a7ff8c`.
- The temporary candidate directory was removed after verification. No signed
  or installed release is claimed by this record.
- The same run passed `Test-RequirementAccountability.ps1`: 101 functional
  requirements, 29 non-functional requirements, and 32 controls; all 162 IDs
  are assigned in the roadmap matrix.

The signed-installer, Authenticode, clean-install, upgrade-recovery and
rollback gates remain open as specified by the Phase 38 and Phase 39 records.
