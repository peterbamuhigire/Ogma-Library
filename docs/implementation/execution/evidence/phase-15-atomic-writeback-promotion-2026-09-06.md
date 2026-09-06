# Phase 15 Atomic Writeback Promotion

Date: 2026-09-06

## Defect closed

Successful writeback and backup undo previously deleted the original path
before moving the verified temporary PDF into place. A process interruption in
that gap could leave the library book missing even though both files were on
the same volume.

Failure restoration also copied the backup directly over the original path,
allowing interruption to expose a partially copied PDF.

## Corrected invariant

- A changed PDF is written and verified at an adjacent temporary path.
- The verified file is promoted with same-directory overwrite move; there is
  no application-level delete-before-move window.
- Undo uses the same promotion primitive.
- Failure recovery verifies backup integrity, copies to a uniquely named
  adjacent recovery file, verifies that PDF, then promotes it over the source.
- A failed recovery removes its temporary file and does not copy partial bytes
  directly onto the source.

These operations rely on the filesystem's same-volume replacement semantics.
Physical process-kill and cross-platform filesystem evidence remains required.

## Executable proof

The combined writeback safety suite verifies successful write/undo,
byte-identical failure restoration, audit state, cancellation, tamper rejection,
durable plans, and absence of leftover recovery temporary files.

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Phase15WriteBackSafetyTests|FullyQualifiedName~PdfWriteBackTests" --logger "console;verbosity=minimal" -m:1
Passed: 13, Failed: 0, Skipped: 0
```

## Residual gate

A physical kill at controlled points around promotion and cross-platform
permission/filesystem drills remain `NOT ASSESSED`. This change removes the
known application-created missing-file window but does not substitute for that
acceptance evidence.
