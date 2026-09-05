# Phase 11 Evidence - Database-Backed Worker Pipeline

Date: 2026-09-05
Reviewer: Peter Bamuhigire, Lead Consultant

## Scope

The real-PDF benchmark was extended so its database-backed mode can use the
same `IsolatedPdfRendererFactory` registered by the desktop application. Each
repetition creates a fresh temporary SQLite catalogue and processes every
fixture again, preventing a previously indexed row from making a repeat a
false no-op.

## Verification

```text
dotnet build scripts/Phase11RealPdfBenchmark/Phase11RealPdfBenchmark.csproj --configuration Release --no-restore --nologo -m:1
dotnet scripts/Phase11RealPdfBenchmark/bin/Release/net10.0/Phase11RealPdfBenchmark.dll spikes/s02-pdfium/fixtures --pipeline --worker --repeat=2
```

Result:

```text
fileCount=3
repeatCount=2
runs=2
booksAttempted=6
booksIndexed=6
booksFailed=0
pagesProcessed=606
failedPages=0
extractedPages=606
extractionArtifacts=6
searchChunks=946
maxPeakWorkingSetBytes=82014208
maxPrivateMemoryBytes=45330432
maxMemoryBytes=805306368
```

The focused worker-isolation suite also passed 9 tests, including a regression
that proves resource telemetry is recorded when a worker-backed reader session
is disposed.

## Gate disposition

The database-backed isolated-worker pipeline subgate is CLOSED for this real
repository fixture corpus. This evidence does not close representative real
500-book acceptance, target-scale throughput, native cross-platform
measurements, physical UI/accessibility, or independent security approval.
