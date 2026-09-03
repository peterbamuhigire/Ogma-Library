# Phase 16 Evidence - Worker Image Validation

Date: 2026-09-04

## Delivered

The PDF worker client now validates generated cover and spine outputs before
copying them from the sandbox to the sidecar. It bounds output bytes, decodes the
image with SkiaSharp, and requires the exact contract dimensions: 200x300 for a
cover and 7x100 for a spine.

## Verification

```text
dotnet build src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj --configuration Release --no-restore -p:BuildProjectReferences=false
  Passed: 0 warnings, 0 errors

dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~PdfWorker|FullyQualifiedName~Phase10PdfInputBrokerTests" --verbosity minimal -m:1
  Passed: 11, Failed: 0, Skipped: 0
```

## Open gates

Provider/embedded source acquisition, explicit garbage collection, lazy high/low
variants, API authorization, UI journeys, and large-library budget testing are
still open. This test run is not physical cross-platform asset evidence.
