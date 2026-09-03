# Phase 12 Progress - Canonical Metadata and Provenance

Date: 2026-09-04

## Delivered in this increment

- Enforced user override precedence in `MetadataApplyService`: ordinary provider
  proposals cannot overwrite a field marked `IsOverridden`.
- Explicit user edits remain authoritative only when submitted as
  `UserOverride` with confidence `1.0`.
- Added length, source, field-name and confidence validation before persistence.
- Added regression tests for provider rejection after curation, explicit user
  replacement, and invalid override metadata.
- Added an executable work/edition field-scope dictionary and persisted scope
  on every review proposal.
- Versioned the confidence calibration model on merged and persisted proposals.
- Changed provider enrichment to create pending proposals only; canonical fields
  and PDF write-back are reached only through explicit review decisions.
- Preserved normalized contributor handling through the existing Author role
  and ISBN value-object validation boundaries.

## Remaining phase gate

Closed. Field scope, override precedence, normalized identifiers/contributor
roles, proposal-only provider enrichment, confidence-model versioning, and the
provenance-bearing review projection are executable and covered by tests.
Physical UI walkthroughs remain owned by later platform/release phases.
