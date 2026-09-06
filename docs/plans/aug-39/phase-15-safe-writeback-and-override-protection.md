# Phase 15 — Safe Writeback and Override Protection

> [Roadmap index](./README.md) · [Previous](./phase-14-metadata-review-and-manual-curation.md) · [Next](./phase-16-cover-thumbnail-and-spine-assets.md)

## Objective
Replace automatic PDF modification with explicit, backed-up, reversible writeback.

## Business/Product Rationale
Ogma must never surprise users by altering source books.

## SDLC Requirements
FR-META-005, CTRL-011, NFR-PROD-013 and user-override precedence.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Metadata/BookMetadataEnrichmentService.cs` can automatically write accepted metadata to writable PDFs.

## Gap Analysis
No consent gate, robust backup/restore, rehash/invalidation transaction or external-change conflict detection.

## Architectural Impact
Separate catalogue acceptance from optional file writeback command.

## Database Work
Writeback plan/audit, source hash, backup locator/hash, result, restore status and actor.

## Backend Work
Preview diff, confirm, root permission check, exclusive change check, backup, atomic replace, rehash and invalidation.

## Frontend Work
Prominent opt-in warning, exact fields/file, backup location, progress and restore action.

## PDF Processing Impact
Revalidate and regenerate derived artifacts after confirmed writeback.

## Metadata Impact
Catalogue edits remain valid even when writeback is skipped/fails.

## Search Impact
Reindex only if file content actually changes.

## AI/RAG Impact
Invalidate content-derived artifacts by new asset hash.

## 3D Bookshelf Impact
Refresh assets only after successful pipeline completion.

## External Integrations
None.

## Privacy Requirements
Backups remain local, documented and deletable.

## Security Requirements
Only selected roots; traversal/symlink/external-change checks; least privilege; no silent overwrite.

## Performance Requirements
Streaming copy/hash; UI remains responsive.

## Error & Recovery Behaviour
Any failure preserves original and backup; interrupted operation is recoverable and auditable.

## Logging/Observability
File/root IDs, hashes, result and duration; paths redacted in export.

## Testing
Unit plan validation; DB audit; filesystem permission/change/interruption/restore tests on both OSes; pipeline rehash/invalidation; UI consent E2E; large-file performance; security traversal tests.

## Skills Engines Applied
`skills-web-dev` safe file transactions; platform admin engines; `srs-skills` destructive-operation controls.

## Dependencies
Phases 8 and 12–14.

## Parallelisation
Backup/write engine and consent UX may proceed from a frozen command contract.

## Migration Considerations
Disable legacy automatic path before any new code ships; retain prior audit evidence.

## Definition of Done
- [x] No enrichment path writes automatically.
- [x] Preview and explicit confirmation are mandatory.
- [x] Backup/restore and external-change detection pass.
- [x] Hash/artifact invalidation is atomic.
- [x] Metadata-contract freeze recorded.

## Kaizen Review
1. Complexity: safe transaction. 2. Consolidate file mutations. 3. Simplify enrichment. 4. Delete automatic writeback. 5. Document recovery. 6. Pattern: confirm-backup-verify. 7. Debt decreases critically.
