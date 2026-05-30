# Phase 04 — Catalogue & Data Layer

The authoritative SQLite catalogue-of-record that every other bounded context reads
through stable contracts — the single source of truth for book identity, metadata,
reading state, and all durable user data.

---

## 1. Title & one-line mission

**Phase 04 — Catalogue & Data Layer.**
Establish the SQLite/EF Core catalogue as the irreplaceable, tamper-evident, fully
portable foundation that all other Ogma Library contexts depend on, with a complete
table model, a rigorous book-identity strategy, a resolved sidecar asset layout,
idempotent reversible migrations, optional at-rest encryption, and a portable export
bundle — so that no other phase needs to make data-model decisions, and no book's
data can be lost.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Tier** | MVP (core tables, identity, migrations, sidecar); V1 (at-rest encryption, export bundle, Work/Edition layer) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | Original Phase 0 (data model) + Phase 1 (catalogue foundation) |
| **Platforms** | Windows 10+ (x64/ARM64) and macOS 12+ (x64/Apple Silicon) — both must be green |
| **Status** | Planned — not yet started |

---

## 3. Objectives

When this phase is done, all of the following are true:

1. The SQLite database contains every table specified in HLD §3/§4 with correct
   foreign-key constraints, indexes, and column types; EF Core migrations are
   idempotent, reversible, and run automatically at startup after backing up the
   prior database.
2. A `BookIdentityService` resolves identity through the five-tier chain
   (rel-path → SHA-256 → size+mtime → PDF fingerprint → ISBN/DOI) and
   deterministically re-matches a book after rename, move, or re-import.
3. The sidecar asset folder layout and per-class naming convention are fully
   specified, implemented, and tested — no other phase needs to invent file paths.
4. All data access in the `Catalogue` bounded context flows through typed EF Core
   `DbContext` interfaces; no raw ADO.NET SQL outside the data layer.
5. Optional at-rest encryption of the catalogue and sidecar is wired (CTRL-OGMA-014,
   CTRL-OGMA-015) so the feature can be toggled without a schema change.
6. A portable export bundle (NFR-PROD-009) containing the catalogue and sidecar
   assets can be produced and re-imported without data loss.
7. Clean read-model projection interfaces are defined so Phases 16-18 (LAN/classroom)
   can consume catalogue data without exposing the EF Core entity graph.

---

## 4. Scope

### In scope

- Full EF Core entity/`DbContext` model for all 15 tables:
  `Books`, `BookFiles`, `BookMetadataFields`, `Authors`, `BookAuthors`,
  `Shelves`, `ShelfBooks`, `ReadingProgress`, `Bookmarks`,
  `Annotations`, `AnnotationBodies`, `ExtractedPages`, `SearchChunks`,
  `EmbeddingVectors`, `AiQueryHistory`, `MetadataLookups`, `Jobs`, `AuditEvents`.
- `IBookIdentityService` interface and `BookIdentityService` implementation
  covering all five identity tiers (rel-path, SHA-256, size+mtime, PDF fingerprint,
  ISBN/DOI).
- Sidecar asset folder: canonical path layout, per-class naming convention, and
  `ISidecarService` for resolving/writing sidecar paths.
- EF Core migrations: idempotent, reversible (down migrations for every up),
  backup-before-apply (copy `.db` before running any migration), version table.
- Optional at-rest encryption via `System.Security.Cryptography` (AES-256-GCM)
  integrated as a SQLite VFS extension or EF Core interceptor (spike outcome from
  Phase 01 drives the approach); CTRL-OGMA-014 / CTRL-OGMA-015.
- Export bundle: zip of `.db` file + sidecar subtree, version-tagged, with
  SHA-256 manifest; import restores to a new sidecar root.
- Work/Edition optional layer: `Works` and `Editions` tables (schema only, no
  UI — deferred merge/split UI to Phase 06/07); foreign key from `Books` to
  `Editions` nullable.
- Domain entities in `OgmaLibrary.Domain` project: value objects
  (`BookId`, `Sha256Hash`, `Isbn`, `RelativePath`, `SidecarPath`).
- `ICatalogueReadModel` and typed projection records for LAN-ready consumption
  (see LAN-CLASSROOM-ARCHITECTURE.md §2-3).
