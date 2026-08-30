# Code Safety Scan Report

**Project:** Ogma-Library | **Date:** 2026-08-30 | **Stack:** .NET 10,
ASP.NET Core/Kestrel, Avalonia, TypeScript/Three.js, SQLite

**Mode:** Automated static/checklist scan with targeted executable tests.
The web-audit PHP-only checks are marked N/A where this repository has no PHP
entry point. This is not an independent penetration test.

## Category A: Security Vulnerabilities

| # | Check | Severity | Findings |
|---|---|---|---|
| 1 | Hardcoded API Keys | PASS | High-confidence secret-pattern test passes; provider keys are supplied through host credential-store abstractions. |
| 2 | Inverted Auth Logic | PASS | `KestrelHostModeListener` rejects disallowed addresses, missing sessions, non-loopback admin access, and non-admin roles before protected handlers. |
| 3 | Open Admin Endpoints | PASS | `/admin/*` is protected by authenticated admin-role and loopback checks. |
| 4 | Missing Signup/Login Auth | MEDIUM | No password-login surface exists; LAN session issuance uses enrollment/managed tokens and now has a 10-attempt-per-minute address throttle. Physical brute-force testing remains unverified. |
| 5 | Missing Row-Level Security | PASS | Host catalogue is active/published scoped; private classroom data is keyed by profile and Host; asset/file/page routes enforce published scope. |

## Category B: Server Stability (500 Error Risks)

| # | Check | Severity | Findings |
|---|---|---|---|
| 6 | Unhandled Runtime Exceptions | MEDIUM | External/provider and native-platform failures have fail-closed paths, but full hostile exception fuzzing is not executed in this environment. |
| 7 | Misconfigured Env Variables | PASS | No required environment-variable deployment path was found; secrets/config are excluded by `.gitignore`. |
| 8 | Misconfigured File Paths | PASS | Library, PDF, sidecar, cache, and host file paths use canonical roots or hashed filenames with traversal tests. |
| 9 | Database Connection Problems | PASS | EF contexts are factory-scoped/disposed; migrations, retry/repair paths, and locked-mode CI checks are present. |
| 10 | Infinite Loops/Recursion | PASS | Retry paths have explicit caps; sync compression, page rendering, and request bounds are finite. |
| 11 | Memory Leaks | MEDIUM | Disk caches and renderer residency are bounded; physical long-run WebView/native-host soak is unverified. |
| 12 | Concurrency Issues | PASS | Quota reservations use serialized transactions/gates; page rendering, cache writes, and classroom sync are bounded/single-flight. |
| 13 | Data Race Conditions | PASS | Targeted concurrency tests pass for quota/cache/sync boundaries; physical multi-process testing remains unverified. |

## Category C: Payment Safety

| # | Check | Severity | Findings |
|---|---|---|---|
| 14 | Duplicate Charge Risk | N/A | No payment, billing, subscription, Stripe, or charge flow exists. AI cost values are estimates only. |

## Additional controls verified

- LAN responses set HSTS, CSP, `nosniff`, frame denial, no-referrer, and
  no-store headers.
- Host profile-sync blobs enforce a 5 MiB size limit and SHA-256 integrity.
- Client sync blobs enforce bounded ciphertext and decompressed plaintext sizes.
- WebView assets use local CSP and `ogma://` scheme handling; the 3D bridge
  rejects unsupported protocol versions and validates inbound bounds.
- `npm audit --omit=dev`: 0 vulnerabilities.
- `dotnet list OgmaLibrary.sln package --vulnerable --include-transitive`: no
  vulnerable packages reported.

## Summary

- **CRITICAL:** 0 | **HIGH:** 0 | **MEDIUM:** 3 | **LOW:** 0 | **PASS:** 10 | **N/A:** 1
- **Top priority follow-ups:**
  1. Execute physical two-machine/native WebView, secret-store, firewall,
     mDNS, and hostile-network tests.
  2. Decide and document the unsupported-platform fallback secret-store risk;
     retain native Windows Credential Manager/macOS Keychain/Linux Secret
     Service as the supported path.
  3. Run long-duration WebView, sync, cache, and provider failure soak tests.

## Exclusions

No production credentials, external provider calls, untrusted PDF corpus,
physical Windows/macOS native host, or independent penetration test was run.
