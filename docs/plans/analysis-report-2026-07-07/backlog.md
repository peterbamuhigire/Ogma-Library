# Remediation Backlog - 2026-07-07

Record unrelated findings here. Do not fix them inline during a phase unless
the phase plan names them.

| ID | Severity | Location | Description | Recommended phase |
| --- | --- | --- | --- | --- |
| BL-2026-07-07-001 | High | `src/OgmaLibrary.App/Views/Catalogue/CatalogueGridView.axaml:71`; `src/OgmaLibrary.App/Views/Catalogue/CatalogueListView.axaml:60` | Compiled XAML bindings use `Authors[0]`; books with empty author lists can throw `ArgumentOutOfRangeException` before `FallbackValue` applies. Prefer binding to a safe `PrimaryAuthor` display property or a stateless first-author converter. | Phase 08 Catalogue UX and Asset Completion |
