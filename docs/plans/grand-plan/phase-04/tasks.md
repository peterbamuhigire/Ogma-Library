# Phase 04 — Tasks

Task IDs: `P04-WPN-TN`. Each task lists its requirement/NFR/CTRL/ADR traceability,
estimate in hours (ideal engineering time), and dependencies within the phase.

---

## WP1 — Domain model & value objects

**Goal:** All domain primitives reside in `OgmaLibrary.Domain` with no outward
dependencies. Every public type has XML doc comments.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP1-T1 | Create `BookId` (strongly-typed GUID wrapper, `IEquatable<BookId>`, JSON-serializable). | HLD §3; ADR-0005 | 1 | — |
| P04-WP1-T2 | Create `Sha256Hash` value object: 32-byte storage, hex-string conversion, `==` semantics. | HLD §4 | 1 | — |
| P04-WP1-T3 | Create `Isbn` value object: stores ISBN-10 and ISBN-13; normalize to ISBN-13 on construction; validate check digit; expose `IsValid` property. | FR-META-001 | 2 | — |
| P04-WP1-T4 | Create `RelativePath` value object: always forward-slash separator; case-fold per OS at construction; `==` uses `OrdinalIgnoreCase` on Windows, `Ordinal` on macOS. | HLD §4; NFR-PROD-009 | 2 | — |
| P04-WP1-T5 | Create `SidecarPath` value object: relative-path + sidecar class enum (`Cover`, `Thumbnail`, `Spine`, `Ocr`, `ExtractedText`, `Embeddings`, `Backup`, `Export`). | SRS gap: sidecar naming | 1 | P04-WP1-T4 |
| P04-WP1-T6 | Define `BookStatus` enum (`Active`, `Unavailable`, `Excluded`) and `FileStatus` enum (`Present`, `Missing`, `Excluded`). | FR-LIB-004 | 1 | — |
| P04-WP1-T7 | Define `BookMatchResult` discriminated union: `ExactMatch`, `FuzzyMatch(float confidence)`, `NewBook`, `Unresolvable(string reason)`. | HLD §4 | 1 | P04-WP1-T1 |
| P04-WP1-T8 | Define `IBookIdentityService` interface in `OgmaLibrary.Application`: `ResolveAsync(AbsolutePath, LibraryRootPath, CancellationToken) → BookMatchResult`. | HLD §4 | 1 | P04-WP1-T7 |
| P04-WP1-T9 | Write `Architecture_Domain_HasNoOutwardDependencies` test (NetArchTest or custom reflection). | Phase 02 arch-test harness | 1 | P04-WP1-T1 |

---

## WP2 — EF Core entity model & configurations

