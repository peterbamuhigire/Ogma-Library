# Phase 13 Stale Provider Result Evidence

Date: 2026-09-05

## Delivered path

The gateway's transient `ProviderMetadataResult.IsStale` state now survives the
provider aggregation boundary as `MetadataLookupRow.IsStale`, with a default of
`false` for existing rows. The new migration is
`Phase13ProviderLookupStaleness`. The catalogue read model projects a bounded
recent set of provider lookups into `BookDetailProjection.ProviderLookups`, and
the book-detail enrichment tab renders localized freshness text including
`stale cached result` when appropriate.

The SQLite read model uses the increasing lookup identity to bound the SQL
slice, then sorts at most eight projected rows by timestamp in memory because
SQLite does not translate `DateTimeOffset` ordering. This keeps the query
portable and bounded.

## Verification

- Migration, schema, read-model, and detail stale-state focused tests: 3 passed,
  0 failed, 0 skipped.
- Migration rollback suite including earlier Phase 12/18 migration paths: 12
  passed, 0 failed, 0 skipped.
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- Full Release solution regression after compiling the migration: 1,102 passed
  (906 core, 41 architecture, 155 UI), 0 failed, 0 skipped.

## Gate disposition

Closed locally: stale state is persisted, projected, bounded, localized, and
verified through the detail consumer.

Still open: written legal/privacy owner review, archived provider terms,
live provider/network evidence, and physical release/UI evidence.
