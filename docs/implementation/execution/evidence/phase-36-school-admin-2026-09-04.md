# Phase 36 School Administration and Managed AI Evidence

Date: 2026-09-04
Reviewer: Peter Bamuhigire, Lead Consultant

## Decision

The local managed-AI boundary subgate is closed. Provider keys remain host-side
behind the platform credential abstraction; client-facing paths enforce active
catalogue scope, metadata-only default policy, payload preview, DPIA
minimization, per-student/class quotas, rate limits, and grounded citation
filtering. Oversized inputs fail before provider invocation or quota reserve.

Physical admin/student E2E, school backup/restore, key rotation/revocation,
retention/erasure acceptance, accessibility/localisation, provider soak, and
formal minors DPIA approval remain open. Managed AI remains metadata-only and
fail-closed by default.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-build --filter "FullyQualifiedName~SchoolAdmin" --verbosity minimal -m:1
```

Result: 46 passed, 0 failed, 0 skipped.