- Architecture tests asserting the `Domain` project has no outward dependencies,
  and that the `Infrastructure.Catalogue` project is the only place that
  instantiates `CatalogueDbContext`.
- Full unit + integration test coverage including golden-corpus fixtures and
  fault-injection for the backup-before-apply and export/import paths.

### Explicitly out of scope

- Any UI (no views, view-models, or icons beyond the stub `icons.md`).
- Ingestion pipeline logic (Phase 05): this phase defines the schema; Phase 05
  populates it.
- FTS5 / SearchChunks population (Phase 10), embedding generation (Phase 11).
- LAN host/client runtime (Phases 16-18): contracts are defined here; the server
  stack is built there.
- PDF write-back (Phase 07), OCR pipeline (Phase 15).
- Actual encryption key-management UI (Phase 19); this phase wires the crypto
  primitive and the settings flag only.

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| HLD §3 (Data) | MVP | SQLite catalogue tables as specified | `CatalogueSchema_HasAllRequiredTables` integration test |
| HLD §4 (Identity) | MVP | Five-tier identity chain | `BookIdentityService_ResolvesAfterRename`, `…AfterMove`, `…FallsBackToHash` unit tests |
| ADR-0005 | MVP | SQLite catalogue + sidecar as architectural choice | Ratified; tested end-to-end in migration + export tests |
| NFR-PROD-009 | V1 | Portability / no lock-in; export bundle | `ExportBundle_RoundTrip_PreservesAllData` integration test |
| NFR-PROD-010 | MVP | Reversible transactional destructive ops | `Migration_BackupBeforeApply_RestoredOnFailure` fault-injection test |
| NFR-PROD-012 | MVP | Signed builds + reversible migrations | `Migration_DownMigration_IsIdempotent` test; CI enforces signed build |
| CTRL-OGMA-014 | V1 | At-rest encryption of catalogue | `CatalogueEncryption_ToggleOn_DatabaseFileBecomesBinary` integration test |
| CTRL-OGMA-015 | V1 | At-rest encryption of sidecar | `SidecarEncryption_ToggleOn_AssetUnreadableWithoutKey` integration test |
| NFR-OGMA-009 | MVP | Background job recoverable without duplicate work | `Jobs_table_IdempotencyKey_PreventsDuplicate` unit test |
| SRS gap: sidecar naming | MVP | Resolved naming convention (see §7) | `SidecarService_ResolvesPath_MatchesConvention` unit test |
| SRS gap: Work/Edition | V1 | Work/Edition cardinality defined (schema) | `WorksEditions_Schema_ForeignKeysValid` migration test |

---

## 6. Dependencies

### Depends on

| Phase / Decision | What is needed |
| --- | --- |
| Phase 00 — Decision Closure | Reference hardware confirmed; Linux scope confirmed; Work/Edition cardinality decision (SRS context gap); sidecar naming convention decision; jurisdiction list (Data Protection Acts). |
| Phase 01 — Risk Spikes | At-rest encryption approach (SQLite VFS vs. EF interceptor) validated by spike, outcome recorded in ADR amendment; FTS5 strategy confirmed. |
| Phase 02 — Scaffolding | 9-project solution structure; `Directory.Build.props`; DI composition root; architecture-test harness; golden-corpus harness in place. |
| Phase 03 — Design System | (Minimal dependency) `ILocalizationService` interface available for any error-message keys introduced here. |

### Unblocks

- **Phase 05** (Ingestion Pipeline) — needs the schema and `IBookIdentityService`.
- **Phase 06** (Catalogue Browsing) — needs `ICatalogueReadModel` projections.
- **Phase 07** (Metadata Enrichment) — needs `MetadataLookups`, `AuditEvents`, provenance fields.
- **Phase 08** (Reader) — needs `ReadingProgress`, `Bookmarks`, `Annotations`.
- **Phase 09** (Annotations) — needs `Annotations`, `AnnotationBodies`.
- **Phase 10** (Search) — needs `ExtractedPages`, `SearchChunks`.
- **Phase 11** (Embeddings) — needs `EmbeddingVectors`.
- **Phase 12** (AI Gateway) — needs `AiQueryHistory`, `AuditEvents`.
- **Phases 16-18** (LAN) — needs `ICatalogueReadModel` projections.

---

## 7. Architecture & approach

### Bounded contexts touched

