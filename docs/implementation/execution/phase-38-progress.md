# Phase 38 Progress - Performance, Reliability, Packaging and Beta

Date: 2026-09-06

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
- Repaired SQLite rollback compatibility for additive-column down paths and
  replaced the Phase 23 foreign-key column removal with a row-preserving table
  rebuild. The migration test class now passes forward, downgrade, remigration,
  and legacy backfill scenarios.
- Closed the locally verifiable migration-compatibility subgate with the
  migration test class passing 9/9. Physical upgrade/rollback drills remain
  release-acceptance work.
- Froze the beta-v1 ordered EF Core migration sequence with an executable
  count/latest-ID/SHA-256 gate. Any migration change now requires an intentional
  baseline update plus compatibility, backfill, backup, and release evidence.
  See `../../releases/phase-38-schema-freeze-v1.md`.
- Recorded the local descriptor, packaging-script, and migration evidence in
  `evidence/phase-38-release-candidate-2026-09-04.md`.

## Verification

- Release build after descriptor changes: 0 warnings, 0 errors.
- Update trust slice: 3 passed, including valid descriptor verification,
  descriptor tamper rejection, unsafe filename rejection, and package digest
  rejection.
- Windows `win-x64` candidate was published, zipped, hashed, and integrity
  verified locally; candidate zip was 109,548,465 bytes and was generated in a
  temporary directory rather than committed.
- Fresh unsigned `win-x64` candidate generation and integrity verification
  passed after the packaging script was corrected to omit empty signature-only
  parameters; the temporary candidate was removed after verification.
- Current-head rerun on 2026-09-05 produced a fresh unsigned `win-x64`
  candidate and passed `Test-ReleaseCandidate.ps1`; artifact SHA-256 was
  `42bfc492967bc014d7d371525f73ae27f941bc884fd8bf3ec3af55353af6c8e1`.
  The candidate was written outside the repository under the task temporary
  directory and is not a signed or installed release.
- Current-head rerun after the Phase 35–37 batch produced and verified another
  unsigned `win-x64` candidate; artifact SHA-256 was
  `2a2a70b5548def4f27dc2518575771eb897c3417c875845bf26396293e2766ca`.
  The temporary candidate directory was removed after verification.
- PowerShell packaging scripts parsed successfully.
- `actionlint` 1.7.12 passed both tracked workflows (`ci.yml` and
  `release-candidate.yml`) with exit code 0; see
  `evidence/phase-38-workflow-lint-2026-09-04.md`.
- SQLite migration test class: 9 passed, including the previously failing
  Phase 18 downgrade and Phase 12 legacy-history rollback scenarios.
- The beta-v1 schema-freeze test is included in protected CI; focused
  migration/freeze verification passed 10/10 and remains required after every
  schema change.
- Current-head release-gate reconciliation is recorded in
  `evidence/phase-38-local-release-reconciliation-2026-09-04.md`.
- On 2026-09-06, locked restore initially caught stale project-reference lock
  metadata after the native WebView dependency was added. All three test lock
  files were refreshed, and a fresh unsigned `win-x64` candidate passed locked
  restore, publish, digest creation and `Test-ReleaseCandidate.ps1` integrity
  verification. Evidence: `evidence/phase-38-locked-restore-2026-09-06.md`.
- The clean-source CI format check was rerun and failed with baseline
  CRLF/charset/import/whitespace diagnostics; the result and unsafe formatter
  fix-all observation are recorded in
  `evidence/phase-38-format-gate-2026-09-05.md`.
- A safe formatting subset was committed as `e0eaea9` and the CRLF checkout
  policy as `17871b9`; the full Release suite remained green at 1,104 tests at
  that commit. The current head later passed 1,110 tests after the classroom
  cache export, erasure, and adapter-parity coverage was added.
  The residual import-order diagnostic in `ReaderModule.cs` was mechanically
  corrected after confirming its content hash matched `HEAD`; a fresh-checkout
  verifier run is recorded in
  `evidence/phase-38-format-remediation-2026-09-05.md`.
- Current-head locked Windows candidate rerun on 2026-09-06 passed restore,
  publish, ZIP digest creation, and `Test-ReleaseCandidate.ps1` integrity
  verification. Candidate SHA-256 was
  `d2f11a4fb222992adc30272943c55657acaefd56a6b3ea46f57c10bb07a7ff8c`;
  the temporary candidate was removed after verification.
- The same current-head run passed requirement accountability for all 162
  requirement/control IDs.
- Current-head unsigned `osx-arm64` cross-target packaging also passed locked
  restore, publish, descriptor generation, and `Test-ReleaseCandidate.ps1`
  integrity verification. Candidate SHA-256 was
  `6f7576cf6232207bcf7b7cc104d8e8404844d95e9a9fb42637e5dce92bc143c9`.
  Evidence: `evidence/phase-38-cross-target-packaging-2026-09-06.md`.
- Protected-main cross-platform CI completed successfully for commit
  `75effc78c44350de79e107ce53f2da9955dc6fcf`, including 41 architecture,
  938 core, and 159 UI tests on each platform. Evidence:
  `evidence/ci-cross-platform-regression-2026-09-06.md`.

## Remaining phase gate

This phase is not complete. Final MSIX/installer production, Authenticode and
Developer ID/notarization evidence, clean W-REF-01/M-REF-01 installation and
performance runs, interrupted-upgrade recovery, and physical rollback drills
remain Phase 39 release-acceptance gates. No private signing key is stored in
the repository. The local release-schema freeze subgate is closed; beta owner
approval remains open.
