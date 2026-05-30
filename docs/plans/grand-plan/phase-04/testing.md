# Phase 04 — Test Plan

This phase is entirely a data layer; it has no UI surface. The focus is on:
schema correctness, identity determinism, migration reliability, sidecar portability,
read-model performance, encryption correctness, and export/import round-trip integrity.
R1 (data loss) tests are the primary gate.

---

## Applicable test layers (from the 9-layer strategy)

| Layer | Applies | Notes |
| --- | --- | --- |
| 1. Domain unit | Yes | Value objects (`BookId`, `Isbn`, `RelativePath`, `Sha256Hash`); identity-chain logic. |
| 2. Infrastructure integration | Yes | EF Core schema; migration; `BookIdentityService`; `SidecarService`; `ExportBundleService`; encryption. |
| 3. PDF layer | Partial | Tier-4 (PDF fingerprint) and tier-5 (ISBN extraction via PdfPig) use golden-corpus PDFs. |
| 4. Search | No | FTS5/SearchChunks population deferred to Phase 10. |
| 5. AI | No | Not in scope. |
| 6. UI | No | No UI surface. |
| 7. 3D | No | Not in scope. |
| 8. Performance | Yes | `GetBookSummariesAsync` 2,000-book budget; `EXPLAIN QUERY PLAN` assertions. |
| 9. Packaging | No | Not in scope for this phase. |

---

## Golden corpus fixtures used

| Fixture | Used by | Oracle |
| --- | --- | --- |
| `corpus/simple-text.pdf` | Tier-2, tier-4, tier-5 identity tests | SHA-256 is stable; fingerprint matches; ISBN absent |
| `corpus/bad-metadata.pdf` | Tier-5 (ISBN absent / malformed) | Returns `NewBook` or `FuzzyMatch` depending on fingerprint |
| `corpus/isbn-in-docinfo.pdf` | Tier-5 (ISBN in DocInfo) | Resolves to existing book via ISBN; `ExactMatch` |
| `corpus/isbn-in-xmp.pdf` | Tier-5 (ISBN in XMP) | Same as above |
| `corpus/very-large-1000pp.pdf` | SHA-256 performance | Hash computed in < 2 s on reference hardware |
| `corpus/password-protected.pdf` | Tier-4/5 | Returns `Unresolvable("password-protected")` |
| `corpus/rotated-pages.pdf` | Tier-4 (fingerprint) | Fingerprint from content stream, not page geometry; `FuzzyMatch` or `ExactMatch` |
| Synthetic 500-book corpus (by seed) | Read-model performance | Query < 150 ms P95 |
| Synthetic 2,000-book corpus (by seed) | Read-model performance | Query < 200 ms P95 (NFR-OGMA-002) |

---

## Test categories and oracles

### 1. Schema correctness

| Test | Oracle | Tier |
| --- | --- | --- |
| `CatalogueSchema_HasAllRequiredTables` | All 20 `DbSet<T>` types produce non-null `IQueryable`; SQLite `sqlite_master` query confirms table names | MVP |
| `CatalogueSchema_ForeignKeys_AllValid` | Insert-and-query for every FK relation without constraint violation | MVP |
| `CatalogueSchema_UniqueConstraints_Enforced` | Duplicate insert on `Jobs.IdempotencyKey`, `ShelfBook.(ShelfId, BookId)` throws `DbUpdateException` | MVP |
| `WorksEditions_Schema_ForeignKeysValid` | `Work → Edition → Book` chain inserts and queries correctly | V1 |

### 2. Book-identity service

| Test | Oracle | Tier |
| --- | --- | --- |
| `BookIdentityService_Tier1_ExactPathMatch` | Stored `RelativePath` matches → `ExactMatch` | MVP |
| `BookIdentityService_ResolvesAfterRename` | SHA-256 unchanged after filename change → `ExactMatch` | MVP (R1) |
| `BookIdentityService_ResolvesAfterMove` | SHA-256 unchanged after directory move → `ExactMatch` | MVP (R1) |
| `BookIdentityService_FallsBackToFingerprint` | Hash differs but fingerprint matches → `FuzzyMatch` | MVP |
| `BookIdentityService_FallsBackToIsbn` | Hash and fingerprint differ but ISBN matches → `FuzzyMatch` | MVP |
| `BookIdentityService_ReturnsMtimeFastPath` | Size+mtime unchanged → SHA-256 not recomputed (spy/mock on hash function) | MVP |
| `BookIdentityService_ReturnsNewBook_OnNewFile` | No tier matches → `NewBook` | MVP |
| `BookIdentityService_ReturnsUnresolvable_OnPasswordPDF` | PdfPig throws on fingerprint extraction → `Unresolvable` | MVP |
| `BookIdentityService_GoldenCorpus_AllFilesResolvable` | All 11 non-password corpus PDFs → `ExactMatch` after first import | MVP |

### 3. Migration reliability

| Test | Oracle | Tier |
| --- | --- | --- |
| `Migration_BackupBeforeApply_RestoredOnFailure` (R1) | Injected migration exception → `.bak` file restored; DB unchanged; `AuditEvent` logged | MVP |
| `Migration_IsIdempotent` | `ApplyAsync()` twice → no exception, row counts unchanged | MVP |
| `Migration_DownMigration_DropsAllTables` | Revert initial migration → `sqlite_master` empty | MVP |
| `Migration_WalMode_Confirmed` | After startup, `PRAGMA journal_mode` returns `wal` | MVP |
| `Migration_ForeignKeys_Confirmed` | After startup, `PRAGMA foreign_keys` returns `1` | MVP |

### 4. Sidecar service

