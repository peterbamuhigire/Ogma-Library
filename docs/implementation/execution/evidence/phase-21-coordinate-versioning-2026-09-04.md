# Phase 21 — Annotation Coordinate Versioning Evidence

Date: 2026-09-04

## Scope

This record closes the local coordinate-version compatibility gate. It does
not claim system-viewer, physical crash/accessibility, or cross-platform
performance evidence.

## Implemented controls

| Control | Evidence |
| --- | --- |
| Explicit representation | `AnnotationV2` and `AnnotationV2Row` carry `CoordinateVersion`; current rows use `normalized-v1`, normalized to the un-rotated page model. |
| Legacy fallback | Empty or omitted persisted versions normalize to `normalized-v1`, preserving existing Phase 09 coordinates. |
| Unsupported-version safety | Repository mapping keeps the marker but returns an empty region list, so an unknown coordinate system cannot render in the wrong place. |
| Migration | `20260904230000_Phase21AnnotationCoordinateVersion` adds the non-null column with the normalized default. |
| Portable state compatibility | Reader-state export includes the coordinate version; import accepts omitted legacy values and skips unsupported versions. |

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase09AnnotationTests|FullyQualifiedName~Phase21AnnotationCoordinateTests" --logger "console;verbosity=minimal"
```

Result: **36 passed, 0 failed, 0 skipped**.

The application/infrastructure build passed with **0 warnings and 0 errors**.

## Remaining gates

- System viewer actions, physical crash/accessibility journeys, and named
  cross-platform performance budgets: **NOT ASSESSED**.
