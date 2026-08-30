# Phase 38 Progress - Performance, Reliability, Packaging and Beta

Date: 2026-08-31

## Delivered in this increment

- Added the `ogma-release-v1` descriptor contract and JSON schema with bounded
  identifiers, safe artifact filenames, supported platform/runtime pairings,
  and mandatory SHA-256 / RSA-PSS-SHA256 fields.
- Added `ReleaseDescriptorVerifier`, which validates descriptor shape before
  verifying the exact signed JSON bytes with the existing protected-key
  verifier. Tampered descriptor and package tests are now covered together.
- Added reproducible Windows and macOS runtime candidate packaging through
  `scripts/New-ReleaseCandidate.ps1`, with digest evidence and a separate
  integrity gate in `scripts/Test-ReleaseCandidate.ps1`.
- Added credential-gated Authenticode verification and Developer ID / Apple
  notarization hooks. The script fails closed when platform signing is required
  but certificate, identity, tooling, or notarization profile is unavailable.
- Added runtime identifiers to the tracked lock files so both `win-x64` and
  `osx-arm64` publish restores remain locked and reproducible.
- Added a manual/tag release-candidate workflow with least-privilege read
  permissions, serialized runs, signature-required-by-default behavior, and
  commit-bound artifact names.
- Added the Phase 38 release pipeline, rollback, migration-drill, key-custody,
  and privacy/observability record.

## Verification

- Release build after descriptor changes: 0 warnings, 0 errors.
- Update trust slice: 3 passed, including valid descriptor verification,
  descriptor tamper rejection, unsafe filename rejection, and package digest
  rejection.
- Windows `win-x64` candidate was published, zipped, hashed, and integrity
  verified locally; candidate zip was 109,548,465 bytes and was generated in a
  temporary directory rather than committed.
- PowerShell packaging scripts parsed successfully.
- `actionlint` was not installed on the developer machine; workflow validation
  remains pending CI/actionlint execution.

## Remaining phase gate

This phase is not complete. Final MSIX/installer production, Authenticode and
Developer ID/notarization evidence, clean W-REF-01/M-REF-01 installation and
performance runs, interrupted-upgrade recovery, migration compatibility, and
physical rollback drills remain Phase 39 release-acceptance gates. No private
signing key is stored in the repository.
