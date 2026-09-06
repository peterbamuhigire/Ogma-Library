# Phase 35 Network-Drop and Reconnect Evidence

Date: 2026-09-06

## Gate exercised

The deterministic Client connection path now covers an established classroom
session losing network access and later reconnecting to the same pinned Host.
This closes the local service-level network-drop/reconnect gate. It does not
replace a physical two-machine or cross-platform network interruption drill.

## Correctness invariant

When Host health or session renewal fails because the network is unavailable:

- connectivity becomes explicitly offline;
- the last active Host connection remains available as cache scope, so offline
  resources are not orphaned by the failure;
- no failed renewal token is persisted; and
- a later successful connection issues a new session, replaces the active
  token, persists that token for the selected non-guest profile, and restores
  online state.

Caller-requested cancellation does not mark the Host offline. A timeout not
caused by the caller and an HTTP/transport failure do.

## Executable proof

`ConnectionService_NetworkDropMarksOfflineAndReconnectRenewsSession` performs
the following sequence against the production orchestration service and real
file-backed profile repository:

1. trust and connect to a Host using session token 1;
2. inject a transport failure into the next Host-health request;
3. verify offline connectivity and retention of session token 1 as the cache
   context;
4. restore the Host and reconnect using the same profile;
5. verify session token 2 replaces the active and persisted token; and
6. verify connectivity returns online.

Verification on the current worktree:

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~OgmaLibrary.Tests.ClassroomClient" --logger "console;verbosity=minimal" -m:1
Passed: 111, Failed: 0, Skipped: 0

dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~MainShell_ClassroomOfflineChip" --logger "console;verbosity=minimal" -m:1
Passed: 2, Failed: 0, Skipped: 0
```

The UI proof verifies that Client mode displays the offline status and removes
it after connectivity is restored.

## Residual gates

Physical Windows/macOS credential-store and pairing evidence, a physical
two-machine network interruption and offline-reader walkthrough, assistive-
technology capture, hostile concurrent two-user isolation, and cross-machine
load evidence remain `NOT ASSESSED`.
