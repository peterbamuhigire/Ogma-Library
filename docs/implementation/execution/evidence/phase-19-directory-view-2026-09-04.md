# Phase 19 Directory View Evidence

Date: 2026-09-04

## Result

The desktop catalogue now has a functional directory mode. The view consumes
the shared filtered catalogue collection, displays each book's library-root-
relative source path alongside title, author, and year, and opens the selected
book through the existing reader navigation command on double-click. The list
uses virtualization so the view does not materialize a separate unbounded UI
tree for the catalogue.

The relative path is an optional desktop projection field populated from the
primary local book file. LAN-facing projection mappings remain explicit, so the
desktop source path is not automatically exposed through the classroom host.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --no-restore --filter "FullyQualifiedName~CatalogueDirectoryViewRenderTests" --logger "console;verbosity=minimal" --results-directory tmp/phase19-directory-results-2
```

Result: 1 passed, 0 failed.

The test constructs a local catalogue projection containing a root-relative PDF
path, switches the catalogue to directory mode, and renders the view through
the headless Avalonia test host. The production projection mapping was reviewed
to confirm that `CatalogueReadModel` and `MetadataQualityService` populate the
field only for the desktop summary path.

## Gate disposition

Closed: functional directory-view delivery.

Still open: full grid/list parity, persisted filter and sort views, UI paging
wiring, processing/quality badges, complete cover-source fallback, API asset
authorization, keyboard/screen-reader journeys, and named reference hardware.
