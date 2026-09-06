# Phase 37 Scan Health Payload Redaction

Date: 2026-09-06
Reviewer: Peter Bamuhigire, Lead Consultant

## Finding and correction

`ScanHealthService` documented a relative-path projection but populated it from
the raw job payload. Depending on job type, that payload can be an absolute
path or serialized request data. The same projection also returned persisted
free-form error messages.

Scan health now returns only opaque `book:<id>` or `job:<id>` source references
and stable failure codes. It never projects job payloads or stored error text.
Metadata-gap reporting also no longer performs an unused per-book file lookup.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ScanHealthTests"
Passed: 4, Failed: 0, Skipped: 0
```

The regression stores an absolute student path and token-like value in the job
payload/error. The returned health item contains only its opaque job reference
and `thumbnail_render_failed` category.

## Gate disposition

The scan-health payload/free-form-error disclosure sub-gate is closed. Physical
operator review and broader hostile/legacy database corpus evidence remain
within the Phase 37 release gates.
