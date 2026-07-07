# Core Product Functionality

Score: **64 / 100**. Weight: 12%.

Coverage reviewed: catalogue/grid/list/detail, reader/session/rendering/annotations/bookmarks/citations, search/OCR/semantic search, AI advisor, classroom host/client/admin, and 3D shelf fallback.

| ID | Location | Rule violated | Severity | Evidence | Consequence |
| --- | --- | --- | --- | --- | --- |
| F-FUNC-001 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_TestCompletionReport.txt:1` | Large-library workflows must pass performance/reliability tests. | High | Diagnostic run reports failing `HealthDashboardTests.BatchEnrichment_2000Books_CompletesWithRetry`. | Metadata health/enrichment is unreliable at realistic library size. |
| F-FUNC-002 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_PRD.txt:1`, `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | PRD-level product is not complete until public-beta gates close. | High | References say phases 19-23 remain to reach beta and DeploymentOps says NO-GO. | Users cannot receive a working released product. |
| F-FUNC-003 | `src/OgmaLibrary.Application/Ai/AdvisorService.cs:56`, `docs/plans/grand-plan/phase-15/tasks.md:65` | V1 surfaces must not rely on V2 placeholders. | Medium | AI answer mode throws; split view remains placeholder. | Important reader/AI workflows end prematurely. |
| F-FUNC-004 | `src/OgmaLibrary.App/Views/Catalogue/CatalogueGridView.axaml:43`, `docs/plans/grand-plan/phase-10/icons.md:96` | Placeholder assets cannot ship as finished product. | Medium | Catalogue cover placeholder and icon release blocker are documented. | The product feels unfinished even where logic works. |

90%+ means all V1 workflows execute end-to-end from first launch to library import, reading, search, AI, classroom, and sync; placeholder/V2-only routes are completed or removed from beta scope; large-library metadata/OCR jobs pass under retry/load conditions.
