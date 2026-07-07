# Phase 05 PDF Worker Render Smoke

Date: 2026-07-07

Purpose: record a Phase 05 smoke measurement after moving production PDF rendering
behind the `OgmaLibrary.Workers pdf-worker` subprocess boundary. This is not a
reference-hardware NFR gate; Phase 16/20 remain responsible for page-turn and
render-budget measurements on W-REF-01 and M-REF-01.

Command:

```powershell
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PdfWorkerIsolationTests.IsolatedPdfRenderer_ValidPdf_RendersThroughWorker" --logger "console;verbosity=normal"
```

Result:

- Passed: 1 / 1.
- Test body duration reported by xUnit: 1 s.
- Total VSTest time: 2.5907 s.

Interpretation: the isolated worker can launch, render a synthetic one-page PDF,
return PNG bytes, and exit within the Phase 05 smoke-test envelope. No product
performance budget is claimed from this measurement.
