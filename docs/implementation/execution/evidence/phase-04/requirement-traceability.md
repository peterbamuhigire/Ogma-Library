# Phase 4 requirement traceability

| Requirement | Workflow and implementation evidence | Failure/recovery behavior | Test evidence | Phase status |
| --- | --- | --- | --- | --- |
| FR-LIB-003 | Legacy BookId -> alias -> catalogue/work/edition; BookFiles -> root occurrence; valid hash -> content asset | Invalid/missing hash remains unknown; migration re-entry is idempotent | Phase04 curated/alias/restart tests | COMPLETE for migration contract; scanner/reconciliation remain Phases 5-9 |
| FR-LIB-004 | Availability is stored on each occurrence and legacy curation is not deleted | Unavailable occurrence remains linked and metadata remains; backup restore protects failed migration | Phase04 unavailable occurrence test; MigrationTests backup/repair tests | COMPLETE for migration contract; root/disconnect runtime remains Phases 5/8 |
| FR-CAT-007 | Canonical work/edition tables, catalogue items, occurrence links and aliases preserve distinct records | No merge during migration; conflicting ISBN/DOI values become preflight conflicts | Phase04 graph count and conflict-path tests | COMPLETE for schema/backfill; reversible merge/split remains Phase 9 |
| NFR-PROD-007 | Transactional schema plus per-batch backfill and alias checkpoints | Re-entry skips committed aliases; migration failure restores verified backup | MigrationTests and Phase04 restart test | COMPLETE for Phase 4 scope |
| NFR-PROD-012 | Local backup, integrity verification, forward-compatible aliases and migration report | Source PDFs are never touched; failed migration restores catalogue backup | Migration backup/repair tests | COMPLETE for Phase 4 scope |
| Search/AI compatibility | Legacy IDs remain available via `LegacyIdentityAliases`; existing vectors are retained and BookRow status is stale-marked | Reindex can rebuild without losing embeddings first | Phase04 `RequiresSemanticReindex` and alias projection test | COMPLETE for migration handoff; reindex implementation remains later phases |

Trace chain: requirement -> migration/backfill workflow -> canonical persistence
and alias -> failure/recovery state -> executable test -> downstream owner.
