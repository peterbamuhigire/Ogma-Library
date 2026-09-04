# Phase 36 Progress - School Administration and Managed AI

Date: 2026-09-04

## Delivered in this increment

- Kept school provider keys host-side through the platform credential-store
  boundary; the classroom proxy receives a configured provider abstraction and
  never exposes the key to clients.
- Enforced active-catalogue scope for managed-AI candidates and bounded query,
  library-id, candidate-field, and payload sizes before provider egress.
- Preserved metadata-only default policy, payload preview/confirmation, DPIA
  screening, per-student/class quota reservation, rate limiting, and grounded
  citation filtering.
- Minimized DPIA audit data by recording birth-year presence rather than the
  exact birth year in the audit payload.
- Added regression proof that overlong requests stop before provider invocation
  or quota reservation.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed
  with 0 warnings and 0 errors.
- School Administration/managed-AI slice: 46 passed.
- Provider failure, payload-preview, quota, rate-limit, DPIA, grounded citation,
  provider-key custody, and overlong-request paths are covered by focused tests.
- Current-head local gate reconciliation is recorded in
  `evidence/phase-36-local-gate-reconciliation-2026-09-04.md`.

## Remaining phase gate

Physical admin/student E2E, school backup/restore rehearsal, key rotation and
revocation evidence, retention/erasure acceptance, accessibility/localisation
capture, provider load/soak, and formal minors DPIA approval remain release
gates. Managed AI remains metadata-only and fail-closed by default.
