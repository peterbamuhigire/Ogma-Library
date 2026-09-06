# Phase 38 Beta Schema Freeze v1

Date: 2026-09-06

## Frozen baseline

The beta-v1 catalogue schema is defined by the ordered EF Core migration
sequence compiled into `OgmaLibrary.Infrastructure`:

```text
migration count: 41
latest migration: 20260906060000_Phase17PausedJobStatus
ordered sequence SHA-256: 8135fad43778f705b48c9d667d8e56d36b8d4445b8be3a5d2b985b1e42637dd5
```

`Phase38ReleaseSchemaFreezeTests` calculates the same normalized sequence from
EF Core metadata. CI fails if a migration is added, removed, renamed, or
reordered without an explicit baseline update.

## Change policy after freeze

A schema change after this freeze requires all of the following in one reviewed
increment:

1. a new additive or safely staged migration;
2. forward, downgrade where supported, and remigration tests;
3. legacy-data preservation/backfill evidence;
4. backup/restore and release compatibility impact review;
5. an intentional update to count, latest ID, and sequence digest in the freeze
   test and this record; and
6. a release note identifying the compatibility boundary.

Editing the baseline only to make CI green is not acceptance evidence.

## Verification

The gate is part of the core test assembly and therefore runs on both protected
CI platforms. Its focused command is:

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Phase38ReleaseSchemaFreezeTests|FullyQualifiedName~MigrationTests" --logger "console;verbosity=minimal" -m:1
Passed: 10, Failed: 0, Skipped: 0
```

Physical install/upgrade/rollback, signed artifacts, reference-machine
performance, and owner approval remain separate release gates.
