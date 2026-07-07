# Phase 04 Security Threat Model

Date: 2026-07-07
Scope: LAN host, AI provider gateway, PDF worker boundary, classroom flows
Findings: F-SEC-001, F-SEC-004, F-SEC-005, F-ARCH-001

## System Overview

Ogma Library is a local-first desktop application with optional LAN Host mode
for classroom sharing and optional AI provider egress. Phase 04 makes the
security gate executable by recording the trust boundaries, STRIDE threats,
controls, tests, residual risks, and CI scans required before later hardening
phases change code in higher-risk subsystems.

## Data Flow

```text
Local user -> Avalonia app -> Catalogue DB / sidecar assets / PDF renderer
Classroom client -> HTTPS LAN Host -> Catalogue read model / page renderer / audit log
Classroom client -> HTTPS LAN Host -> School AI proxy -> IAiProvider -> provider endpoint
PDF file -> PDF adapter / worker boundary -> rendered pages / extracted text
```

## Trust Boundaries

| Boundary | Source | Target | Principal risk |
| --- | --- | --- | --- |
| TB-01 | Classroom client | HTTPS LAN Host | Spoofing, token replay, unauthorized file or admin access |
| TB-02 | LAN Host | Catalogue DB and sidecar storage | Path traversal, local path disclosure, audit gaps |
| TB-03 | LAN Host | School AI proxy and provider gateway | Secret exposure, payload over-sharing, quota bypass |
| TB-04 | PDF file input | PDF renderer / worker boundary | Malformed file escape, network/process/file-system abuse |
| TB-05 | CI runner | Dependency and analyzer toolchain | Supply-chain drift, missing scan evidence |

## Assets

| Asset | Sensitivity | Controls |
| --- | --- | --- |
| LAN bearer session tokens | High | Hash-at-rest, fingerprint-only audit, short lifetime, auth-required endpoints |
| School AI provider keys | Critical | Credential store abstraction, no HTTP/log echo, status-only admin response |
| PDF files and rendered pages | High | Content-mode gate, catalogue-backed resolver, traversal rejection, audit rows |
| Student profile sync blobs | High | Authenticated upload/download, size cap, per-profile identity |
| Audit events | Medium | One row per LAN request with action, status, actor, resource, token fingerprint |

## STRIDE Analysis

| ID | Category | Threat | Existing mitigation | Verification |
| --- | --- | --- | --- | --- |
| S-01 | Spoofing | Client impersonates a LAN user with a missing or replayed token. | Non-public endpoints require active bearer session; managed enrollment token replay is rejected. | `LanHostEndpointTests`; `ClientSessionService` tests through LAN suite. |
| S-02 | Spoofing | Student requests an administrator session through enrollment. | Enrollment rejects admin roles; admin routes require host-local admin sessions. | `LanHostEndpointTests`. |
| T-01 | Tampering | Client manipulates asset/book identifiers to escape the library root. | Catalogue-backed resolver and sidecar hash/variant validation reject malformed identifiers. | `LanBookFileResolverTests`; `LanHostEndpointTests`; Phase 05 expands fuzzing. |
| T-02 | Tampering | AI request changes `profileId` to another student. | LAN Host compares request profile to authenticated session client id. | `LanHostEndpointTests`. |
| R-01 | Repudiation | Client denies failed or successful LAN requests. | LAN Host writes audit rows for health, session, unauthorized, admin, catalogue, AI, asset, sync, page, and file routes. | `LanHostEndpointTests`. |
| I-01 | Information Disclosure | Raw bearer tokens or provider keys appear in logs or HTTP responses. | Audit stores token fingerprint only; admin AI key endpoint returns status only. | `LanHostEndpointTests`; `SecurityBaselineTests`; CI secret scan. |
| I-02 | Information Disclosure | Catalogue DTOs expose local paths or sidecar paths. | LAN projection removes path metadata and emits controlled asset links. | `LanHostEndpointTests`. |
| D-01 | Denial of Service | Page-render requests exhaust host resources. | `ILanPageRenderLimiter` caps concurrent render leases and returns 429 when saturated. | `LanPageRenderLimiterTests`; LAN load smoke tests. |
| D-02 | Denial of Service | Dependency or analyzer drift hides new vulnerabilities. | CI runs locked restore, vulnerability scan, analyzer scan, and tests on Windows/macOS. | `.github/workflows/ci.yml`; `SecurityBaselineTests`. |
| E-01 | Elevation of Privilege | Student reaches `/admin/*` routes. | Admin routes require loopback and administrator role. | `LanHostEndpointTests`. |
| E-02 | Elevation of Privilege | AI request bypasses school privacy and quota policy. | School AI proxy enforces preview/confirmation, profile binding, quota, and provider availability. | `LanHostEndpointTests`; Phase 10 expands AI answer-mode controls. |

## Gate Status

| Gate | Status | Evidence |
| --- | --- | --- |
| Threat model exists | Pass | This document covers LAN Host, AI provider, PDF worker, classroom flows, assets, STRIDE, and residual risks. |
| Dependency scan | Pass | `dotnet list OgmaLibrary.sln package --vulnerable --include-transitive` passes locally and is wired in CI. |
| SAST analyzer scan | Pass | `dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn --verbosity minimal` passes locally and is wired in CI. |
| Secret pattern scan | Pass | High-confidence source/workflow scan passes locally and is wired in CI. |
| Abuse-case tests | Pass | LAN Host and Security tests cover unauthorized access, token redaction, admin denial, path redaction, profile mismatch, render limiting, and credential-store behavior. |

## Residual Risks

Residual risks are tracked in `docs/security/phase-04-risk-register.md`. Phase
04 does not implement untrusted-PDF sandboxing, at-rest encryption, signing,
or final DPIA sign-off; those remain assigned to later phases in the master plan.
