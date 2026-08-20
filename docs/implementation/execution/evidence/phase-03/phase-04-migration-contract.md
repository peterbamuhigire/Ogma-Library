# Phase 4 canonical identity migration contract

Phase 3 changes no database schema. This contract fixes what Phase 4 must
implement and test before the persistence freeze.

## Canonical persistence targets

| Relation | Required identity/data | Required constraint/index |
| --- | --- | --- |
| LibraryRoots | opaque root ID and non-secret display/health state | primary key; root authority uniqueness defined in Phase 5 |
| FileOccurrences | occurrence ID, root ID, normalized root-relative locator, nullable asset ID, availability | unique root + normalized locator; root/asset FKs; locator is never a global identity |
| ContentAssets | asset ID, complete SHA-256, fingerprint version, byte size | unique hash + fingerprint version; valid SHA-256 and positive version checks |
| Works | work ID and explicit resolution state | primary key; valid-state check |
| Editions | edition ID, work ID and explicit resolution state | work FK; valid-state check and work lookup index |
| EditionContentAssets | edition ID and asset ID | composite uniqueness and both FKs |
| CatalogueItems | catalogue item ID, work ID, edition ID, nullable preferred occurrence | edition/work consistency; indexed consumer lookups |
| BibliographicIdentifiers | owner scope/ID, source, kind, normalized value | scoped source + kind + value uniqueness; valid kind/scope checks |
| IdentityDecisions | decision ID, subject/candidate occurrence IDs, relationship, disposition, tier, confidence, policy version | distinct occurrence check; valid enums/range/version; pair lookup index |
| LegacyIdentityAliases | legacy BookId to catalogue/work/edition mapping | unique legacy ID; canonical FKs |

Canonical locally generated keys use 26-character Crockford ULIDs stored as
SQLite `TEXT`. Paths and hashes are never primary keys. Phase 4 may introduce
temporary migration-only tables, but they must not become a second identity
authority.

## Legacy mapping

1. Back up and integrity-check the SQLite catalogue before mutation.
2. Create one provisional work, edition and catalogue item for every legacy
   `BookRow`; preserve title, authors, shelves, tags, reading state, annotations,
   metadata provenance and derived-index ownership through aliases/FKs.
3. Create one file occurrence for every `BookFileRow`. If a legacy row has only
   `BookRow.RelativePath`, create a compatibility occurrence without claiming it
   is another physical file.
4. Create/group content assets only for valid persisted SHA-256 values. Invalid,
   blank or absent values remain unknown. Never hash a path.
5. Preserve unavailable/excluded status as state. Do not delete catalogue data.
6. Do not merge provisional works or editions during migration. Suspected
   relationships become later review candidates.
7. Populate legacy aliases before switching readers. Compare row counts and
   curated-state checksums before committing.
8. Use transactional migration where SQLite permits, forward repair for failures
   after irreversible DDL, and retain a verified pre-migration backup.

## Phase 4 acceptance tests

- Clean install and legacy fixture migration.
- Invalid/default identities rejected by DB constraints.
- Valid hashes grouped; absent/invalid hashes remain null and never become fake.
- Multiple file rows and unavailable files preserve all curated data.
- Work/edition/catalogue aliases resolve every legacy `BookId`.
- Interrupted migration recovers from backup or resumes forward safely.
- Up/down where lossless and forward-recovery where a down migration would lose
  newly captured data.
- `PRAGMA foreign_key_check` and `PRAGMA integrity_check` succeed.
