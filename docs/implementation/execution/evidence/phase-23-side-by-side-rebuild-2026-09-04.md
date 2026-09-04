# Phase 23 Side-by-Side Rebuild Evidence

Date: 2026-09-04

## Scope

Production rebuilds use the staged extraction capability to write chunks under
`fts5-rebuild-{rebuildId}` while the active `fts5-v1` generation remains in the
search path. A healthy rebuild with no failed books promotes staged rows in one
transaction. Failed staged runs do not delete the active generation.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase23SideBySideRebuildTests|FullyQualifiedName~IndexManagerServiceTests" --verbosity minimal -m:1
```

Result: 7 passed, 0 failed, 0 skipped.

The dedicated regression kept the old active FTS result readable while a new
generation was staged, then verified the new result after promotion and that
all persisted rows use the active version. Existing checkpoint/resume,
integrity, lifecycle-event, cancellation, and duplicate-protection tests also
passed.

## Still open

Reference-hardware latency confirmation and physical assistive-technology
walkthroughs remain release evidence gates.