**Library Catalogue** (owns this phase entirely). Reader, Search, AI Advisor, and
Bookshelf Presentation contexts are *consumers* of read-model projections; they never
touch `CatalogueDbContext` directly (enforced by architecture tests).

### Project assignments

| Artifact | Project |
| --- | --- |
| Domain entities, value objects, `IBookIdentityService`, `ISidecarService` | `OgmaLibrary.Domain` |
| `ICatalogueReadModel`, projection records, `IJobRepository`, `IAuditService` interfaces | `OgmaLibrary.Application` |
| `CatalogueDbContext`, EF Core entity configurations, migrations, `BookIdentityService`, `SidecarService`, `ExportBundleService`, encryption interceptor | `OgmaLibrary.Infrastructure` |
| Architecture tests, integration tests | `OgmaLibrary.Tests` |

### Table model (HLD §3/§4 — canonical)

```
Books               (BookId PK, RelativePath, Sha256Hash, SizeBytes, MtimeTicks,
                     PdfFingerprint, IsbnNormalized, Doi, Status, EditionId FK?)
BookFiles           (BookFileId PK, BookId FK, AbsolutePath, FileStatus, LastSeenUtc)
BookMetadataFields  (FieldId PK, BookId FK, FieldName, Value, Source, SourceTimestamp,
                     Confidence, IsOverridden)
Authors             (AuthorId PK, NormalizedName, SortName)
BookAuthors         (BookId FK, AuthorId FK, Role, DisplayOrder)
Shelves             (ShelfId PK, Name, ShelfType [Virtual|Smart], Query, DisplayOrder,
                     CreatedUtc)
ShelfBooks          (ShelfId FK, BookId FK, AddedUtc, DisplayOrder)
ReadingProgress     (BookId FK PK, CurrentPage, ScrollOffsetPx, LastReadUtc,
                     TotalPagesRead, CompletionPct)
Bookmarks           (BookmarkId PK, BookId FK, Page, Label, CreatedUtc)
Annotations         (AnnotationId PK, BookId FK, Page, Type [Highlight|Note],
                     CreatedUtc, ModifiedUtc, ColorKey)
AnnotationBodies    (AnnotationId FK PK, QuoteText, NoteText, RectJson)
ExtractedPages      (ExtractedPageId PK, BookId FK, PageNumber, TextContent,
                     ExtractionMethod, ExtractionUtc)
SearchChunks        (ChunkId PK, BookId FK, ExtractedPageId FK?, ChunkIndex,
                     ChunkText, TokenCount)
EmbeddingVectors    (VectorId PK, ChunkId FK, ModelId, DimensionCount, VectorBlob,
                     CreatedUtc)
AiQueryHistory      (QueryId PK, QueryText, ProviderKey, ModelId, PrivacyTier,
                     RequestPayloadHash, ResponseSummary, TokensIn, TokensOut,
                     CostEstimate, CreatedUtc, IsDeleted)
MetadataLookups     (LookupId PK, BookId FK, Provider, RequestIsbn, ResponseJson,
                     Timestamp, Confidence, Applied)
Jobs                (JobId PK, JobType, IdempotencyKey UNIQUE, Status, BookId FK?,
                     Payload, StartedUtc, CompletedUtc, ErrorMessage, RetryCount)
AuditEvents         (EventId PK, EventType, EntityId, EntityType, ActorId,
                     BeforeJson, AfterJson, Timestamp, IsLocalOnly)
Works               (WorkId PK, CanonicalTitle, CanonicalAuthorId FK?)
Editions            (EditionId PK, WorkId FK, Language, PublicationYear, Publisher)
```

> Column detail is the binding schema; EF Core `IEntityTypeConfiguration<T>` classes
> enforce every constraint. Migrations are the source of truth for the deployed schema.

### Book-identity strategy (HLD §4 — five tiers, ordered)

1. **Relative path** — path relative to the library root; cheapest, first check.
2. **SHA-256 content hash** — computed once at import; re-computed on explicit
   re-check or when path check fails.
3. **Size + mtime** — fast heuristic to decide whether to recompute SHA-256.
4. **PDF fingerprint** — first 1,024 bytes of first content stream (PdfPig);
   catches files renamed *and* touched.
5. **ISBN/DOI** — last resort; allows re-match even after a full re-encode.

