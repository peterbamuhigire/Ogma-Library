# Phase 14 Evidence - Proposal Concurrency and Input Safety

Date: 2026-09-04

## Scope

This increment closes the service-layer gates for optimistic proposal versioning
and review-boundary value safety. It does not close the phase: bulk preview/undo,
complete field dictionary coverage, and keyboard/screen-reader UI journeys remain
open.

## Implementation evidence

- `MetadataProposalRow.Version` is persisted with a positive default and mapped
  as an EF optimistic concurrency token.
- Proposal decisions increment `Version` and translate
  `DbUpdateConcurrencyException` into a reload-required review error.
- Proposal creation and acceptance reject markup, executable URL schemes, and
  non-HTTPS absolute URL values.
- Migration `20260904120000_Phase14ProposalConcurrency` adds the durable column.

## Verification

```text
dotnet build src/OgmaLibrary.Application/OgmaLibrary.Application.csproj --configuration Release --no-restore
  Passed: 0 warnings, 0 errors

dotnet build src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj --configuration Release --no-restore -p:BuildProjectReferences=false
  Passed: 0 warnings, 0 errors

dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~Phase14MetadataReviewTests|FullyQualifiedName~Phase12MetadataPrecedenceTests|FullyQualifiedName~ExtractionPipelineServiceTests" --verbosity minimal -m:1
  Passed: 17, Failed: 0, Skipped: 0
```

## Open evidence

- No browser/keyboard/screen-reader walkthrough was performed in this service
  verification.
- Bulk operation preview, commit, and reversible undo commands are not yet
  implemented.
