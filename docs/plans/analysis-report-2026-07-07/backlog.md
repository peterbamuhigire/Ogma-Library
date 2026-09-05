# Remediation Backlog - 2026-07-07

Record unrelated findings here. Do not fix them inline during a phase unless
the phase plan names them.

| ID | Severity | Location | Description | Recommended phase |
| --- | --- | --- | --- | --- |
| BL-2026-07-07-001 | Resolved | `src/OgmaLibrary.App/Views/Catalogue/CatalogueGridView.axaml`; `src/OgmaLibrary.App/Views/Catalogue/CatalogueListView.axaml`; `src/OgmaLibrary.App/Views/Catalogue/CatalogueDirectoryView.axaml` | Resolved 2026-09-05: `BookSummaryProjection.PrimaryAuthor` supplies a safe display fallback and all three catalogue surfaces bind to it; regression coverage is in `CatalogueGridTests.BookSummaryProjection_UsesSafePrimaryAuthorFallback`. | Phase 08 |
