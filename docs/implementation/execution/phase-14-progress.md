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
- Added a shared canonical field dictionary covering provider, review, catalogue,
  and PDF write-back fields with explicit work/edition scope classification.
- Added a bounded durable bulk-review command: server-created previews revalidate
  proposal versions, apply all selected proposals in one transaction, and record
  before/after snapshots in the append-only audit stream.
- Added one-time token-protected undo that refuses to overwrite later metadata
  edits and recalculates quality after apply and undo.
- Added focused atomicity, stale-preview, restore, repeat-undo, and later-edit
  conflict tests; all 11 Phase 14 metadata tests pass locally.
- Completed the previously missing bulk tag add/remove behavior with bounded,
  normalized user metadata storage and tag-inclusive before/after audit state;
  the Shelf integration suite passes 5/5.
- Added a book-scoped metadata review panel with localized accept/reject
  controls, editable values, explicit user-override routing for edits, and
  stale/validation failure feedback. Controls expose automation names and
  accept keyboard focus.

## Remaining phase gate

The backend bulk preview/apply/undo and tag-mutation subgates are closed. The
service-layer and desktop review UI sub-gates are also closed. Concurrency and
review-boundary sanitization are implemented and tested; physical
OS/browser/screen-reader evidence remains outside this local headless proof.

The Aug-39 Definition of Done now records the field/editability contract,
visible provenance state, mandatory review, and reversible bulk workflow as
closed. The physical keyboard/screen-reader journey remains unchecked.

Focused Phase 14 metadata verification on 2026-09-06: **6 passed, 0 failed,
0 skipped**.
