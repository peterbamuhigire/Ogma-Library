# Phase 15 Streaming Backup Evidence - 2026-09-06

## Critical flow

| Actor | Trigger | Success path | Failure path | Recovery |
| --- | --- | --- | --- | --- |
| Desktop user | Confirms backup preparation before PDF metadata writeback | Stream hash source, stream-copy to a unique temporary file, verify copied bytes, atomically promote backup, then persist plan | Cancellation, read/write failure, or source change prevents promotion and deletes the temporary file | Original PDF remains untouched; user can retry preparation after resolving the cause |

## Change

- SHA-256 is computed from an asynchronous sequential file stream instead of a
  whole-file byte allocation.
- Backup bytes are copied asynchronously to a unique temporary path.
- The temporary copy is rehashed against the source hash before promotion.
- Verified bytes are flushed through the file handle before promotion.
- Only a verified copy is atomically moved to the final backup path.
- Temporary bytes are removed in `finally` on cancellation or failure.
- The durable writeback plan and audit event are written only after successful
  backup promotion.

## Verification

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PdfWriteBackTests|FullyQualifiedName~Phase15WriteBackSafetyTests" --logger "console;verbosity=minimal" -m:1
```

Result: **PASS** - 13 passed, 0 failed, 0 skipped.

The new regression supplies an already-cancelled token and verifies that
preparation throws cancellation, creates no backup directory, and persists no
writeback plan. Existing tests continue to prove exact backup/restore behavior,
external-change rejection, ACL-denial handling, and tampered-backup rejection.

## Gate interpretation

The repository-verifiable streaming backup and partial-file cleanup subgate is
closed. Actual mid-copy process termination, installed desktop recovery,
macOS permission behavior, and independent security approval remain
**NOT ASSESSED**.