**Goal:** Every table in HLD §3 has a corresponding C# entity and `IEntityTypeConfiguration<T>`
with correct constraints, indexes, and cascade rules.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP2-T1 | Create `Book` entity with all columns from the schema in §7; configure primary key, unique index on `(RelativePath, LibraryRootId)`. | HLD §3; ADR-0005 | 2 | WP1 |
| P04-WP2-T2 | Create `BookFile` entity; configure FK to `Book` with cascade delete; index on `(BookId, FileStatus)`. | HLD §3; FR-LIB-004 | 1 | P04-WP2-T1 |
| P04-WP2-T3 | Create `BookMetadataField` entity; configure index on `(BookId, FieldName, Source)`. | HLD §3; FR-META-004 | 1 | P04-WP2-T1 |
| P04-WP2-T4 | Create `Author` and `BookAuthor` (join with `Role`, `DisplayOrder`) entities. | HLD §3 | 1 | P04-WP2-T1 |
| P04-WP2-T5 | Create `Shelf` entity; `ShelfType` discriminator (`Virtual`, `Smart`); index on `Name`. | HLD §3; FR-CAT-003 | 1 | P04-WP2-T1 |
| P04-WP2-T6 | Create `ShelfBook` join entity with `AddedUtc` and `DisplayOrder`; unique constraint `(ShelfId, BookId)`. | HLD §3; FR-CAT-003 | 1 | P04-WP2-T5 |
| P04-WP2-T7 | Create `ReadingProgress` entity; primary key is `BookId` (one progress per book). | HLD §3; FR-READ-001 | 1 | P04-WP2-T1 |
| P04-WP2-T8 | Create `Bookmark` entity; index on `(BookId, Page)`. | HLD §3; FR-READ-007 | 1 | P04-WP2-T1 |
| P04-WP2-T9 | Create `Annotation` entity with `Type` discriminator; create `AnnotationBody` as dependent entity (1-to-1, owned). | HLD §3; FR-READ-008; ADR-0008 | 2 | P04-WP2-T1 |
| P04-WP2-T10 | Create `ExtractedPage` entity; index on `(BookId, PageNumber)`. | HLD §3; FR-SEARCH-002 | 1 | P04-WP2-T1 |
| P04-WP2-T11 | Create `SearchChunk` entity; index on `(BookId, ChunkIndex)`; FK to `ExtractedPage` nullable. | HLD §3; FR-SEARCH-002 | 1 | P04-WP2-T10 |
| P04-WP2-T12 | Create `EmbeddingVector` entity; FK to `SearchChunk`; index on `(ChunkId, ModelId)`; `VectorBlob` stored as `BLOB`. | HLD §3; FR-SEARCH-004 | 1 | P04-WP2-T11 |
| P04-WP2-T13 | Create `AiQueryHistory` entity; index on `(CreatedUtc DESC)`; `IsDeleted` soft-delete flag. | HLD §3; FR-AI-009; NFR-PROD-014 | 1 | P04-WP2-T1 |
| P04-WP2-T14 | Create `MetadataLookup` entity; index on `(BookId, Provider, Timestamp DESC)`. | HLD §3; FR-META-002 | 1 | P04-WP2-T1 |
| P04-WP2-T15 | Create `Job` entity; `UNIQUE` constraint on `IdempotencyKey`; index on `(Status, JobType)`. | HLD §3; NFR-OGMA-009 | 1 | P04-WP2-T1 |
| P04-WP2-T16 | Create `AuditEvent` entity; index on `(Timestamp DESC)`, `(EntityId, EntityType)`. | HLD §3; NFR-PROD-013; CTRL-OGMA-018 | 1 | P04-WP2-T1 |
| P04-WP2-T17 | Create `Work` and `Edition` entities; `Books.EditionId` nullable FK to `Editions`. | HLD §3; SRS gap: Work/Edition | 1 | P04-WP2-T1 |
| P04-WP2-T18 | Create `CatalogueDbContext` with all `DbSet<T>` properties; configure `IDesignTimeDbContextFactory` for EF tools. | ADR-0005 | 2 | P04-WP2-T1..T17 |
| P04-WP2-T19 | Write `CatalogueSchema_HasAllRequiredTables` integration test: build the DB in-memory, assert all `DbSet` types produce non-null `IQueryable`. | HLD §3 | 2 | P04-WP2-T18 |
| P04-WP2-T20 | Write `Architecture_CatalogueDbContext_UsedOnlyInInfrastructure` test. | Phase 02 arch-test harness | 1 | P04-WP2-T18 |

---

## WP3 — Initial migration & startup sequence

**Goal:** The database is created or migrated automatically on startup, with a backup
taken before any migration runs. Failures restore the backup.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP3-T1 | Generate the initial EF Core migration (`InitialCreate`) using `dotnet ef migrations add`; verify down migration drops all tables. | ADR-0005; NFR-PROD-012 | 2 | WP2 |
| P04-WP3-T2 | Implement `MigrationService.ApplyAsync()`: (1) compute destination `.bak` path with timestamp, (2) `File.Copy(db, bak)`, (3) `context.Database.MigrateAsync()`, (4) on exception restore bak + log `AuditEvent`. | NFR-PROD-010; NFR-PROD-012 | 3 | P04-WP3-T1 |
| P04-WP3-T3 | Verify `PRAGMA journal_mode=WAL` and `PRAGMA foreign_keys=ON` are set in `OnConfiguring` / connection string. | HLD §3; NFR-PROD-006 | 1 | P04-WP2-T18 |
| P04-WP3-T4 | Write `Migration_BackupBeforeApply_RestoredOnFailure` fault-injection test: corrupt a migration class so it throws; assert the `.bak` is restored and the original DB is unmodified. | NFR-PROD-010 (R1) | 3 | P04-WP3-T2 |
| P04-WP3-T5 | Write `Migration_IsIdempotent` test: run `ApplyAsync` twice in sequence; assert no error and DB rows intact. | NFR-PROD-012 | 1 | P04-WP3-T2 |
| P04-WP3-T6 | Write `Migration_DownMigration_DropsAllTables` test: apply then revert; assert schema is empty. | NFR-PROD-012 | 1 | P04-WP3-T1 |

