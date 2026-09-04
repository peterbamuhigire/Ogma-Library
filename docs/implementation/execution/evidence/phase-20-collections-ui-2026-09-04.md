# Phase 20 Collection Controls Evidence

Date: 2026-09-04

## Result

The catalogue sidebar now exposes localized create, rename, and delete controls
for user collections. Creation trims the entered name, rejects empty input, uses
the catalogue write boundary, reloads the shelf projection, clears the input,
and reports success or failure. Rename requires a selected collection and a
non-empty name, then reloads the projection and reports success or failure.
Deletion requires a selected collection,
clears the active shelf filter when the selected shelf is removed, reloads the
projection, and reports success or failure. The controls are automation-named
and disabled while the shelf list is loading or the required selection/name is
absent.

Smart-shelf query authoring, file/relink actions, and full
organisation end-to-end acceptance remain open.

## Verification

    dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --no-restore -p:BaseOutputPath=tmp/phase20-collections-ui-build-1/ --filter "FullyQualifiedName~ShelfSidebarTests" --logger "console;verbosity=minimal" --results-directory tmp/phase20-collections-ui-results-1

Result: 1 passed, 0 failed. The test verifies trimmed collection creation,
success status, input reset, selected-collection rename, deletion, filter
clearing, and the corresponding write-boundary calls. The production Release build
also compiles the bound sidebar controls.

## Gate disposition

Closed: basic collection create/rename/delete control sub-gate.

Still open: smart-shelf saved queries, file/relink actions,
complete status/history presentation, provenance/TOC tabs, accessibility, and
end-to-end organisation workflows.