`BookIdentityService.ResolveAsync(filePath)` returns a `BookMatchResult` that is
either `ExactMatch`, `FuzzyMatch(confidence)`, `NewBook`, or `Unresolvable`
(unsupported file). The cascade is deterministic and unit-testable with no PDF
I/O for tiers 1-3.

### Sidecar asset folder layout (resolves SRS context gap)

Root: `<LibraryRoot>/.ogma/`

```
.ogma/
  db/
    catalogue.db          # main database
    catalogue.db.bak      # pre-migration backup (renamed with timestamp)
  covers/
    <sha256-prefix-8>/<sha256-full>.jpg   # 200x300 cover thumbnail
  thumbnails/
    <sha256-prefix-8>/<sha256-full>_p<NNN>.jpg  # per-page thumbnails
  spines/
    <sha256-prefix-8>/<sha256-full>_spine.jpg   # spine strip (7x100)
  ocr/
    <sha256-prefix-8>/<sha256-full>.hocr         # hOCR output (Phase 15)
  text/
    <sha256-prefix-8>/<sha256-full>.json.gz      # extracted text pages
  embeddings/
    <sha256-prefix-8>/<sha256-full>_<model>.bin  # raw vector blobs (if file-backed)
  backups/
    <timestamp>_pre_writeback_<sha256-8>.pdf     # pre-write-back PDF backups
  export/
    <timestamp>_ogma_export.zip                  # export bundles
```

- Using the SHA-256 prefix-8 sharding prevents large directories on any OS.
- All paths are relative to the library root in the database; absolute paths are
  never persisted (portability, NFR-PROD-009).
- `ISidecarService.Resolve(BookId, SidecarClass)` → `AbsolutePath`; creates
  directories on demand, never fails silently.

### EF Core / migrations

- `CatalogueDbContext : DbContext` with `IDesignTimeDbContextFactory` for tooling.
- Each entity has an `IEntityTypeConfiguration<T>` class — no data annotations on
  domain entities.
- Migrations live in `Infrastructure/Persistence/Migrations/`; each has a matching
  down migration.
- On startup: `MigrationService.ApplyAsync()` copies `catalogue.db` to
  `catalogue.db.<timestamp>.bak`, then calls `context.Database.MigrateAsync()`.
  If the migration throws, the backup is restored and the error is logged with an
  `AuditEvent` — NFR-PROD-010 / NFR-PROD-012.

### At-rest encryption (CTRL-OGMA-014/015)

- Spike (Phase 01) determines whether to use `SQLitePCLRaw` with an encrypted VFS
  (e.g. SQLCipher) or an EF Core `IDbCommandInterceptor` with column-level AES-256-GCM
  for sensitive tables. The chosen approach is recorded in an ADR amendment.
- The encryption key is derived from the OS credential store (DPAPI on Windows,
  Keychain on macOS) — CTRL-OGMA-001; never stored in plain text.
- A `EncryptionSettings` feature flag controls the feature; toggling off decrypts
  and rewrites, toggling on encrypts — both operations are transactional with backup.

### LAN read-model projections

`ICatalogueReadModel` exposes:

```csharp
IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(CatalogueFilter filter);
Task<BookDetailProjection>            GetBookDetailAsync(BookId id);
IAsyncEnumerable<ShelfProjection>     GetShelvesAsync();
Task<ReadingProgressProjection>       GetProgressAsync(BookId id);
```

`BookSummaryProjection` and `BookDetailProjection` are plain records with no EF
Core navigation properties — safe to serialize over the LAN wire (Phases 16-18)
without leaking the entity graph.

### Cross-platform notes

- SQLite via `Microsoft.Data.Sqlite`; the native SQLite3 binary is bundled for both
  `win-x64`, `win-arm64`, `osx-x64`, and `osx-arm64` via the
  `SQLitePCLRaw.bundle_e_sqlcipher` (or equivalent) NuGet.
- File path separators: the sidecar service always uses `Path.Combine` and stores
  relative paths with `/` as separator in the DB (portable).
- Case sensitivity: relative-path comparisons use `OrdinalIgnoreCase` on Windows,
  `Ordinal` on macOS (detected at runtime), stored lowercased for the hash/identity
  primary key to avoid duplicates across case-insensitive/sensitive mounts.

---

## 8. Work breakdown (summary)

Full detail in `tasks.md`.