---

## WP4 — Book-identity service

**Goal:** `BookIdentityService` resolves the five-tier identity chain correctly and
deterministically for all golden-corpus file patterns.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP4-T1 | Implement tier-1 (relative path) lookup: query `BookFiles` by normalized relative path; return `ExactMatch` if found and `FileStatus=Present`. | HLD §4 | 2 | WP3 |
| P04-WP4-T2 | Implement tier-2 (SHA-256): compute hash of file bytes asynchronously; query `Books.Sha256Hash`; return `ExactMatch` or `FuzzyMatch`. | HLD §4 | 2 | P04-WP4-T1 |
| P04-WP4-T3 | Implement tier-3 (size+mtime) fast-path: if stored size and mtime match, skip SHA-256 recompute; return cached hash result. | HLD §4; NFR-OGMA-009 | 2 | P04-WP4-T2 |
| P04-WP4-T4 | Implement tier-4 (PDF fingerprint): extract first 1,024 bytes of the first content stream via `PdfPig`; query `Books.PdfFingerprint`. | HLD §4 | 3 | P04-WP4-T3 |
| P04-WP4-T5 | Implement tier-5 (ISBN/DOI): extract ISBN from `PdfPig` metadata; normalize via `Isbn` value object; query `Books.IsbnNormalized`. | HLD §4; FR-META-001 | 2 | P04-WP4-T4 |
| P04-WP4-T6 | Unit test: `BookIdentityService_ResolvesAfterRename` — create book with path A, rename file to B, assert tier-2 returns `ExactMatch`. | HLD §4 (R1) | 2 | P04-WP4-T2 |
| P04-WP4-T7 | Unit test: `BookIdentityService_ResolvesAfterMove` — move file to sub-folder, assert tier-2 `ExactMatch`. | HLD §4 (R1) | 1 | P04-WP4-T6 |
| P04-WP4-T8 | Unit test: `BookIdentityService_FallsBackToFingerprint` — change hash by altering 1 byte outside content stream; assert tier-4 `FuzzyMatch`. | HLD §4 | 2 | P04-WP4-T4 |
| P04-WP4-T9 | Unit test: `BookIdentityService_ReturnsMtimeFastPath` — assert SHA-256 not recomputed when size+mtime unchanged (mock file system). | NFR-OGMA-009 | 1 | P04-WP4-T3 |
| P04-WP4-T10 | Golden-corpus test: run `ResolveAsync` over all 11 corpus PDFs; assert `ExactMatch` for all; no `Unresolvable`. | Test Strategy: golden corpus | 2 | P04-WP4-T5 |

---

## WP5 — Sidecar service

