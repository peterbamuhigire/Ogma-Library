# Phase 17 runtime metrics evidence

Date: 2026-09-04

`IJobRuntimeService.GetMetricsAsync` returns status totals, total attempts, and
active lease counts grouped by job type. It projects only operational fields;
job payloads, paths, error text, and lease owners are not exposed by the
metrics contract.

`ExportDiagnosticsJsonAsync` adds a maximum of 100 recent operational job
records to the metrics snapshot and applies the same redaction boundary.

Verification: `Phase17JobRuntimeTests` passed, 7 tests total, including the
redacted metrics snapshot.

Remaining Phase 17 gate is kill/restart load evidence;
search-extraction and embedding workers also remain stage-based until their
queue conversion is separately implemented and verified.
