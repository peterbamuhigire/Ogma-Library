# Phase 18 Evidence - AI Accessibility Copy

Date: 2026-09-05

## Delivered

The AI recommendation/answer panel no longer hard-codes accessibility copy.
Interpreted-intent, citation-opening, evidence-limitation, and one-to-five
answer-rating labels are bound to the shared English/French localization
surface. The view-model raises property changes for these labels when the
culture changes, so an already-open panel does not retain stale language.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AdvisorViewModelTests|FullyQualifiedName~AdvisorViewRenderTests" --logger "console;verbosity=minimal"
```

The core project passed 9 view-model tests. The UI project was then run with:

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AdvisorViewRenderTests" --logger "console;verbosity=minimal"
```

The UI render test passed 1/1. The post-change full solution run passed 1,096
tests: 900 core, 41 architecture, and 155 UI; 0 failures and 0 skips.

## Residual scope

This closes the AI-panel accessibility-copy subgate only. Application-wide
hard-coded copy inventory, contrast snapshots, route inventory, and manual
Narrator/VoiceOver journeys remain open in the Phase 18 progress record.
