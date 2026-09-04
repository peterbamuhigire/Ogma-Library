# Phase 32 — Search and Advisor Shelf Focus Evidence

Date: 2026-09-04

## Change

Search “open selected” and Advisor “open book”/citation actions now accept an
optional shelf-focus callback. The desktop composition supplies the callback
to the shared `Bookshelf3DViewModel`, which validates the book ID and emits the
typed `FocusBook` bridge message. The callback is optional, so tests, reduced
compositions, and the accessible 2D fallback do not require a WebView.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~SearchViewModelTests.SearchViewModel_QueryDebouncesAndOpenSelectedNavigates|FullyQualifiedName~AdvisorViewRenderTests.AdvisorViews_RenderLoadedRecommendationAndPlan" --logger "console;verbosity=minimal"
```

Result: **2 passed, 0 failed, 0 skipped**. Both tests assert the selected
book ID reaches the shelf-focus callback while existing navigation still runs.

## Remaining gates

Reference confirmation and physical Windows/macOS WebView screenshot,
keyboard, interaction, and accessibility evidence remain **NOT ASSESSED**.