| WP | Title | Key tasks |
| --- | --- | --- |
| WP1 | Domain model & value objects | `BookId`, `Sha256Hash`, `Isbn`, `RelativePath`, `SidecarPath`; identity enums; `BookMatchResult` |
| WP2 | EF Core entity model & configurations | All 20 tables; `IEntityTypeConfiguration<T>`; index strategy; cascade-delete rules |
| WP3 | Initial migration & startup sequence | `MigrationService` with backup-before-apply; version table; down migrations |
| WP4 | Book-identity service | Five-tier cascade; `BookIdentityService`; unit tests with synthetic file trees |
| WP5 | Sidecar service | `ISidecarService`; path resolution; directory creation; cross-platform path normalization |
| WP6 | Read-model projections | `ICatalogueReadModel`; `BookSummaryProjection`; `BookDetailProjection`; LINQ queries with EF Core |
| WP7 | At-rest encryption | Spike-driven implementation; key derivation; toggle transaction; CTRL-OGMA-014/015 |
| WP8 | Export bundle | `ExportBundleService`; zip with SHA-256 manifest; import with conflict resolution |
| WP9 | Work/Edition schema | `Works`/`Editions` tables; `Books.EditionId` nullable FK; no UI |
| WP10 | Architecture & integration tests | Schema tests; identity tests; migration fault injection; export round-trip |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons** — no new UI icons; `icons.md` is a stub (data-layer phase).
- [x] **i18n (en/fr)** — error and audit message keys defined in `en` + `fr`; no
      hard-coded user-facing strings in `MigrationService`, `ExportBundleService`,
      or identity-resolution error results.
- [x] **Accessibility** — no UI surface introduced; not applicable.
- [x] **Privacy/egress** — no off-device calls. `AiQueryHistory.IsDeleted` and
      `EmbeddingVectors` deletion path provided (NFR-PROD-014) for Phase 12 to wire.
- [x] **Reversibility (R1)** — backup-before-apply migration; export/import round-trip;
      all destructive operations (encryption toggle, export, migration) are transactional
      with restore paths; fault-injection tests required.
- [x] **Performance budgets** — `ICatalogueReadModel.GetBookSummariesAsync` query
      plan verified to return 2,000 rows in < 200 ms on reference hardware
      (NFR-OGMA-002 precondition); index coverage tested with `EXPLAIN QUERY PLAN`.
- [x] **Bounded-context tests** — `Domain_HasNoOutwardDependencies` and
      `CatalogueDbContext_UsedOnlyInInfrastructure` architecture tests.
- [x] **Documentation** — XML doc comments on all public types/members;
      `docs/architecture/catalogue-data-model.md` authored; ADR-0005 ratified.

---

## 10. Definition of Done

Global DoD (README §6) plus:

- [ ] All 20 tables exist in the deployed database with the correct columns, types,
      indexes, and foreign keys; verified by `CatalogueSchema_HasAllRequiredTables`.
- [ ] `BookIdentityService` resolves correctly for all five tiers; all unit tests pass
      on both Windows and macOS CI runners.
- [ ] At least one up migration and its corresponding down migration are present and
      both are idempotent (run twice without error); verified by `Migration_IsIdempotent`.
- [ ] `MigrationService.ApplyAsync()` produces a timestamped `.bak` file before applying
      and restores it if migration fails; verified by `Migration_BackupBeforeApply_Restores`.
- [ ] `SidecarService` resolves all six sidecar classes to the specified paths on both
      Windows and macOS; verified by cross-platform path tests.
- [ ] `ExportBundle_RoundTrip_PreservesAllData` passes: export, delete DB + sidecar,
      import, assert all rows and files intact.
- [ ] `ICatalogueReadModel.GetBookSummariesAsync` returns 2,000 rows in < 200 ms P95
      on CI (trend data if reference hardware not yet confirmed in Phase 00).
- [ ] Architecture tests: `Domain_HasNoOutwardDependencies` and
      `CatalogueDbContext_UsedOnlyInInfrastructure` both green.
- [ ] At-rest encryption compiles and the toggle pathway is integration-tested
      (even if key-management UI is deferred to Phase 19).
- [ ] All public types have XML doc comments; `catalogue-data-model.md` is current.
- [ ] Builds and tests pass on **both Windows and macOS** CI runners (no
      platform-conditional compilation in the data layer).
