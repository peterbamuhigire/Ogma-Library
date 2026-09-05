# Phase 11 Evidence - Real-PDF Adapter Memory Boundary

Date: 2026-09-05
Reviewer: Peter Bamuhigire, Lead Consultant

## Problem and change

The real-PDF adapter benchmark previously materialised every PdfPig page in a
document-scoped list before extracting one page. On the available seven-file
corpus this produced a 3,821,600,768-byte peak working set. `PdfiumAdapter`
now resolves only the requested page with `PdfDocument.GetPage(pageNumber)`;
the all-pages cache was removed. This preserves the document-scoped parse and
serialised extraction boundary while avoiding retention of the complete page
object graph.

## Windows verification

Compiled benchmark command:

```text
dotnet build scripts/Phase11RealPdfBenchmark/Phase11RealPdfBenchmark.csproj --configuration Release --no-restore --nologo -m:1
dotnet scripts/Phase11RealPdfBenchmark/bin/Release/net10.0/Phase11RealPdfBenchmark.dll C:\Users\Peter\Downloads
```

The compiled extractor process was monitored directly, rather than measuring
the `dotnet run` launcher.

```text
exitCode=0
peakWorkingSetBytes=492982272
fileCount=7
filesWithErrors=0
totalBytes=241109195
totalPages=3326
fullPages=3206
partialPages=53
emptyPages=9
scannedPages=58
totalWords=1057996
elapsedMilliseconds=28815
allocatedBytes=8627320664
```

The result is a 87.1% reduction in observed working set versus the prior
3,821,600,768-byte run, while preserving all extracted counts and zero file
errors. The production isolated PDF worker's configured Windows process-memory
ceiling is 768 MiB (`PdfWorkerOptions.MaxMemoryBytes`), leaving approximately
298 MiB above this local direct-adapter observation.

## Regression verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PdfiumAdapterPasswordTests|FullyQualifiedName~PdfWorkerIsolationTests|FullyQualifiedName~ExtractionPipelineServiceTests" --logger "console;verbosity=minimal"
```

Result: 17 passed, 0 failed, 0 skipped.

The complete post-change solution run passed 1,094 tests: 898 core, 41
architecture, and 155 UI; 0 failures and 0 skips. Release build passed with 0
warnings and 0 errors.

## Gate disposition

The local Windows page-retention/resource subgate is closed for this corpus.
This evidence does not close representative real 500-book acceptance, native
cross-platform memory/throughput proof, real-PDF accuracy acceptance, or
physical UI/accessibility gates. Those remain explicitly open in the Phase 11
progress record.
