# Phase 23 index observability evidence

Date: 2026-09-04

The full-text index lifecycle exposes durable rebuild checkpoints with attempted,
indexed, failed, and chunk counters. The index-manager status event reports
integrity, index size, chunk counts, and OCR/index failure counts. The LAN-ready
search read model publishes per-book indexed/failed events and a completed
rebuild event with total chunks and elapsed duration.

Verification: `IndexManagerServiceTests` passed 6/6, including status/event
publication, rebuild completion, cancellation/resume state, and search read-model
observability assertions.

This closes the Phase 23 observability subgate only. Progress/no-index UI,
side-by-side rebuild swap, and 50,000-book latency evidence remain open.
