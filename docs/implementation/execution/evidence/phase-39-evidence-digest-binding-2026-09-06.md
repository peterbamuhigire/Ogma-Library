# Phase 39 Evidence Digest Binding

Date: 2026-09-06
Reviewer: Peter Bamuhigire, Lead Consultant

## Decision

The release-acceptance record no longer treats boolean assertions as sufficient
proof. Every artifact, reference-machine result, schema-freeze result,
migration rehearsal, and approval must cite at least one evidence ID. Each ID
resolves to a relative file beside the acceptance record and is verified by
SHA-256 before any acceptance assertion can pass.

This closes the repository contract-integrity sub-gate. It does not close any
physical release gate: the checked-in evidence file is explicitly test-only,
and no production acceptance record exists.

## Controls

- Evidence IDs are bounded, safe identifiers and must be unique.
- Evidence paths must be relative and remain inside the acceptance-record
  directory after canonicalization.
- Evidence files must exist and match their declared SHA-256 digest.
- Every platform, hardware, schema-freeze, migration, and approval object must
  reference verified evidence.
- The JSON schema and executable PowerShell validator reject undeclared fields.
- The deterministic text fixture is marked `-text` in `.gitattributes`, so Git
  cannot rewrite its bytes per platform and invalidate the declared digest.

## Verification

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ReleaseAcceptance.ps1 -RecordPath tests/fixtures/release-acceptance-contract-valid.json -ExpectedCommitSha 0000000000000000000000000000000000000000
Exit: 0

powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ReleaseAcceptance.ps1 -RecordPath tests/fixtures/release-acceptance-contract-invalid-schema-freeze.json -ExpectedCommitSha 0000000000000000000000000000000000000000
Exit: 1 (expected stale migration-count rejection)

dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ReleaseAcceptanceContractTests"
Passed: 1, Failed: 0, Skipped: 0
```

The automated test also changes the declared evidence digest and proves that
the validator rejects the record before acceptance.

## Remaining gates

Signed/notarized production artifacts, W-REF-01 and M-REF-01 installed-build
evidence, physical critical journeys, performance/accessibility results,
upgrade interruption, backup/restore, rollback, and genuine owner approval
remain `NOT ASSESSED`.