| Test | Oracle | Tier |
| --- | --- | --- |
| `SidecarService_ResolvesPath_MatchesConvention` | Cover path = `.ogma/covers/<prefix8>/<sha256>.jpg` (forward-slash on both OS) | MVP |
| `SidecarService_CreatesDirectories_OnFirstAccess` | Resolve to non-existent path → directory created | MVP |
| `SidecarService_PathsArePortable` | Relative paths stored with `/` are re-resolvable on both Windows and macOS | MVP |
| `SidecarService_AllSixClasses_Resolve` | `Cover`, `Thumbnail`, `Spine`, `Ocr`, `ExtractedText`, `Embeddings` all return distinct valid paths | MVP |

### 5. Read-model projections

| Test | Oracle | Tier |
| --- | --- | --- |
| `GetBookSummaries_2000Books_Under200ms` | P95 elapsed < 200 ms; measured with `Stopwatch`; run 10 iterations | MVP (NFR-OGMA-002) |
| `GetBookDetail_AllFieldGroupsPopulated` | All five field groups (File, Biblio, Reading, Enrichment, AI) non-null for a fully-seeded book | MVP (FR-CAT-004) |
| `GetShelves_IncludesSmartShelves` | Both `Virtual` and `Smart` shelf types returned | MVP (FR-CAT-003) |
| `CatalogueReadModel_DoesNotExposeIQueryable` (arch test) | `ICatalogueReadModel` method return types contain no `IQueryable<T>` | MVP |
| `BookSummaryProjection_IsSerializable` | `JsonSerializer.Serialize/Deserialize` round-trip is lossless | V1 (LAN projection) |

### 6. At-rest encryption

| Test | Oracle | Tier |
| --- | --- | --- |
| `CatalogueEncryption_ToggleOn_DatabaseFileBecomesBinary` | Raw first 16 bytes of `.db` file do not match SQLite magic string `53 51 4C 69 74 65 20 66 6F 72 6D 61 74 20 33 00` | V1 |
| `SidecarEncryption_ToggleOn_AssetUnreadableWithoutKey` | Cover JPEG file when read raw does not begin with `FF D8 FF` JPEG magic | V1 |
| `Encryption_Toggle_RoundTrip_NoDataLoss` (R1) | Enable → seed 50 rows → disable → assert all rows intact | V1 |
| `Encryption_Key_NeverWrittenToPlaintext` | No file in `.ogma/` contains the raw 32-byte key material (walk directory tree; assert no file > 0 bytes matches key bytes) | V1 |

### 7. Export bundle

| Test | Oracle | Tier |
| --- | --- | --- |
| `ExportBundle_RoundTrip_PreservesAllData` (R1) | Export 50-book corpus → delete DB + sidecar → import → assert `Books`, `Annotations`, `ReadingProgress`, `Bookmarks` row-for-row identical | V1 |
| `ExportBundle_ManifestHashMismatch_Rejects` | Corrupt 1 byte in a bundled file → `ImportAsync` throws `BundleIntegrityException` | V1 |
| `ExportBundle_VersionTagged` | Manifest `ogma-manifest.json` contains `schemaVersion` field matching the current migration version | V1 |

### 8. Architecture tests

| Test | Oracle |
| --- | --- |
| `Architecture_Domain_HasNoOutwardDependencies` | `OgmaLibrary.Domain` assembly does not reference `Infrastructure`, `Application`, `App`, or any NuGet package with "EF" in its name |
| `Architecture_CatalogueDbContext_UsedOnlyInInfrastructure` | No type in `Domain`, `Application`, `Reader`, `Bookshelf3D`, `Workers`, or `App` directly instantiates or injects `CatalogueDbContext` |
| `Architecture_NoRawAdoOutsideInfrastructure` | No `System.Data.Common.DbCommand` or `System.Data.SqlClient` usage outside `Infrastructure` assembly |

---

## Fault-injection strategy

For R1 tests (data loss), the following faults are injected:

| Fault | Injected in | Verification |
| --- | --- | --- |
| `IOException` during migration `MigrateAsync` | `MigrationService` via test-double `IDbMigrator` | `.bak` is restored; no partial migration in DB |
| `IOException` during encryption swap (rename) | `EncryptionService` test-double | Original unencrypted DB survives; no data loss |
| Corrupt zip entry (1-byte flip) | `ExportBundleService` test-double | `BundleIntegrityException` thrown; no partial restore |
| `OperationCanceledException` during export | `CancellationToken` cancel mid-zip | Partial export file deleted; no sidecar directory left in partial state |

---

## Performance baselines

| Metric | Budget | Corpus | Measurement method |
| --- | --- | --- | --- |
| `GetBookSummariesAsync` 2,000 books | < 200 ms P95 | Synthetic seed | `Stopwatch.Elapsed`; 10 iterations; assert P95 threshold |
| `GetBookSummariesAsync` 500 books | < 150 ms P95 | Synthetic seed | Same |
| SHA-256 hash of 1,000-page PDF | < 2 s | `very-large-1000pp.pdf` | `Stopwatch.Elapsed`; single run; assert < 2 s |
| Export 2,000-book DB + covers | < 30 s | Synthetic seed + cover stubs | `Stopwatch.Elapsed`; single run; trend data |

> If reference hardware is not confirmed by Phase 00, baselines are recorded as
> trend data in CI and converted to hard gates once hardware is fixed (Phase 20).

---

## CI matrix

Tests must pass on:

| Runner | .NET | Architecture |
| --- | --- | --- |
| Windows 10 x64 | .NET 10 LTS | x64 |
| macOS 12 x64 | .NET 10 LTS | x64 |
| macOS 14 Apple Silicon | .NET 10 LTS | ARM64 |

The Windows runner uses the Windows-style temp path (`C:\Temp\...`) in integration
tests; the macOS runner uses `/tmp/...`. `SidecarService` and `ExportBundleService`
tests must pass on both without conditional compilation.
