# Phase 11 real-PDF benchmark

Run the bounded text-layer adapter benchmark against a local corpus:

```text
dotnet run --project scripts/Phase11RealPdfBenchmark/Phase11RealPdfBenchmark.csproj -- C:\path\to\pdfs
```

The command emits JSON to standard output and never writes extracted text or
corpus content. It reports file size, page count, page-quality distribution,
word count, elapsed time, allocations, and per-file errors. A non-zero exit
code means at least one file could not be processed.

This tool measures the PDF adapter boundary. It is not a substitute for the
database-backed target-scale extraction-pipeline and reference-machine gates.
