# Phase 35 Classroom Client Offline and Sync Evidence

Date: 2026-09-04
Reviewer: Peter Bamuhigire, Lead Consultant

## Decision

The local offline-cache and sync-integrity subgate is closed. Cache entries
are scoped to host and certificate identity, content and metadata are
tamper-evident including an exact content-length check, sync payloads are bounded against oversized/compression-bomb
inputs, and sync is single-flight. Per-profile private storage, guest no-sync,
TOFU, conflict semantics, and host-scoped eviction remain covered.

Physical credential-store/pairing, renewed-session reconnect, offline reader
UX/accessibility, cache clear/export controls, hostile two-user isolation, and
cross-machine load evidence remain open.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-build --filter "FullyQualifiedName~ClassroomClient" --verbosity minimal -m:1
```

Result: 104 passed, 0 failed, 0 skipped.
