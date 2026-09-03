# Phase 14 Progress - Metadata Review and Manual Curation

Date: 2026-09-04

## Delivered in this increment

- Added durable `MetadataProposals` with source, confidence, current value,
  alternatives, status and decision timestamps.
- Added `IMetadataReviewService` for create/list/accept/reject commands.
- Acceptance is routed through `IMetadataApplyService`; optional edited values
  and explicit user overrides are validated by the existing precedence rules.
- Rejected and already-decided proposals cannot be applied again.
- Added a durable positive `Version` concurrency token to proposal rows and
  translate conflicting decisions into a safe reload-required error.
- Added review-boundary validation that rejects markup, executable URL schemes,
  and non-HTTPS URL values before proposals are persisted or accepted.
- Added tests proving proposals remain pending until explicit review and that
  accepted user edits become protected curation.
- Added focused tests for version defaults and unsafe-value rejection.

## Remaining phase gate

Bulk preview/undo, complete field dictionary coverage, and keyboard/screen-reader
UI journeys remain before phase 14 closure. Concurrency and review-boundary
sanitization are implemented and tested; OS/browser accessibility evidence is
not assessed by this service-layer increment.
