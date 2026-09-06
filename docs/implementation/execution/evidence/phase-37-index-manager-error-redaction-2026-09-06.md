# Phase 37 Index Manager Error Redaction

Date: 2026-09-06
Reviewer: Peter Bamuhigire, Lead Consultant

## Finding and correction

The Index Manager could render raw exception, rebuild, integrity, and OCR error
messages. Legacy or adapter messages can contain filesystem paths, provider
details, or token-like values.

The view model now maps those boundaries to localized stable states. Detailed
errors remain internal; the user sees rebuild failed, integrity failed, or OCR
failed without raw diagnostic content.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter "FullyQualifiedName~IndexManagerViewModel"
Passed: 3, Failed: 0, Skipped: 0
```

The regression injects an absolute student path and token-like value through
both integrity and OCR status contracts and proves neither reaches rendered
collections.

## Gate disposition

The Index Manager raw operational-error rendering sub-gate is closed. Physical
operator/accessibility review and controlled internal diagnostic retention
remain release concerns.
