# Phase 21 Reader Portability Bounds Evidence

Date: 2026-09-04

## Delivered

- Reader-state import now maps malformed JSON to a documented
  `InvalidDataException` instead of leaking serializer-specific failures.
- Imports retain the existing 8 MiB byte bound and same-book/schema checks.
- Imports are bounded to 10,000 bookmarks and 2,000 annotations before any
  persistence work begins.
- Missing bookmark/annotation arrays remain backward-compatible and are treated
  as empty collections.

## Verification

Focused `Phase21ReaderPortabilityTests`: 3 passed, 0 failed, 0 skipped.

Full isolated solution validation:

```text
dotnet test OgmaLibrary.sln --no-restore
  -p:BaseOutputPath=tmp/full-suite-build-2026-09-04-phase21-portability/
  --logger "console;verbosity=minimal"
  --results-directory tmp/full-suite-results-2026-09-04-phase21-portability/
```

Result: 881 core + 41 architecture + 142 UI = 1,064 passed, 0 failed,
0 skipped.

## Remaining phase 21 gates

Functional split view, complete import/export UI, coordinate-version fallback,
platform viewer actions, physical Narrator/VoiceOver journeys, and
cross-platform performance budgets remain open.