- [ ] No hard-coded user-facing strings; `en` and `fr` resource keys present for
      all error/log messages that surface to the user.

---

## 11. Skills to use

Full invocation detail in `skills.md`.

- `backend-databases:database-design-engineering` — table model, index strategy,
  foreign-key cascade rules, query-plan analysis.
- `backend-databases:database-reliability` — migration backup/restore, encryption
  toggle transaction, export/import correctness.
- `backend-databases:database-internals` — SQLite VFS, WAL mode, connection-pool
  sizing, `EXPLAIN QUERY PLAN` on the read-model queries.
- `superpowers:test-driven-development` — write entity config tests and identity
  unit tests before implementing `BookIdentityService`.
- `superpowers:writing-plans` + `superpowers:executing-plans` — drive the WP sequence.
- `architecture:validation-contract` — define and enforce `ICatalogueReadModel`
  interface contracts.
- `documentation-generation:architecture-decision-records` — ratify ADR-0005;
  draft any amendment from the encryption spike.
- `/code-review` — end of each WP and before phase close.

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| Domain value objects + `IBookIdentityService` | `OgmaLibrary.Domain/Catalogue/` |
| EF Core entity configurations (20 tables) | `OgmaLibrary.Infrastructure/Persistence/Configurations/` |
| `CatalogueDbContext` + `IDesignTimeDbContextFactory` | `OgmaLibrary.Infrastructure/Persistence/` |
| Initial EF Core migration (+ down) | `OgmaLibrary.Infrastructure/Persistence/Migrations/` |
| `MigrationService` with backup-before-apply | `OgmaLibrary.Infrastructure/Persistence/` |
| `BookIdentityService` | `OgmaLibrary.Infrastructure/Catalogue/` |
| `SidecarService` | `OgmaLibrary.Infrastructure/Catalogue/` |
| `ICatalogueReadModel` + projection records | `OgmaLibrary.Application/Catalogue/` |
| `ExportBundleService` | `OgmaLibrary.Infrastructure/Catalogue/` |
| Encryption interceptor/VFS wrapper | `OgmaLibrary.Infrastructure/Persistence/Encryption/` |
| Architecture + integration tests | `OgmaLibrary.Tests/Catalogue/` |
| `docs/architecture/catalogue-data-model.md` | `docs/architecture/` |
| ADR-0005 ratified + encryption-approach amendment | `docs/adr/` |
| Golden-corpus DB fixture (seed data for 500/2,000 books) | `tests/fixtures/corpus/` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Encryption approach (VFS vs. column-level) not validated before WP7 | R1 | Phase 01 spike must close this; WP7 blocked until ADR amendment is ratified. |
| SHA-256 on 1,000-page PDFs is slow on first import | R3 | Benchmark in WP4; hash is computed in a `Workers` background job (Phase 05), not on the UI thread; size+mtime fast-path skips rehash on unchanged files. |
| SQLite WAL mode conflicts between multiple `DbContext` instances | R4 | Single `CatalogueDbContext` per process (DI singleton); WAL configured at startup; integration test verifies no SQLITE_BUSY under concurrent read. |
| Work/Edition schema is under-specified (SRS context gap) | R1 | Phase 00 must provide cardinality decision; this phase implements the schema only — merge/split UI is deferred; FK is nullable so data is never forced into an edition. |
| Export bundle size on large libraries (2,000 books + covers) | R3 | Covers are the dominant asset; export includes only the DB + the sidecar classes the user selects (covers optional); tested with synthetic 2,000-book corpus. |

---

## 14. Owner asks

1. **Work/Edition cardinality decision** (SRS context gap, Phase 00): does a Work
   have many Editions? Can a book belong to multiple Works? Required before WP9.
2. **Sidecar naming convention approval**: the layout in §7 resolves the SRS gap —
   please review and confirm (or amend) the directory names, sharding strategy,
   and sub-folder names before WP5 is built.
3. **Encryption approach sign-off** (after Phase 01 spike): approve the ADR amendment
   choosing SQLCipher-via-VFS vs. column-level AES before WP7 begins.
4. **At-rest encryption tier** (MVP vs V1): the table above marks encryption as V1;
   confirm this is acceptable (i.e., encryption is not required for the MVP release).
5. **No icon procurement request** for this phase (data layer only).

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand-plan agent | Initial draft authored. |
