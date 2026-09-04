# Phase 20 Tag Editor Evidence

Date: 2026-09-04

## Result

The desktop book-detail Bibliographic tab now provides a bounded tag editor.
It accepts comma, semicolon, or pipe-separated values, removes duplicates
case-insensitively, limits the list to 32 tags of at most 128 characters each,
and persists the canonical value through
`ICatalogueWriteService.UpdateMetadataFieldAsync` as user-owned metadata.
After persistence the detail projection is reloaded and the view reports a
localized success or failure message. The editor is hidden when the write
boundary is not composed.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --no-restore -p:BaseOutputPath=tmp/phase20-tags-ui-build-3/ --filter "FullyQualifiedName~BookDetailCurationTests" --logger "console;verbosity=minimal" --results-directory tmp/phase20-tags-ui-results-3
```

Result: 3 passed, 0 failed. The tag-specific test verifies normalized input,
the exact write-boundary call, refreshed tags and localized status, and the
rendered textbox and save button after selecting the Bibliographic tab.

## Gate disposition

Closed: bounded book-detail tag editor and its local write/refresh/render proof.

Still open: collections, smart-shelf saved queries, file/relink actions,
complete status/history presentation, lazy TOC and provenance tabs,
accessibility, and end-to-end organisation workflows.
