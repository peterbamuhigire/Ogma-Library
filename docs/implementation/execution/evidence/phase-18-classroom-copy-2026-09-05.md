# Phase 18 Classroom Copy Localization Evidence

Date: 2026-09-05

## Scope

This increment closes the named copy debt in F-UI-002 for the Student Smart
Search and book-detail surfaces. It does not claim application-wide
localization, physical accessibility, contrast, or route-inventory closure.

## Implementation proof

- `StudentSmartSearchView.axaml` no longer contains literal user-facing
  headings, action labels, watermarks, accessibility names, or grounding copy.
- `BookDetailView.axaml` no longer contains the named literal tag hint or
  reading summary formats.
- `StudentSmartSearchViewModel` exposes resource-backed labels and status
  copy, subscribes to culture changes, and disposes its subscription.
- `BookDetailViewModel` exposes resource-backed tag/reading summaries and
  notifies bound localized properties when the culture changes.
- English, French, and pseudo-locale entries are present for the new resource
  keys; missing-key marker checks are included in the focused test.

## Verification

Focused command:

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter "FullyQualifiedName~StudentSmartSearchViewModelTests|FullyQualifiedName~Phase18DesignSystemTests|FullyQualifiedName~BookDetailCurationTests|FullyQualifiedName~BookDetailFileAndProvenanceTests"
```

Result: 16 passed, 0 failed, 0 skipped.

Full-solution command:

```text
dotnet test OgmaLibrary.sln --configuration Release --no-restore
```

Result: 895 core tests passed, 41 architecture tests passed, and 155 UI tests
passed. One timing-sensitive LAN catalogue P95 test reported 2,558 ms against
the 2,000 ms threshold during the concurrent full run; the same test passed in
an isolated rerun (1 passed, 0 failed). This remains an environment/performance
gate and is not attributed to the localization change.

A subsequent complete current-head run passed 1,093 tests (897 core, 41
architecture, 155 UI), with 0 failures and 0 skips.

## Gate disposition

- F-UI-002: resolved for its named Student Smart Search/book-detail scope;
  application-wide copy debt remains tracked under Phase 18.
- F-DOC-002: resolved by the canonical ledger, phase progress records, and
  explicit evidence language distinguishing local completion from open physical
  or release gates.
- Phase 18: remains `IN PROGRESS` because application-wide copy coverage,
  contrast, route inventory, and physical accessibility remain open.
