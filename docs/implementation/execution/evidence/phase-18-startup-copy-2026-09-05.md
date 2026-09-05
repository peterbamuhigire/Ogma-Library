# Phase 18 Startup Copy Localization Evidence

Date: 2026-09-05

Startup migration progress now uses localized resource keys for both the
indeterminate preparation message and the completed/total progress format.
The view model retains the counters and recomputes the visible text when the
active culture changes, so an in-flight migration does not remain in stale
language.

Verification:

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Phase18DesignSystemTests|FullyQualifiedName~StartupShellRenderTests"
```

Result: 6 passed, 0 failed, 0 skipped. The Phase 18 resource contract covers
English, French, and pseudo-locale values, and the existing startup rendering
proof remains green.

This closes the startup migration copy subgate only. Application-wide copy
coverage, contrast, route inventory, and physical accessibility remain open.
