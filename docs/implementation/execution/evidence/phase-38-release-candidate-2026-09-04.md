# Phase 38 Release Candidate and Migration Evidence

Date: 2026-09-04
Reviewer: Peter Bamuhigire, Lead Consultant

## Decision

The local release-descriptor, artifact-integrity, detached-signature, and
migration-compatibility subgates are closed. The repository validates bounded
release descriptors, exact artifact digests, and safe platform/runtime pairings;
the candidate script cryptographically verifies RSA-PSS/SHA-256 signatures over
the exact descriptor bytes; and the SQLite migration class proves forward,
downgrade, remigration, and legacy backfill behavior. Release packaging remains
fail-closed when signing, public-key, or platform tooling is absent.

Final MSIX/installer production, Authenticode/Developer ID/notarization, clean
reference-machine installation and performance runs, interrupted-upgrade
recovery, and physical rollback remain Phase 39 gates. `actionlint` was not
available on this machine and CI validation remains open.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-build --filter "FullyQualifiedName~MigrationTests|FullyQualifiedName~RsaUpdateVerifierTests" --verbosity minimal -m:1
```

Result: 12 passed, 0 failed, 0 skipped.

PowerShell syntax parsing passed for `New-ReleaseCandidate.ps1`,
`Test-ReleaseCandidate.ps1`, and `Test-ReleaseAcceptance.ps1`.

An ephemeral end-to-end script check passed a valid RSA-PSS descriptor signature
and rejected a tampered signature. The test key and temporary artifacts were
deleted after verification.
