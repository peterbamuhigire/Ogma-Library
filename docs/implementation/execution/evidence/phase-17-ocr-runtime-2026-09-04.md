# Phase 17 OCR worker lease evidence

Date: 2026-09-04

`OcrJobProcessor` now claims OCR work through `IJobRuntimeService`, completes
only the worker-owned lease, and routes processing failures through the bounded
typed retry policy. Existing per-page progress and resume behavior is retained;
legacy running rows with no lease expiry are treated as recoverable. Raw OCR/PDF
exception text is not sent to the runtime failure message.

Verification: `OcrJobProcessorTests` passes 4/4, including interrupted-job
recovery without duplicate pages.

Remaining Phase 17 gates are structured metrics, diagnostics export, and
kill/restart load evidence.