**Goal:** `SidecarService` resolves canonical paths for all six sidecar classes,
creates directories on demand, and is portable across OS path separators.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP5-T1 | Implement `SidecarService.Resolve(BookId, SidecarClass, variant?) → AbsolutePath`: apply the sharding and naming convention from §7; `Directory.CreateDirectory` on first access. | SRS gap: sidecar naming; NFR-PROD-009 | 3 | WP1 |
| P04-WP5-T2 | Implement `SidecarService.ResolveRelative(BookId, SidecarClass) → RelativePath` for DB storage (always forward-slash). | NFR-PROD-009 | 1 | P04-WP5-T1 |
| P04-WP5-T3 | Unit test: `SidecarService_ResolvesPath_MatchesConvention` — assert cover path equals `.ogma/covers/<prefix8>/<sha256>.jpg` on both Windows and macOS (run with both path separators). | SRS gap | 2 | P04-WP5-T1 |
| P04-WP5-T4 | Unit test: `SidecarService_CreatesDirectories_OnFirstAccess` — resolve path to a non-existent directory; assert created. | SRS gap | 1 | P04-WP5-T1 |
| P04-WP5-T5 | Integration test: `SidecarService_PathsArePortable` — create paths on Windows-separator temp dir, export relative paths, re-resolve on macOS-separator; assert same file. | NFR-PROD-009 | 2 | P04-WP5-T2 |

---

## WP6 — Read-model projections

**Goal:** Clean, EF-Core-free projection records exposed through `ICatalogueReadModel`
so Reader, 3D, and (future) LAN clients never touch the entity graph.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP6-T1 | Define `BookSummaryProjection` record: `BookId`, `Title`, `Authors`, `CoverRelativePath`, `Status`, `Rating`, `ShelfIds`, `ReadingProgressPct`, `IsAvailable`. | LAN-CLASSROOM-ARCHITECTURE.md §3; FR-CAT-001 | 2 | WP2 |
| P04-WP6-T2 | Define `BookDetailProjection` record: all five metadata field groups (File, Bibliographic, Reading, Enrichment, AI — FR-CAT-004). | FR-CAT-004 | 2 | P04-WP6-T1 |
| P04-WP6-T3 | Define `ShelfProjection`, `ReadingProgressProjection`, `BookmarkProjection` records. | FR-CAT-003; FR-READ-001 | 1 | P04-WP6-T1 |
| P04-WP6-T4 | Implement `CatalogueReadModel : ICatalogueReadModel` with EF Core LINQ projections (`.Select(b => new BookSummaryProjection(…))`); no lazy loading. | NFR-OGMA-002 | 4 | P04-WP6-T1..T3 |
| P04-WP6-T5 | Integration test: `GetBookSummaries_2000Books_Under200ms` — seed synthetic 2,000-book corpus; assert P95 query time < 200 ms. | NFR-OGMA-002; NFR-PROD-003 | 3 | P04-WP6-T4 |
| P04-WP6-T6 | Integration test: `GetBookDetail_AllFieldGroupsPopulated` — seed one book with full metadata; assert all five field groups non-null. | FR-CAT-004 | 1 | P04-WP6-T4 |

---

## WP7 — At-rest encryption

**Goal:** The catalogue and sidecar can be encrypted at rest using a key from the OS
credential store; toggling encryption is reversible and transactional.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP7-T1 | Implement `IEncryptionKeyProvider` backed by DPAPI (Windows) and Keychain (macOS); derive AES-256-GCM key from OS secret. | CTRL-OGMA-014; CTRL-OGMA-001 | 3 | Phase 01 spike result |
| P04-WP7-T2 | Implement chosen encryption approach (VFS or column-level, per ADR amendment); wire into `CatalogueDbContext` via connection string or interceptor. | CTRL-OGMA-014 | 4 | P04-WP7-T1; ADR amendment |
| P04-WP7-T3 | Implement `EncryptionService.EnableAsync()` and `DisableAsync()`: (1) backup DB, (2) re-encrypt/decrypt, (3) verify integrity, (4) swap file atomically. | CTRL-OGMA-014; NFR-PROD-010 | 4 | P04-WP7-T2 |
| P04-WP7-T4 | Implement sidecar asset encryption: files in `.ogma/` (except `db/`) wrapped with AES-256-GCM envelope on write, decrypted on read via `SidecarService`. | CTRL-OGMA-015 | 3 | P04-WP7-T1 |
| P04-WP7-T5 | Integration test: `CatalogueEncryption_ToggleOn_DatabaseFileBecomesBinary` — enable encryption; open raw file bytes; assert not valid UTF-8 SQLite header. | CTRL-OGMA-014 | 2 | P04-WP7-T3 |
| P04-WP7-T6 | Integration test: `Encryption_Toggle_RoundTrip_NoDataLoss` — enable, seed data, disable; assert all rows intact. | CTRL-OGMA-014; NFR-PROD-010 (R1) | 2 | P04-WP7-T3 |

