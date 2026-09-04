# Phase 20 Smart-Shelf Query Evidence

Date: 2026-09-04

## Scope

The saved smart-shelf contract is now exercised through the catalogue read and
write boundaries. Smart shelves use the closed `SmartShelfField` and
`SmartShelfOperator` enums; no dynamic expressions or executable query content
are accepted.

## Delivered

- Added bounded JSON parsing and validation for persisted smart-shelf condition
  arrays (maximum 32 conditions, 128 characters per value, 4,096 characters
  per query).
- Smart-shelf creation rejects malformed or unsupported query JSON and virtual
  shelves do not persist an unused query.
- Catalogue reads load the selected shelf type and evaluate smart conditions
  against the projected catalogue, with paging applied after evaluation.
- Smart-shelf book counts are calculated from the saved query rather than the
  manual `ShelfBooks` join table.
- The closed condition set is translated to server-side predicates for rating,
  status, year, availability, and reading progress before projection; the
  in-memory evaluator remains as a defense-in-depth semantic check.
- Damaged or untrusted smart-shelf queries fail closed to zero results and a
  zero displayed count.

## Verification

Focused verification:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore
  --filter "FullyQualifiedName~CatalogueReadModelTests|FullyQualifiedName~SmartShelfEvaluatorTests|FullyQualifiedName~ShelfTests"
  -p:BaseOutputPath=tmp/phase20-smart-shelf-build/
  --logger "console;verbosity=minimal"
  --results-directory tmp/phase20-smart-shelf-results/
```

Result: 25 passed, 0 failed, 0 skipped across the evaluator, read-model, and
write-path tests.

Full isolated solution validation:

```text
dotnet test OgmaLibrary.sln --no-restore
  -p:BaseOutputPath=tmp/full-suite-build-2026-09-04-phase20-smart-shelf/
  --logger "console;verbosity=minimal"
  --results-directory tmp/full-suite-results-2026-09-04-phase20-smart-shelf/
```

Result: 880 core + 41 architecture + 142 UI = 1,063 passed, 0 failed,
0 skipped after the server-side predicate optimization.

## Remaining phase 20 gates

File/relink actions, complete status/history presentation, lazy TOC and
provenance tabs, physical accessibility, and end-to-end evidence remain open.
