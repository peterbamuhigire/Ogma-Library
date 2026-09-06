# Phase 36 AI-History Erasure Audit

Date: 2026-09-06

## Defect closed

Institution-wide managed-AI erasure deleted query history and usage-ledger rows
transactionally, but did not create an audit record for the destructive admin
action itself. Existing search audit rows survived, yet operators could not
prove when a purge occurred or how many erasable rows it removed.

## Corrected invariant

The purge now appends `SchoolAiHistoryPurged` in the same SQLite transaction as
the two deletions. The event is local-only and contains only:

- query-history row count;
- usage-ledger row count; and
- UTC purge timestamp.

It excludes profile identifiers, query text, response text, provider payloads,
and credentials. Transaction rollback therefore preserves the pre-purge state
if the audit write cannot be committed.

## Executable proof

The focused test seeds one query-history row, one usage row, and one prior
search audit event. It verifies both erasable tables are empty, the prior audit
is retained, exactly one purge audit is appended with the correct counts, and
the purge payload contains neither the student question nor profile identity.

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SchoolAiHistoryManagementServiceTests|FullyQualifiedName~SchoolAdminScaffoldTests" --logger "console;verbosity=minimal" -m:1
Passed: 14, Failed: 0, Skipped: 0

dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OgmaLibrary.Tests.SchoolAdmin" --logger "console;verbosity=minimal" -m:1
Passed: 44, Failed: 0, Skipped: 0
```

## Residual gates

Physical administrator/student erasure acceptance, retention-policy approval,
backup/restore rehearsal, platform key lifecycle, accessibility, provider soak,
and formal minors DPIA approval remain `NOT ASSESSED`.
