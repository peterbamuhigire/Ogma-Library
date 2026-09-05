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

Add `--worker --repeat=N` to replay each file N times through the persistent
production PDF worker session. This mode reports each worker's peak working set
and private memory, and fails if private memory exceeds the configured 768 MiB
Job Object ceiling. The worker executable is taken from
`OGMA_PDF_WORKER_PATH` when set, otherwise from the repository Release output.

Add `--pipeline --worker --repeat=N` to run the database-backed extraction
pipeline through the production isolated worker. Each repetition uses a fresh
temporary catalogue, so all books are processed again; the report includes
cumulative pages, artifacts, chunks, and maximum worker memory observations.
This closes only the measured worker-backed pipeline subgate for the supplied
corpus; it does not substitute for the representative target-scale corpus.

Without `--pipeline`, this tool measures the PDF adapter boundary. The
pipeline mode exercises the database-backed path, but neither mode substitutes
for target-scale allocation/throughput or reference-machine gates.