---

## WP8 — Export bundle

**Goal:** A portable `.zip` export bundle can be produced from any library and
re-imported without data loss, satisfying NFR-PROD-009.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP8-T1 | Implement `ExportBundleService.ExportAsync(options, progress, ct)`: zip `catalogue.db` + selected sidecar subtrees; write `ogma-manifest.json` (version, timestamp, SHA-256 of each file). | NFR-PROD-009 | 4 | WP3; WP5 |
| P04-WP8-T2 | Implement `ExportBundleService.ImportAsync(zipPath, targetRoot, conflictPolicy, ct)`: validate manifest SHA-256 hashes; restore DB to target; restore sidecar files; run migration on restored DB. | NFR-PROD-009 | 4 | P04-WP8-T1 |
| P04-WP8-T3 | Integration test: `ExportBundle_RoundTrip_PreservesAllData` — seed 50-book corpus; export; delete; import; assert all `Books`, `Annotations`, `ReadingProgress` rows identical. | NFR-PROD-009 (R1) | 3 | P04-WP8-T2 |
| P04-WP8-T4 | Integration test: `ExportBundle_ManifestHashMismatch_Rejects` — corrupt one file in the zip; assert `ImportAsync` throws `BundleIntegrityException`. | NFR-PROD-009 | 1 | P04-WP8-T2 |

---

## WP9 — Work/Edition schema

**Goal:** The optional Work/Edition layer is present in the schema with correct
foreign-key relations; no UI or use-case logic is built here.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP9-T1 | Implement `Work` and `Edition` entities with configurations; add migration `AddWorkEditionLayer`. | SRS gap: Work/Edition | 2 | WP3; Phase 00 owner decision |
| P04-WP9-T2 | Add `Books.EditionId` nullable FK column; add corresponding migration. | SRS gap: Work/Edition | 1 | P04-WP9-T1 |
| P04-WP9-T3 | Integration test: `WorksEditions_Schema_ForeignKeysValid` — insert `Work`, `Edition`, `Book` linked to edition; assert query succeeds; delete `Edition` cascades or nullifies `Book.EditionId` per chosen rule. | SRS gap | 1 | P04-WP9-T2 |

---

## WP10 — Architecture & integration tests (consolidation)

**Goal:** Close all required architecture tests, golden-corpus validation, and the
performance baseline for the read model.

| ID | Task | Req / NFR / ADR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P04-WP10-T1 | Finalize architecture tests: domain isolation, DbContext isolation, no raw ADO.NET outside Infrastructure. | Phase 02 arch-test harness | 2 | WP2; WP4 |
| P04-WP10-T2 | Finalize golden-corpus seed script: insert synthetic 500- and 2,000-book corpora with `BookId`, `Sha256Hash`, and realistic metadata diversity. | Test Strategy: perf corpora | 3 | WP2; WP6 |
| P04-WP10-T3 | Run `EXPLAIN QUERY PLAN` on `GetBookSummariesAsync` and `GetBookDetailAsync`; assert no full-table scans; document findings. | NFR-OGMA-002 | 2 | P04-WP6-T4 |
| P04-WP10-T4 | Verify all public types have XML doc comments (build with `<GenerateDocumentationFile>true</GenerateDocumentationFile>`; no warnings). | SOURCE-SUMMARY.md §L7 | 1 | All WPs |
| P04-WP10-T5 | Author `docs/architecture/catalogue-data-model.md`: table diagram (Mermaid ERD), identity-chain decision tree, sidecar layout reference. | Documentation | 3 | All WPs |
| P04-WP10-T6 | Ratify ADR-0005 (SQLite + sidecar) and record any encryption-approach amendment ADR in `docs/adr/`. | ADR-0005 | 1 | WP7 |
