# Phase 36 Progress - School Administration and Managed AI

Date: 2026-09-04

## Delivered in this increment

- Kept school provider keys host-side through the platform credential-store
  boundary; the classroom proxy receives a configured provider abstraction and
  never exposes the key to clients.
- Key replacement is an overwrite-safe rotation operation at the credential
  boundary, and `DeleteKeyAsync` revokes the configured provider key. Local
  tests verify replacement status, revocation status, and clearing of mutable
  key buffers; platform-specific lifecycle evidence remains separate.
- Enforced active-catalogue scope for managed-AI candidates and bounded query,
  library-id, candidate-field, and payload sizes before provider egress.
- Preserved metadata-only default policy, payload preview/confirmation, DPIA
  screening, per-student/class quota reservation, rate limiting, and grounded
  citation filtering.
- Minimized DPIA audit data by recording birth-year presence rather than the
  exact birth year in the audit payload.
- Added regression proof that overlong requests stop before provider invocation
  or quota reservation.
- Made institution-wide AI-history erasure append a payload-minimized audit in
  the same transaction as query-history and usage-ledger deletion. The event
  records only deletion counts and UTC time; it excludes profile/query/response
  data. Evidence:
  `evidence/phase-36-ai-history-erasure-audit-2026-09-06.md`.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed
  with 0 warnings and 0 errors.
- School Administration/managed-AI slice: 47 passed.
- Dedicated school-admin scaffold suite: 12 passed, including replacement
  overwrite proof in one normalized credential scope.
- Provider failure, payload-preview, quota, rate-limit, DPIA, grounded citation,
  provider-key custody, and overlong-request paths are covered by focused tests.
- The Host AI proxy integration slice now passes 6/6, including direct proof
  that an exhausted student token budget returns `school_ai_quota_exhausted`
  before the configured provider is invoked.
- Managed profile revocation now revokes every outstanding Host session for the
  same profile in the same database save. The real HTTPS Host endpoint flow
  proves the already-issued bearer token receives 401 on its next request.
- Usage-dashboard integration evidence now aggregates ten student queries over
  two daily ledger rows and verifies the combined token, cost, and quota values.
- School AI history/scaffold controls passed 14/14; the current SchoolAdmin
  namespace slice passed 44/44 after erasure auditing was added.
- Added online SQLite school backup and non-destructive restore rehearsal with
  integrity, schema, and per-table row-count comparison. The focused slice
  passed 2/2. Evidence:
  `evidence/phase-36-backup-restore-rehearsal-2026-09-06.md`.
- Current-head local gate reconciliation is recorded in
  `evidence/phase-36-local-gate-reconciliation-2026-09-04.md`.
- Current focused school-administration/managed-AI/profile verification:
  **51 passed, 0 failed, 0 skipped** on 2026-09-06.

## Remaining phase gate

Physical admin/student E2E, administrator-run backup/restore and protected
storage evidence, platform key rotation/revocation evidence, physical retention/erasure acceptance,
accessibility/localisation capture, provider load/soak, and formal minors DPIA
approval remain release gates. The local transactional erasure-audit subgate is
closed, as is the non-destructive local restore-rehearsal subgate. Managed AI
remains metadata-only and fail-closed by default.

The Aug-39 Definition of Done now records server-side administration RBAC,
host-only rotatable key custody, and grounded published-scope managed AI as
closed by local executable evidence. The combined retention/erasure gate and
formal DPIA/backup approval gate remain unchecked because their physical and
accountable-owner acceptance is still outstanding.
