# Phase 38 release pipeline and beta gates

Status: implementation in progress. This document records executable release
mechanics and the evidence still requiring protected platform environments.

## Artifact identity

Each candidate is identified by the source commit, version, runtime identifier,
artifact filename, SHA-256 digest, and detached descriptor signature. The zip
is built once by `scripts/New-ReleaseCandidate.ps1`; promotion must copy that
same artifact and descriptor rather than rebuild it.

The descriptor is compact, property-ordered UTF-8 JSON. Its exact bytes are the
signature payload. `ReleaseDescriptor.TryParse` enforces the schema, platform /
runtime pairing, bounded identifiers, safe artifact filename, and SHA-256
shape. `ReleaseDescriptorVerifier.TryVerify` performs structural validation and
then verifies RSA-PSS/SHA-256 using the protected public key.

## Pipeline stages

| Stage | Executable gate | Evidence / owner |
| --- | --- | --- |
| Commit | locked restore, Release build, tests, analyzers, dependency and secret scans | CI run / engineering |
| Candidate build | `New-ReleaseCandidate.ps1` for `win-x64` and `osx-arm64` | artifact digest / release engineer |
| Integrity | `Test-ReleaseCandidate.ps1` and `RsaUpdateVerifierTests` | descriptor + digest / security |
| Platform signing | Authenticode/MSIX and Developer ID/notarization in protected runners | signed artifact, certificate identity, notarization ticket / release owner |
| Acceptance | clean install, launch, migration, upgrade, rollback, and critical-flow checks on W-REF-01 and M-REF-01 | signed run record / QA |
| Promotion | immutable artifact and descriptor copied to the beta channel | release ID and digest / release owner |

The local repository can prove the first three stages. It cannot honestly prove
hardware-specific performance, clean-install behavior, Authenticode, Apple
notarization, or protected key custody without the reference machines and
secrets. Those are hard beta gates, not waived checklist items.

## Signing and key custody

No private signing key belongs in this repository. The packaging script accepts a
key only through an operator-controlled path or protected CI secret and fails
when `-RequireSignature` is used without one. The descriptor public-key ID must
match the key pinned by the updater. Signing services should emit the key ID,
certificate chain, timestamp/notarization result, and artifact digest into the
release evidence record.

## Upgrade, rollback, and migration drill

1. Install a baseline candidate against a disposable profile and record its
   release ID and schema version.
2. Apply the candidate migration, open the catalogue, reader, search, AI
   degraded mode, and LAN host journeys, and record health evidence.
3. Interrupt an upgrade before first launch; recovery must leave the baseline
   install usable and must not delete user data.
4. Apply the next candidate and verify the same artifact digest is used in every
   environment.
5. Roll back the application binary using the previous immutable artifact. If a
   migration is forward-only, use the documented compatible read path and a
   backup restore/compensating migration; never rewrite production data as an
   improvised rollback.
6. Reject a descriptor mutation, signature mutation, artifact mutation, and
   unsupported schema before download/install.

The physical drill remains open until its commands, machine IDs, timestamps,
backup hashes, and screenshots/logs are attached to the Phase 39 handover pack.

## Privacy and observability

Release IDs and local health metrics are diagnostic metadata. Crash/support
collection remains opt-in and minimised. A beta evidence record must include
release ID, commit SHA, artifact digest, migration result, verification result,
rollback decision, and observation window without including library contents,
book annotations, provider secrets, or raw network identifiers.
