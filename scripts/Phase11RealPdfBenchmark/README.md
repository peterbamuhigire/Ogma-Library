# Phase 11 real-PDF benchmark

Run the bounded text-layer adapter benchmark against a local corpus:

```text
dotnet run --project scripts/Phase11RealPdfBenchmark/Phase11RealPdfBenchmark.csproj -- C:\path\to\pdfs
```

Add `--pipeline` to exercise the database-backed extraction pipeline in a
temporary SQLite catalogue and report indexed pages, artifacts, and chunks:

```text
dotnet run --project scripts/Phase11RealPdfBenchmark/Phase11RealPdfBenchmark.csproj -- C:\path\to\pdfs --pipeline
```

The command emits JSON to standard output and never writes extracted text or
corpus content. It reports file size, page count, page-quality distribution,
word count, elapsed time, allocations, and per-file errors. A non-zero exit
code means at least one file could not be processed.

Without `--pipeline`, this tool measures the PDF adapter boundary. The
pipeline mode exercises the database-backed path, but neither mode substitutes
for target-scale allocation/throughput or reference-machine gates.
