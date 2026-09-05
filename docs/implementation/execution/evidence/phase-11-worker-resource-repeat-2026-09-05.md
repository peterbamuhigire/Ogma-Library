# Phase 11 Evidence - Persistent PDF Worker Resource Repetition

Date: 2026-09-05
Reviewer: Peter Bamuhigire, Lead Consultant

## Scope

The seven-file Windows corpus previously used for the Phase 11 real-PDF
benchmark was replayed three times per file through the production
`PdfWorkerClient.PdfWorkerSession` boundary. Each session used the configured
768 MiB Windows Job Object memory ceiling and 15-second CPU ceiling. The
benchmark reports worker peak working set and private memory before disposal;
it does not persist extracted text or corpus content.

## Verification

```powershell
dotnet build src/OgmaLibrary.Workers/OgmaLibrary.Workers.csproj --configuration Release --no-restore --nologo -m:1
dotnet build scripts/Phase11RealPdfBenchmark/Phase11RealPdfBenchmark.csproj --configuration Release --no-restore --nologo -m:1
dotnet scripts/Phase11RealPdfBenchmark/bin/Release/net10.0/Phase11RealPdfBenchmark.dll <seven-file-corpus> --worker --repeat=3
```

Result:

```text
fileCount=7
repeatCount=3
runs=21
runsWithErrors=0
maxPeakWorkingSetBytes=247021568
maxPrivateMemoryBytes=195772416
maxMemoryBytes=805306368
```

Every file returned the same page and word counts on all three repetitions:
3,326 pages and 1,057,996 words in aggregate. The largest worker observation
was 247,021,568 bytes peak working set and 195,772,416 bytes private memory,
both below the configured 805,306,368-byte ceiling. The slowest individual
run was 13,818 ms, below the 15-second per-worker CPU budget; no worker was
killed or returned an extraction error.

## Gate disposition

The repeated per-book resource-ceiling subgate for the persistent production
PDF worker extraction session is closed on Windows for this corpus. This does
not close representative real 500-book acceptance, database-pipeline
target-scale proof, native cross-platform measurements, physical UI/accessibility,
or independent security approval.
