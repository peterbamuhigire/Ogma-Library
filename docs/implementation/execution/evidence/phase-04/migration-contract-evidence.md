# Phase 4 migration contract evidence

## Canonical schema

Migration `20260820101651_Phase04CanonicalIdentity` adds the canonical relations
without rewriting legacy tables that are still referenced by earlier search,
reader, classroom and annotation migrations:

- `LibraryRoots`
- `FileOccurrences`
- `ContentAssets`
- `CanonicalWorks`
- `CanonicalEditions`
- `CatalogueItems`
- `EditionContentAssets`
- `CatalogueItemOccurrences`
- `BibliographicIdentifiers`
- `IdentityDecisions`
- `LegacyIdentityAliases`

SQLite checks enforce 26-character generated IDs, hash syntax, fingerprint
version, allowed states, owner scope, confidence range, distinct decision
occurrences and canonical edition/work consistency. Unique indexes enforce
root-relative locator identity, hash plus fingerprint version and scoped
bibliographic values.

## Backfill behavior

`CanonicalIdentityMigrationService` runs after EF schema application and before
startup reports catalogue readiness. It is restartable in 500-book batches and
creates one provisional work, edition and catalogue item per legacy BookId. It
creates occurrences from BookFiles, falls back to the legacy BookRow locator,
and groups repeated normalized locators into one occurrence while preserving
every catalogue-item relationship.

Valid persisted SHA-256 values become `ContentAssets` with
`VerificationStatus=0` (legacy/unverified) and fingerprint version 1. Invalid,
missing or incomplete hashes remain null. No path is hashed or used as an asset
identity. Existing embedding rows remain in place while BookRow embedding status
is reset to stale for later rebuild.

ISBN/DOI values are copied only when valid and unambiguous. Conflicting legacy
values are counted in the preflight report and left for later review; migration
never silently merges works or editions.

## Recovery and observability

Before a file-backed migration, `CatalogueMigrator` creates a local timestamped
SQLite backup using the SQLite backup API, verifies `PRAGMA integrity_check`, and
restores the exact backup after closing connections if schema or backfill fails.
Progress exposes only stage and counts (`identity.backfill`, completed, total,
conflicts). No paths, titles, PDF text, prompts, credentials or provider payloads
are emitted.

## Acceptance evidence

- `Phase04CanonicalIdentityMigrationTests` covers curated-field preservation,
  unknown-hash behavior, aliases, stale embedding state, restartability,
  progress and invalid-row constraints.
- `MigrationTests` covers backup creation, idempotency, missing-table repair and
  migration downgrade/remigration behavior.
- `StartupShellRenderTests` covers the migration progress-compatible startup
  surface and recoverable failure shell.
