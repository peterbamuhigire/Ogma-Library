# Phase 29 Content-Aware Consent Evidence

Date: 2026-09-04

## Result

The Advisor answer surface now exposes an explicit, unchecked-by-default
consent control for local page and note evidence. The view model passes that
choice into the existing `AnswerRequest.AllowContentAwareTier` boundary; an
answer request cannot silently escalate from metadata-only evidence.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore -p:BaseOutputPath=tmp/phase29-citation-build/ --filter "FullyQualifiedName~AdvisorViewModelTests" --logger "console;verbosity=minimal" --results-directory tmp/phase29-citation-results
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --no-restore -p:BaseOutputPath=tmp/phase29-consent-ui-build-2/ --filter "FullyQualifiedName~AdvisorViewRenderTests" --logger "console;verbosity=minimal" --results-directory tmp/phase29-consent-ui-results-2
```

Result: 7 core tests and 1 headless UI test passed, with 0 failures. The core
consent regression verifies a default false request and a true request only
after the user model is explicitly checked; the headless render locates the
unchecked consent control in the actual Advisor view.

## Gate disposition

Closed: local answer content-aware-consent wiring.

Still open: human-labelled unsupported-claim/abstention benchmarks and
physical accessibility evidence.
