# Phase 11 extraction-bounds evidence — 2026-09-04

## Change

The PDFium/PdfPig text-layer adapter now selects only the requested page instead
of materializing every page in a document. Word extraction is streamed with
explicit per-page bounds: 100,000 words and 4,096 characters per word. Text is
Unicode-normalized, coordinates are clamped to the normalized page rectangle,
and truncation is reported as `Partial` quality rather than silently presented
as complete extraction.

This is an implementation/resource-budget improvement. It does not substitute
for the real target-scale mixed-PDF corpus and native memory measurements.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore --filter "FullyQualifiedName~Phase11" --logger "console;verbosity=minimal"
```

Result: **PASS — 11 passed, 0 failed, 0 skipped**.

The existing Phase 11 suite verifies versioned/idempotent artifacts, ranked ISBN
evidence, bounded Unicode TOC extraction, and malformed-PDF handling. The Debug
build completed without compiler errors.

Current-HEAD rerun of the Phase 10 broker/isolation and Phase 11 extraction
selectors passed 27 combined tests, with 0 failures and 0 skips.

## Remaining gates

- A representative real 500-book mixed-quality corpus is still required.
- Native peak memory, throughput, and very-large-document measurements remain
  `NOT ASSESSED`.
- Physical UI status/reprocess controls and cross-platform PDF conformance remain
  open.
