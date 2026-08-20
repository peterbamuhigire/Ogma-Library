# ADR 0016: Canonical library identity

## Status

Accepted as the Phase 3 domain freeze candidate on 2026-08-20. Persistence
ratification is the Phase 4 freeze point.

## Context

The legacy catalogue overloaded `BookRow` with file hash, bibliographic and
presentation concerns. `BookRepository` then manufactured a path-derived
64-character value when a hash was unavailable. That value looked like a strong
content identity but was not evidence about file bytes. The model could not
safely distinguish a rename, an exact copy, another rendering of one edition, a
different edition of one work, or a merely similar title.

The SRS requires rename/move continuity (FR-LIB-003), preservation of curated
state while files are unavailable (FR-LIB-004), and reversible work/edition
grouping (FR-CAT-007). Search, advisor and 3D clients also need one stable
catalogue identity that does not expose a filesystem path.

## Options considered

1. Keep one `Book` aggregate and add nullable columns. Rejected because the same
   identity would continue to mean physical bytes, publication and work.
2. Model file occurrence, exact content asset, edition and work separately.
   Accepted because each relationship has different evidence and lifecycle.
3. Treat content hash as the catalogue identity. Rejected because distinct byte
   streams can represent one edition and one work can have several editions.

## Decision

Ogma uses the following identity chain:

`LibraryRoot -> FileOccurrence -> ContentAsset -> Edition -> Work`

A `CataloguePresentationIdentity` selects a stable catalogue item, work,
edition and optional preferred occurrence for grid, list, search, advisor and 3D
clients. It contains no path. Relative locators remain infrastructure-owned.

The terms and invariants are:

- A library root is an approved filesystem authority boundary.
- A file occurrence is one observed locator in one root. It may be unavailable
  and may have an unknown content asset.
- A content asset is one exact byte sequence and cannot exist without a genuine
  validated complete-file SHA-256 and fingerprint contract version.
- An edition is a particular publication. Several different content assets may
  represent it.
- A work is the intellectual work and owns one or more editions.
- ISBN and provider edition IDs are edition-scoped. Provider work IDs are
  work-scoped. All provider IDs retain source and kind.
- Unknown, provisional, identified and ambiguous bibliographic states are
  explicit.
- Only equal verified complete-file hashes may create an automatic exact-copy
  relationship. Edition IDs, work IDs and title/author similarity produce a
  reviewable proposal. Similar titles never cause an automatic merge.
- Identity decisions are path-free, versioned and record relationship, evidence
  tier, disposition and confidence.
- Unavailability is observation state, not deletion intent.

New canonical IDs are opaque non-empty strings. Phase 4 generation uses
Crockford ULID text; compatibility aliases preserve legacy `BookId` references
during migration rather than reusing paths or hashes as primary keys.

## Consequences

- Phase 4 must add normalized identity tables, constraints, indexes, aliases and
  a data-preserving migration before canonical repositories replace the legacy
  adapter.
- Existing `Book`, `BookFile` and `IBookRepository` domain names have been
  removed. The temporary shapes are explicitly named `LegacyCatalogueRecord`,
  `LegacyFileRecord` and `ILegacyCatalogueRepository`.
- The compatibility adapter maps only verified persisted hashes, sizes and
  timestamps. Missing or invalid facts remain null.
- Phases 5-9 implement root authorization, scanning, reconciliation and
  reversible bibliographic decisions against this model.
- Phases 19, 22, 28 and 31 adopt `CataloguePresentationIdentity`; none may use a
  path as catalogue identity.

## Verification

- `CanonicalIdentityModelTests` exercises strong-ID, exact-copy, edition, work,
  similarity, ambiguity and provider-namespace invariants.
- `Architecture_CanonicalIdentityModel_IsPathIndependentAndExplicit` prevents
  paths entering canonical identity contracts.
- `Architecture_LegacyCatalogueAdapter_DoesNotFabricateHashes` prohibits the
  removed placeholder-hash implementation.
