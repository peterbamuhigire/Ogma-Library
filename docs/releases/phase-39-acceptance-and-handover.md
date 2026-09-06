# Phase 39 acceptance and handover

The final handover record is a JSON document conforming to
`packaging/release-acceptance.schema.json`. Validate it with:

```powershell
./scripts/Test-ReleaseAcceptance.ps1 -RecordPath .\acceptance-record.json -ExpectedCommitSha <40-character-release-commit-sha>
```

The command is intentionally fail-closed. A signed descriptor is necessary but
not sufficient: both platform artifacts must also be signed, installed cleanly,
tested on the named reference hardware, and tied to migration/rollback evidence.
The record must also match the executable beta-v1 schema freeze in
`phase-38-schema-freeze-v1.md`; stale or implicitly changed migration sequences
are rejected.

## Handover packet

The release owner must archive, outside source control where appropriate:

- the full-commit-SHA release record and both artifact digests;
- the verified schema-freeze version, migration count, latest migration, and
  ordered-sequence digest;
- Authenticode/MSIX certificate and verification output for Windows;
- Developer ID identity, notarization ticket, and stapling output for macOS;
- clean-install and critical-flow capture from W-REF-01 and M-REF-01;
- performance, accessibility, localisation, hostile-input, backup/restore,
  interrupted-upgrade, migration, and rollback evidence;
- support/runbook ownership, beta channel location, observation window, and
  residual-risk approval.

No library contents, annotations, provider keys, raw IP addresses, or signing
private keys belong in the handover packet.
