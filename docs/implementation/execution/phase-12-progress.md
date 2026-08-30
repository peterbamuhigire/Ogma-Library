# Phase 12 Progress - Canonical Metadata and Provenance

Date: 2026-08-30

## Delivered in this increment

- Enforced user override precedence in `MetadataApplyService`: ordinary provider
  proposals cannot overwrite a field marked `IsOverridden`.
- Explicit user edits remain authoritative only when submitted as
  `UserOverride` with confidence `1.0`.
- Added length, source, field-name and confidence validation before persistence.
- Added regression tests for provider rejection after curation, explicit user
  replacement, and invalid override metadata.

## Remaining phase gate

Work/edition scope separation, normalized contributor roles, proposal-only
extraction/provider writes, versioned confidence calibration, and a complete
provenance presentation contract remain before phase 12 closure.
