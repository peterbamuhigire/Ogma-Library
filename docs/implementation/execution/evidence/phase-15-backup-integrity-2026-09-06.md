# Phase 15 Backup Integrity Evidence - 2026-09-06

## Scope

This increment covers integrity protection for the durable backup used by PDF
metadata writeback and recovery.

## Finding

The prepared token recorded the original PDF SHA-256, but the write and restore
boundaries previously checked only that the backup existed and was within the
trusted backup root. A backup could therefore be replaced after preparation
without being detected by those boundaries.

## Change

`PdfWriteBackService` now recomputes the backup SHA-256 and compares it with the
prepared token before:

- loading a persisted writeback plan;
- writing metadata to the original PDF;
- restoring the original PDF.

Missing or mismatched backup bytes fail closed. The original PDF is not mutated
when the check fails.

## Verification

Command:

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Phase15WriteBackSafetyTests" --logger "console;verbosity=minimal" -m:1
```

Result: **PASS** - 4 passed, 0 failed, 0 skipped.

The new regression replaces the prepared backup with tampered bytes, verifies
that `WriteAsync` rejects the operation, verifies that `RestoreBackupAsync`
returns failure, and verifies that the original PDF remains byte-identical in
both cases.

`git diff --check` passed for the source and test changes.

## Gate interpretation

This closes the backup-byte-integrity subgate for the tested Windows/.NET
implementation. It does not close Phase 15. The following remain
**NOT ASSESSED** or open:

- recovery after an actual process kill at each writeback interruption point;
- cross-platform permission and replacement behaviour;
- installed desktop end-to-end evidence for the complete recovery journey;
- independent security approval for the writeback recovery boundary.

## CI follow-up

The first cross-platform run for commit `85b72ca` passed on macOS but exposed
that the existing Windows ACL-denial test could surface an unauthorized backup
read before the established writeback failure boundary. The implementation now
handles that permission failure through the existing cleanup/audit path while
retaining explicit integrity rejection for mismatched backup bytes.

Follow-up local verification:

```text
PdfWriteBackTests + Phase15WriteBackSafetyTests: 12 passed, 0 failed, 0 skipped
```

The repaired cross-platform CI result is pending.
