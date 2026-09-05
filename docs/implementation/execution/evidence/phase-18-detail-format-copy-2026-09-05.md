# Phase 18 Detail Format Copy Evidence

Date: 2026-09-05

Book-detail metadata display now obtains missing-value, field, provenance,
default-source, and manual-override labels from the localization service. The
English resource values preserve the existing output contract; French and
pseudo-locale values are covered by the shared resource test.

Verification:

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BookDetailCurationTests|FullyQualifiedName~BookDetailFileAndProvenanceTests|FullyQualifiedName~Phase18DesignSystemTests"
```

Result: 11 passed, 0 failed, 0 skipped.

This closes the detail-format copy subgate only. Phase 18 application-wide
copy coverage, contrast, route inventory, and physical accessibility remain
open.
