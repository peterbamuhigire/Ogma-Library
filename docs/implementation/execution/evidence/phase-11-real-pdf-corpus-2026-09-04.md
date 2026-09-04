# Phase 11 Evidence — Real PDF Adapter Corpus

Date: 2026-09-04
Reviewer: Peter Bamuhigire, Lead Consultant

## Decision

The real-PDF adapter corpus subgate is closed for the locally available
Windows corpus. The reusable benchmark processed seven PDFs without a file
error, covering text-heavy, standards, illustrated, and duplicate-content
documents. The benchmark reports resource measurements without persisting
extracted private content.

This does not close the full Phase 11 gate: the roadmap still requires a
representative target-scale corpus and an approved per-book allocation ceiling
for the complete database-backed extraction pipeline. Those facts are not
invented from this seven-file adapter run.

## Verification

Command:

```text
dotnet build scripts/Phase11RealPdfBenchmark/Phase11RealPdfBenchmark.csproj --configuration Release --no-restore --nologo -m:1
dotnet run --project scripts/Phase11RealPdfBenchmark/Phase11RealPdfBenchmark.csproj --configuration Release --no-build --no-restore -- C:\Users\Peter\Downloads
```

Result:

```text
files=7
filesWithErrors=0
bytes=241109195
pages=3326
words=1057996
elapsedMilliseconds=30411
allocatedBytes=8626841648
fullPages=3206
partialPages=53
emptyPages=9
scannedPages=58
```

The focused PDF/extraction regression slice passed 11/11 with no failures or
skips after the adapter changed to reuse a document-scoped PdfPig page parse
and serialize access to it. A verification rerun completed the same corpus in
27.149 seconds with 8,628,472,328 allocated bytes and zero file errors.
