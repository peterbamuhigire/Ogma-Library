# Phase 37 Progress - Security, Privacy and Data Protection Hardening

Date: 2026-09-06

## Delivered in this increment

- Completed the required 14-point code-safety and web-boundary review; results
  are recorded in `docs/security/safety-scan-2026-09-06.md`.
- Added a per-address LAN session issuance throttle with `429`/`Retry-After`
  behavior to reduce enrollment-code brute-force risk.
- Added security headers to every LAN response: HSTS, CSP, `nosniff`, frame
  denial, no-referrer, and no-store.
- Added SHA-256 integrity and 5 MiB bounds to opaque Host profile-sync blobs;
  client sync payloads already enforce bounded ciphertext/decompression.
- Minimized LAN audit identity data to a one-way remote-IP fingerprint and
  preserved secret/token redaction.
- Added tamper, oversize, and response-header regression tests.
- Recorded the current local hardening evidence in
  `evidence/phase-37-security-hardening-2026-09-04.md`.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed
  with 0 warnings and 0 errors after the final code changes.
- Security-focused slice: 34 passed.
- LAN-host slice: 61 passed, including session-throttle, response-header,
  profile-sync tamper, and oversize tests.
- `npm audit --omit=dev`: 0 vulnerabilities.
- NuGet vulnerable-package scan: no vulnerable packages reported.
- Fresh verification on 2026-09-04 again reported 0 npm audit vulnerabilities
  and no vulnerable NuGet packages across the solution.
- Current-head security/migration regression and script-parser reconciliation
  are recorded in `evidence/phase-37-local-security-reconciliation-2026-09-04.md`.
- Fresh current-tree dependency checks on 2026-09-05 reported 0 npm audit
  vulnerabilities and no vulnerable NuGet packages across the solution; all
  13 PowerShell scripts also parsed successfully.
- Current-tree dependency checks on 2026-09-06 again reported no vulnerable
  NuGet packages, and `npm audit --omit=dev --audit-level=high` reported zero
  vulnerabilities for `src/shelf3d`.
- Normalized raw exception messages at the selected ingestion, search,
  write-back, provider, embedding, FTS, and advisor-parser boundaries. The
  PDF worker now preserves typed failure categories while returning stable
  operator-safe messages across its child-process protocol. The focused
  boundary regression passed 55/55; physical/soak boundary review remains
  open.
- Added a seven-case synthetic malformed-PDF corpus at the production isolated
  worker boundary. Every case fails with a zero-page result or stable redacted
  exception, cannot create its outside-sandbox marker, and is followed by a
  successful valid-PDF render to prove worker recovery. Evidence:
  `evidence/phase-37-synthetic-hostile-pdf-corpus-2026-09-06.md`.
- The Phase 36 school-data service now creates an online SQLite backup and
  rehearses it in an isolated temporary database with integrity, schema, and
  table-count verification. This closes Phase 37's local non-destructive
  backup/restore sub-gate; see
  `evidence/phase-36-backup-restore-rehearsal-2026-09-06.md`.
- Removed persisted free-form job errors from the Library Health projection and
  CSV export. Operators receive stable failure codes, with `job_failed` for
  uncategorized legacy rows; a path/token leakage regression passed in the
  10-test Health Dashboard slice. Evidence:
  `evidence/phase-37-job-diagnostic-redaction-2026-09-06.md`.

## Remaining phase gate

Physical hostile PDF corpus, native secret-store and two-user erasure tests,
firewall/mDNS/network capture, independent penetration review, backup/restore
on protected target storage, and long-duration cross-platform soak remain
release evidence gates. The local isolated restore rehearsal is closed; it is
not evidence of physical recovery time, access control, or retention behavior.
The 2026-09-06 static safety scan retains a medium follow-up for hostile-PDF
physical/third-party-corpus evidence; the synthetic exception/recovery subgate
is now closed. Evidence:
`../../security/safety-scan-2026-09-06.md`.
