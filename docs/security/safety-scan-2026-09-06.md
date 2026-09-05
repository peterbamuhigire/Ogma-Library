# Code Safety Scan Report

**Project:** Ogma Library | **Date:** 2026-09-06 | **Stack:** C#/.NET 10,
Avalonia desktop, TypeScript/Three.js, SQLite, Kestrel LAN Host

## Category A: Security Vulnerabilities

| # | Check | Severity | Findings |
|---|-------|----------|----------|
| 1 | Hardcoded API Keys | PASS | No high-confidence client-side key or token literal found. Release workflows reference GitHub Actions secrets rather than embedding them. |
| 2 | Inverted Auth Logic | PASS | LAN middleware rejects disallowed addresses, missing sessions, non-loopback administration requests, and non-admin roles; see `KestrelHostModeListener.cs`. |
| 3 | Open Admin Endpoints | PASS | `/admin` routes are behind authenticated-session, loopback, and administrator-role checks. |
| 4 | Missing Signup/Login Auth | MEDIUM | Ogma has no public password-login flow. LAN session issuance is enrollment-code/profile-token gated and rate-limited, but physical brute-force behavior remains unassessed. |
| 5 | Missing Row-Level Security | PASS | Published catalogue, asset, file, profile-sync, and managed-AI paths apply publication or authenticated-client scope checks; targeted tenancy/host isolation tests exist. |

## Category B: Server Stability (500 Error Risks)

| # | Check | Severity | Findings |
|---|-------|----------|----------|
| 6 | Unhandled Runtime Exceptions | MEDIUM | Worker and several local extraction/provider boundaries persist selected `Exception.Message` values, which can contain implementation or path details; e.g. `BookIngestionWorker.cs:183` and `MetadataExtractionService.cs:97`. This is local operator-facing diagnostic exposure, not a confirmed remote exploit. |
| 7 | Misconfigured Env Variables | PASS | Supported environment settings are parsed and validated by `OgmaRuntimeOptions`; invalid values fail closed. |
| 8 | Misconfigured File Paths | PASS | File access uses canonical/bounded path authorities, safe asset classes, and traversal tests. |
| 9 | Database Connection Problems | PASS | EF contexts are scoped through factories where production concurrency requires it; HTTP clients are managed by the host composition and responses are disposed. |
| 10 | Infinite Loops/Recursion | PASS | Worker loops, retry counts, pagination, lease windows, and bounded scans have explicit cancellation or size limits. |
| 11 | Memory Leaks | MEDIUM | Disk cache, WebView host, loopback listener, event subscriptions, streams, and cancellation resources have bounded/disposal paths; long-duration native WebView/cache/provider soak remains unassessed. |
| 12 | Concurrency Issues | PASS | Durable leases, database transactions, cache semaphore protection, session windows, and bounded worker capacity are covered by tests. |
| 13 | Data Race Conditions | PASS | Write paths use transactions, optimistic/concurrency checks, idempotency keys, lease ownership, or serialized gates as appropriate. |

## Category C: Payment Safety

| # | Check | Severity | Findings |
|---|-------|----------|----------|
| 14 | Duplicate Charge Risk | NOT APPLICABLE | No payment, checkout, or charge flow exists in the scoped application. |

## Summary

- **CRITICAL:** 0 | **HIGH:** 0 | **MEDIUM:** 3 | **LOW:** 0 | **PASS:** 10 | **NOT APPLICABLE:** 1
- **Top Priority Fixes:** complete the physical brute-force, native-host/cache soak, and cross-platform security evidence; normalize exception-to-diagnostic messages at worker and provider boundaries before any remote diagnostic or broader shared-log sink is introduced; retain detailed exceptions only in controlled local debugging channels.

## Scope and limitations

This is a static code-safety scan, not a penetration test. It covered tracked
application source, the native WebView/loopback adapter, LAN Host middleware,
release workflows, and the JavaScript shelf source. It did not establish
physical platform security, independent penetration review, macOS behavior,
credential-store security, or long-duration hostile soak evidence. Dependency
checks on 2026-09-06 found no vulnerable NuGet packages and `npm audit
--omit=dev --audit-level=high` found zero vulnerabilities for `src/shelf3d`.
