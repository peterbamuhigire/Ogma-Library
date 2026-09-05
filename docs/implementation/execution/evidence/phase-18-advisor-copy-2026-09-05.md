# Phase 18 Advisor Copy Localization Evidence

Date: 2026-09-05

`RecommendationPanelViewModel.InterpretedIntentText` now obtains its visible
labels and separator from localization resources for topics, exclusions,
level, length, mood, comparison, and broad discovery. English, French, and
pseudo-locale values are covered by the in-memory localization contract.

Verification:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Phase30AdvisorQualityTests"
```

Result: 6 passed, 0 failed, 0 skipped.

The existing English Advisor assertions remain green, while the added test
verifies French labels and pseudo-locale expansion. This closes the Advisor
interpreted-intent copy subgate only; Phase 18 application-wide localization,
contrast, route, and physical accessibility gates remain open.
