# Phase 39 Acceptance Schema-Freeze Binding

Date: 2026-09-06

## Contract defect repaired

The PowerShell acceptance validator required `artifactName`, while the JSON
schema omitted that property and set `additionalProperties: false`. A record
that satisfied the script could therefore fail schema validation. The schema
now requires and defines the same bounded, path-safe artifact name enforced by
the validator.

## Schema-freeze binding

Release acceptance now requires an exact `schemaFreeze` object containing:

```text
version: beta-schema-v1
migrationCount: 41
latestMigration: 20260906060000_Phase17PausedJobStatus
sequenceSha256: 8135fad43778f705b48c9d667d8e56d36b8d4445b8be3a5d2b985b1e42637dd5
verified: true
```

Both the JSON schema and executable validator enforce these values. Phase 38's
compiled migration-sequence test independently derives the same baseline.

## Synthetic contract verification

The fixtures under `tests/fixtures/` are explicitly labelled
`contract-fixture-do-not-release`, use zero digests/commit SHA, and name the
owner `TEST FIXTURE - NOT AN APPROVAL`. They are contract tests, not release
evidence.

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Test-ReleaseAcceptance.ps1 -RecordPath tests\fixtures\release-acceptance-contract-valid.json
exit 0: Release acceptance passed for contract-fixture-do-not-release ...

powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Test-ReleaseAcceptance.ps1 -RecordPath tests\fixtures\release-acceptance-contract-invalid-schema-freeze.json
exit 1: Acceptance migration count does not match the frozen baseline.
```

All three JSON documents parsed successfully and the PowerShell parser reported
no syntax errors.

## Residual gates

This closes contract/schema consistency and schema-freeze binding only. No
signed artifact, installation, reference-machine run, accessibility/performance
result, migration drill, backup/restore, rollback, or owner acceptance is
created by these synthetic fixtures.
