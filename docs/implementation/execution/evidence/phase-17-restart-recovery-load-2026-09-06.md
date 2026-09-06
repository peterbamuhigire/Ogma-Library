# Phase 17 Restart Recovery Load Follow-up - 2026-09-06

## Scope

This follow-up validates the restart-style durable queue recovery benchmark
after removing per-job SQLite context startup from the timed drain.

The test still disposes the pre-restart context, recovers an orphaned lease
through a newly created context, and drains 64 queued metadata jobs through a
post-restart worker context. It does not claim OS process-kill, cross-machine,
or long-duration soak evidence.

## Change

`Phase17RuntimeRecoveryLoadTests` now uses one recreated worker context for the
timed queue drain. This measures the queue/runtime path rather than repeatedly
opening and closing a SQLite database for every job, which caused a false
Windows performance failure at 37.4 seconds against the old 10-second gate.

## Verification

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Phase17RuntimeRecoveryLoadTests" --logger "console;verbosity=minimal" -m:1
```

Result: **PASS** - 1 passed, 0 failed, 0 skipped; test duration 3 seconds.

## Gate interpretation

The local restart-style load benchmark is green and its timing is now scoped
to the intended worker drain. Phase 17 remains open for full-application crash
recovery, cross-platform process behavior, and long-duration soak evidence.

