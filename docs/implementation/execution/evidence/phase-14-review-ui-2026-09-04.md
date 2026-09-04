# Phase 14 Metadata Review UI Evidence

Date: 2026-09-04

## Result

The desktop book-detail Bibliographic tab now displays pending metadata
proposals for the loaded book. Each proposal shows the field, proposed value,
current value, source, and confidence; the proposed value may be edited before
acceptance. Accept and reject actions route through IMetadataReviewService.
An edited value is explicitly marked as a user override. Failures, including
stale proposal decisions and service validation failures, are surfaced as a
localized status message while pending cards remain reloadable.

The review controls use localized automation names and are keyboard focusable.
This evidence is limited to the Avalonia headless UI; physical operating-system,
screen-reader, and browser accessibility acceptance remains open.

## Verification

    dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --no-restore -p:BaseOutputPath=tmp/phase14-review-ui-build-5/ --filter "FullyQualifiedName~BookDetailCurationTests" --logger "console;verbosity=minimal" --results-directory tmp/phase14-review-ui-results-5

Result: 4 passed, 0 failed. The review-specific test verifies pending proposal
loading, rendered proposal controls, automation naming, keyboard focus,
edited-value/user-override routing, removal after decision, and localized
success feedback. The same focused class retains the Phase 20 curation and tag
editor proofs.

## Gate disposition

Closed: bounded desktop metadata review journey and local accessibility
sub-gate.

Still open: physical OS/browser/screen-reader acceptance and the broader
release validation gates.
