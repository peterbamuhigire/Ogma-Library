# Phase 34 Progress - Classroom Host Security and Published Read Model

Date: 2026-09-04

## Delivered in this increment

- Constrained LAN catalogue paging to active (`Status = 0`) records regardless
  of caller-supplied lifecycle filters.
- Constrained metadata search and fuzzy fallback to active catalogue records.
- Added published-scope checks before serving catalogue details, page renders,
  PDF files, or sidecar assets; an existing private sidecar hash is not enough
  to make an asset reachable.
- Redacted private reading progress, annotations, reading memory, metadata
  fields, file size, OCR state, and password state from the classroom host
  projection while preserving the stable published bibliographic contract.
- Preserved TLS, enrollment/session validation, role checks, local-only admin
  routes, path traversal prevention, range support, render concurrency limits,
  and redacted request audit behavior already present in the host boundary.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed
  with 0 warnings and 0 errors after the serial verification run.
- Full LAN Host slice: 61 passed.
- Architecture suite: 41 passed.
- Endpoint integration proof covers HTTPS, authentication, RBAC, session
  replay, pagination, search, TLS-backed page rendering, range/file policy,
  profile sync, sidecar delivery, and unpublished/private-scope rejection.
- Local authenticated load-smoke coverage passed for 20 concurrent catalogue
  clients and 10 concurrent page-render clients, each under the encoded
  p95 <2-second assertion.
- The catalogue concurrency smoke was rerun three consecutive times in
  isolation and passed each time, followed by a clean 61-test LAN slice.
- After a Windows full-suite contention failure in CI run 34022144296, the live
  HTTPS performance collection was isolated from unrelated test collections
  and given an explicit warm-up request. The page-render ceiling remains
  `< 2,000 ms`; the catalogue gate now enforces the original `<= 800 ms` P95
  requirement. Three consecutive catalogue runs measured 149 ms, 146 ms, and
  149 ms. Evidence:
  `evidence/phase-34-ci-load-isolation-2026-09-06.md`.
- Current-head local gate reconciliation is recorded in
  `evidence/phase-34-local-gate-reconciliation-2026-09-04.md`.

## Remaining phase gate

Physical two-machine Windows/macOS acceptance, firewall behavior, mDNS failure
and manual fallback, certificate TOFU UX, sustained hostile/load/soak evidence,
and privacy-capture review remain release gates. Standalone mode remains
listener-free by default and classroom enablement remains opt-in. The local
concurrency smoke sub-gate is closed; it is not a substitute for the physical
two-machine or sustained soak gates.
