# Data Layer and Migrations

Score: **68 / 100**. Weight: 10%.

Coverage reviewed: `CatalogueDbContext`, entity configurations, EF migrations, repository transactions, student private rows, search/FTS/embedding storage, and migration tests.

| ID | Location | Rule violated | Severity | Evidence | Consequence |
| --- | --- | --- | --- | --- | --- |
| F-DATA-001 | `src/OgmaLibrary.Infrastructure/Catalogue/CatalogueMigrator.cs:139`, `:167`, `:179` | Migrations must be deterministic; repair paths must be bounded and documented. | Medium | Migrator contains special repair for pre-created Phase 18 tables and manual FTS creation. | Schema drift is survivable but indicates migration history is not fully clean. |
| F-DATA-002 | `src/OgmaLibrary.Infrastructure/Catalogue/Entities/EnrolledProfileRow.cs:18`, `src/OgmaLibrary.Infrastructure/SchoolAdmin/SchoolProfileEnrollmentService.cs:152` | Schema comments and code must agree for security-sensitive fields. | Medium | Entity comment says token hash or opaque token until hashing lands; service now hashes tokens. | Maintainers can misread the storage guarantee. |
| F-DATA-003 | `src/OgmaLibrary.Infrastructure/Catalogue/CatalogueDbContext.cs:220`, `:224` | SQLite durability settings require explicit operational validation. | Medium | PRAGMA foreign keys and WAL are enabled at open. | Good default exists, but release evidence must prove backup/restore and corruption handling on target platforms. |

Strengths: the EF model is broad and centralized; migration tests cover backup-before-apply, idempotency, repair, and Phase 18 roundtrips.

90%+ means repair debt is documented or eliminated, security-sensitive comments match behavior, backup/restore/corruption tests run on Windows and macOS, and search/FTS/embedding indexes have integrity-check and rebuild evidence.
