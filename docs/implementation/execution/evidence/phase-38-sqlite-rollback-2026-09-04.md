# Phase 38 Evidence — SQLite Migration Rollback Compatibility

Date: 2026-09-04
Scope: migration rollback and remigration behavior on the supported SQLite runtime

## Implemented repair

SQLite EF Core 10.0.9 does not generate `DropColumnOperation` or foreign-key
drop operations for SQLite. The migration down paths now use explicit SQLite
column removal where safe. Phase 23 uses a row-preserving table rebuild because
`SearchChunks.ExtractionArtifactId` is part of the table foreign-key definition.
The rebuild preserves the primary key, existing rows, the page foreign key,
and the pre-Phase-23 indexes.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --filter "FullyQualifiedName~MigrationTests" --no-restore --verbosity minimal -m:1
```

Result: 9 passed, 0 failed.

The two full-suite failures previously observed in the Phase 18 downgrade and
Phase 12 legacy-history rollback tests are included in this passing class.

## Still open

This proves application-level SQLite migration rollback only. It does not
replace clean-install, upgrade interruption, backup/restore, physical rollback,
installer, signing, or reference-machine release evidence.
