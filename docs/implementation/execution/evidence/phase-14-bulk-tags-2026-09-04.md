# Phase 14/20 Bulk Tag Mutation Evidence

Date: 2026-09-04

## Result

`CatalogueWriteService.BulkEditAsync` now executes the previously unimplemented
`TagsToAdd` and `TagsToRemove` contract fields. Tag input is bounded to 32
values per operation, each value is trimmed and limited to 128 characters, and
delimiter injection is rejected. Add/remove matching is case-insensitive and
the resulting set is stored as a user-owned `Tags` metadata value. The bulk
before/after audit payload includes the tag state.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore -p:BaseOutputPath=tmp/phase20-tags-build/ --filter "FullyQualifiedName~ShelfTests" --logger "console;verbosity=minimal" --results-directory tmp/phase20-tags-results
```

Result: 5 passed, 0 failed. The regression adds duplicate tags, verifies
deterministic normalized storage and user provenance, removes a tag using a
different case, and verifies the remaining value.

## Gate disposition

Closed: backend bulk tag add/remove behavior and audit projection.

Still open: metadata-review accessibility UI, tag-management UI, collections,
and full end-to-end organisation workflows.
