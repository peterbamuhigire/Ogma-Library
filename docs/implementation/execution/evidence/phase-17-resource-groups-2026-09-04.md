# Phase 17 resource-group capacity evidence

Date: 2026-09-04

Atomic job claims now enforce bounded concurrency for heavy shared resources:
OCR/PDF rendering share a one-job `document-render` group, metadata and search
extraction share a one-job `metadata-index` group, and embedding generation uses
a one-job `semantic-index` group. Unknown job types default to one active lease
per type. Expired leases do not consume capacity.

Verification: `Phase17JobRuntimeTests.Claim_EnforcesHeavyWorkResourceGroupCapacity` passes.

Remaining Phase 17 gates are conversion of the remaining polling workers,
structured metrics, diagnostics export, and kill/restart load evidence.
