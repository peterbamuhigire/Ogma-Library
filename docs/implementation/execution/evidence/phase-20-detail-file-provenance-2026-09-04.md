# Phase 20 — Detail File, TOC and Provenance Evidence

Date: 2026-09-04

## Scope

This evidence closes the local detail-panel gates for useful missing-file
presentation and lazy TOC/provenance access. It does not claim a physical file
relink-picker test, accessibility audit, or end-to-end desktop evidence.

## Implemented controls

| Control | Evidence |
| --- | --- |
| Missing files remain useful | `FileAvailabilityText` reports an unavailable file while the metadata projection remains bound. |
| Missing files cannot launch the reader | `CanOpenReader` is false and `OpenReaderAsync` returns before navigation. |
| TOC is genuinely lazy | No locator or extractor call occurs during detail load; both run only from `LoadTocAsync`. |
| TOC is bounded and path-safe | The existing `IBookFileLocator` resolves the file; the view model retains at most 500 extracted rows and skips extraction for a null path. |
| Provenance is genuinely lazy | `ProvenanceRows` remains empty until `LoadProvenance` is explicitly called. |
| Localized presentation | New Contents, Provenance, file-state, and lazy-load labels are present in the English and French in-memory catalogues. |
| Durable root recovery is wired | `ChooseFolderAsync` matches the previous settings path to a durable root and calls `RelinkAsync` to preserve its identity; a new path uses `EnsureForLegacyPathAsync` before settings persistence. |

## Verification

```text
dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration Debug --no-restore
```

Result: **0 warnings, 0 errors**.

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~BookDetailFileAndProvenanceTests" --logger "console;verbosity=minimal"
```

Result: **2 passed, 0 failed, 0 skipped**.

The shell navigation/root-recovery regression slice also passed:

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~ShellReaderNavigationTests|FullyQualifiedName~BookDetailFileAndProvenanceTests" --logger "console;verbosity=minimal"
```

Result: **15 passed, 0 failed, 0 skipped**.

## Remaining gates

- Physical relink-picker and real missing-file recovery: **NOT ASSESSED**.
- Accessibility audit and end-to-end desktop interaction evidence: **NOT ASSESSED**.
