# Phase 20 Curation UI Evidence

Date: 2026-09-04

## Result

The desktop book-detail Reading tab now exposes durable controls for the
personal reading status (Unread, Reading, Finished, Set aside), a 1–5 rating,
and the favourite flag. Controls are available only when the curation service
is composed, use the existing validated application contract, refresh the
detail projection after a successful write, and present a localized result
message.

The controls do not expose reading-history reason text or file paths and do not
bypass the service-layer rating and book-identity validation.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --no-restore -p:BaseOutputPath=tmp/phase20-ui-build/ --filter "FullyQualifiedName~BookDetailCurationTests" --logger "console;verbosity=minimal" --results-directory tmp/phase20-curation-results-3
```

Result: 1 passed, 0 failed. The test loads a detail projection, exercises all
three action families, verifies the service calls, and verifies the refreshed
status, rating, favourite state, and success message.

The existing `Phase20BookCurationTests` service suite remains the persistence
proof for progress, rating, favourite, history capture, invalid rating, and
unknown-book rejection.

## Gate disposition

Closed: detail-view status/rating/favourite write controls.

Still open: collections/tags, smart-shelf saved queries, file/relink actions,
complete status/history presentation, lazy TOC and provenance tabs,
accessibility, and end-to-end evidence.
