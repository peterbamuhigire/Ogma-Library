# Phase 23 — Tasks

> Work packages → tasks. Read `README.md` first, especially the Extension SDK
> architecture, SLO definitions, and runbook requirements.

---

## Work Package 1: Beta Go-Live Readiness

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP1-T1 | Final verification pass on all G1-G8 gates: run the specific test methods from `docs/qa/BETA-GATES-STATUS.md`; update status to green or escalate any red gate before proceeding. | 0.5 d | Phase 21 QA sign-off | G1-G8 |
| P23-WP1-T2 | Complete the Deployment & Ops go-live checklist: signed artifacts (Phase 22), Velopack beta feed live, GitHub Release (beta) published, store submissions in review, SLO monitoring ready (WP2), runbooks ready (WP3). Commit `docs/ops/GO-LIVE-CHECKLIST.md`. | 0.5 d | Phase 22 complete | Go-live readiness |
| P23-WP1-T3 | Owner sign-off session: present the go-live checklist; record sign-off (name + date) in `docs/ops/GO-LIVE-CHECKLIST.md`. | — (owner time) | P23-WP1-T2 | Launch authorization |

---

## Work Package 2: SLO Monitoring Setup

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP2-T1 | Write `docs/ops/SLO-DEFINITIONS.md`: four SLOs (update-success ≥99.0%, crash-free ≥99.5%, release-host ≥99.9%, median update ≤8 s), measurement windows, error budgets, and the budget-burn policy (feature work pause at 50% burn). | 0.5 d | Phase 20 telemetry | SLO-001..004 |
| P23-WP2-T2 | Implement the opt-in telemetry aggregation for SLOs: extend `ITelemetryService` (Phase 20) to record `update_success` (bool), `session_crash` (bool), `update_download_ms` (long) events in the local rotating log; add a `SloAggregator` that computes 7-day rolling rates from the local log. | 0.5 d | Phase 20 WP8, P23-WP2-T1 | SLO-001/002/004 |
| P23-WP2-T3 | Implement startup `integrity_check` for the SQLite catalogue: on every launch, run `PRAGMA quick_check`; if it fails, display the corrupted-catalogue recovery wizard (localized) before opening the main window. | 0.5 d | Phase 04 | RUNBOOK-CORRUPTED-DB, R1 |
| P23-WP2-T4 | Create `docs/ops/SLO-DASHBOARD.md` (or a static HTML file committed to `docs/ops/`) showing a sample SLO report format; document how to generate the report from the local telemetry log using a provided Python/PowerShell script. | 0.5 d | P23-WP2-T2 | SLO monitoring |

---

## Work Package 3: Incident Response Runbooks

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP3-T1 | Write `docs/ops/SEV-TIERS.md`: SEV-1/2/3 tier definitions, criteria, response SLAs, and escalation contacts. | 0.25 d | README §3d | SEV tiers |
| P23-WP3-T2 | Write `docs/ops/RUNBOOK-KEY-COMPROMISE.md`: step-by-step from detection to recovery (revoke feed → new key → re-sign → emergency update → advisory). Include verification commands for each step. | 0.5 d | Phase 22 CTRL-OGMA-013 | Runbook 1 |
| P23-WP3-T3 | Write `docs/ops/RUNBOOK-MALICIOUS-UPDATE.md`: detect via client CTRL-OGMA-013 failure or user report → push descriptor update removing malicious version → coordinate with key-compromise runbook if applicable → advisory. | 0.5 d | Phase 22 trust chain | Runbook 2 |
| P23-WP3-T4 | Write `docs/ops/RUNBOOK-CRASH-SPIKE.md`: detect (telemetry or issue reports) → triage (OS/hardware/locale/library-size segmentation) → hotfix or rollback → postmortem template. | 0.25 d | Phase 20 fault injection | Runbook 3 |
| P23-WP3-T5 | Write `docs/ops/RUNBOOK-CORRUPTED-DB.md`: detect (startup `integrity_check` failure) → recovery wizard steps → `PRAGMA integrity_check` repair → restore from migration backup → rescan from source PDFs as last resort. | 0.25 d | P23-WP2-T3, Phase 04 | Runbook 4 |
| P23-WP3-T6 | Dry-run all four runbooks: for each runbook, execute steps 1-3 on a test environment (not production); record that each step produced the expected outcome; note any step that could not be executed (and why). Commit dry-run records to `docs/ops/`. | 0.5 d | P23-WP3-T2..T5 | Runbook verification |
| P23-WP3-T7 | Create `docs/ops/INCIDENT-LOG.md` as a blank template with the postmortem structure (timeline, impact, root cause, action items, SLO impact). | 0.25 d | P23-WP3-T1 | Incident response |

---

## Work Package 4: Public Beta Promotion

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP4-T1 | Promote the Phase 22 Beta channel artifact to the public beta Velopack feed (re-sign the feed descriptor for the public feed URL; do not rebuild the binary). | 0.25 d | Phase 22 promote.yml, P23-WP1-T3 | Beta go-live |
| P23-WP4-T2 | Publish the GitHub Release (beta) with the announcement text, links to the store listings, and a link to the beta-feedback GitHub Issues label. | 0.25 d | P23-WP4-T1 | Beta go-live |

---

## Work Package 5: Beta Soak (Ongoing — Not a Blocking Task)

| Task ID | Description | Duration | Satisfies |
| --- | --- | --- | --- |
| P23-WP5-T1 | Daily SLO check: review opt-in telemetry log; compute 7-day rolling SLO rates; record in `docs/ops/BETA-SOAK-REPORT.md`. | Daily, 14 days | SLO-001..004 |
| P23-WP5-T2 | GitHub Issues triage: label, categorize, and respond to `beta-feedback` issues within 48 hours; separate bugs from feature requests; link bugs to existing tracked defects or create new ones. | Daily, 14 days | Beta feedback |
| P23-WP5-T3 | Soak exit decision: after 14 days, evaluate: SLOs met for 7 consecutive days; no open SEV-1 or SEV-2 incident; blocker-bug count = 0. If pass, commit `docs/ops/BETA-SOAK-REPORT.md` with "SOAK COMPLETE" status and proceed to stable promotion. | 0.5 d | Beta soak exit |

---

## Work Package 6: Extension SDK Core

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP6-T1 | Create `OgmaLibrary.Extensions.Sdk` project: target `net10.0`; no dependency on `OgmaLibrary.Infrastructure`; `GenerateDocumentationFile=true`; `<Nullable>enable</Nullable>`; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. | 0.5 d | Phase 02 scaffold | L.8, L.7 |
| P23-WP6-T2 | Define all extension interfaces: `IMetadataProvider`, `IExportProvider`, `IOcrProvider`, `ILibraryImporter`, `IThemeProvider`, `IMcpExtension`, `IEgressPolicy`; add XML doc comments to all public members. | 1 d | P23-WP6-T1 | L.8, CTRL-OGMA-007 |
| P23-WP6-T3 | Define catalogue and search read APIs: `ICatalogueReadApi`, `ISearchReadApi`, all DTOs (`BookSummary`, `BookDetail`, `ShelfSummary`, `AnnotationSummary`, `ReadingProgress`, `SearchResult`, `SearchOptions`, `CatalogueQuery`). XML doc all types. | 0.5 d | P23-WP6-T1 | L.8 |
| P23-WP6-T4 | Implement `ExtensionLoader`: `AssemblyLoadContext.LoadFromAssemblyPath`; scan extensions directory; resolve `[OgmaExtension]`-attributed types; build extension DI container with `ICatalogueReadApi`, `ISearchReadApi`, `IAiProvider` (gateway-backed) available; block `ICredentialStore`, `IAuditTrailService`, and `HttpClient` registration. | 1 d | P23-WP6-T2..T3 | L.8 |
| P23-WP6-T5 | Architecture tests: `Extension_SdkDoesNotDependOnInfrastructure` (OgmaLibrary.Extensions.Sdk references only Domain + primitives); `Extension_CannotCallHttpDirectly` (extension DI container does not expose `IHttpClientFactory`); `Extension_CannotAccessCredentialStore` (extension DI container does not expose `ICredentialStore`). | 0.5 d | P23-WP6-T4 | CTRL-OGMA-007 |
| P23-WP6-T6 | Implement `CatalogueReadApiAdapter` and `SearchReadApiAdapter` in `OgmaLibrary.Infrastructure`: adapters that implement `ICatalogueReadApi` and `ISearchReadApi` by delegating to the existing `ICatalogueService` and `ISearchService`; registered as read-only in the extension DI container. | 0.5 d | P23-WP6-T3 | L.8 |

---

## Work Package 7: Bundled Importers

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP7-T1 | Implement `ZoteroRdfImporter`: parse Zotero RDF export (XML); map `rdf:Description` elements to `BookImportRecord`; handle multi-author arrays, tags, notes. | 0.5 d | P23-WP6-T2 | L.8, NFR-PROD-009 |
| P23-WP7-T2 | Implement `ZoteroJsonImporter`: parse Better BibTeX JSON export; map fields to `BookImportRecord`. | 0.5 d | P23-WP6-T2 | L.8 |
| P23-WP7-T3 | Implement `CalibreMetadataImporter`: parse `metadata.opf` (Dublin Core XML); map Dublin Core fields to `BookImportRecord`; handle Calibre custom columns as tags. | 0.5 d | P23-WP6-T2 | L.8 |
| P23-WP7-T4 | Implement `GoodreadsCSVImporter`: parse Goodreads export CSV; map `Title`, `Author`, `ISBN13`, `Bookshelves`, `My Rating`, `Date Read` to `BookImportRecord`. | 0.25 d | P23-WP6-T2 | L.8 |
| P23-WP7-T5 | Write importer tests (`OgmaLibrary.Tests.Importers`): for each importer, test against a golden-corpus import fixture (a sample export file for each source); assert that all expected `BookImportRecord` fields are populated. Include edge cases (missing ISBN, multi-author, empty shelf list). | 0.5 d | P23-WP7-T1..T4 | L.8 verification |
| P23-WP7-T6 | Wire importers into the `ImportReviewService` UI flow (already scaffolded in Phase 05/07); add "Import from Zotero/Calibre/Goodreads" menu item to the Library menu; localize all new strings in all 5 locales. | 0.5 d | P23-WP7-T1..T4, Phase 05 | L.8, I18N |

---

## Work Package 8: MCP Extension Surface

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP8-T1 | Define `IMcpExtension`, `McpTool`, `McpToolCall`, `McpToolResult` types in the Extension SDK; XML doc all public types; document the MCP protocol binding contract. | 0.25 d | P23-WP6-T1 | L.8 |
| P23-WP8-T2 | Implement `McpListenerScaffold`: an opt-in, loopback-only `System.Net.HttpListener` that routes MCP protocol requests to registered `IMcpExtension` implementations; default off; enabled via `AppSettings.EnableMcpListener`; listens on `http://127.0.0.1:<configurable-port>/mcp`. | 0.5 d | P23-WP8-T1 | L.8, loopback-only |
| P23-WP8-T3 | Add `EnableMcpListener` toggle to Settings; localize in all 5 locales; display a warning that enabling the MCP listener starts a local HTTP server (and that it is loopback-only); keyboard-accessible and screen-reader-announced. | 0.25 d | P23-WP8-T2 | L.8, NFR-PROD-007 |

---

## Work Package 9: Extension SDK Developer Docs

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP9-T1 | Configure DocFX: `docfx.json` pointing at `OgmaLibrary.Extensions.Sdk` XML docs; generate API reference HTML into `docs/sdk/api/`; set up GitHub Pages workflow to publish from `docs/sdk/`. | 0.5 d | P23-WP6-T2..T3 | L.8, L.7 |
| P23-WP9-T2 | Write getting-started tutorial: `docs/sdk/tutorials/01-build-a-metadata-provider.md` — step-by-step from "create a .NET class library" to "implement `IMetadataProvider`" to "drop the DLL in the Extensions folder" to "see the provider in action." Aimed at a .NET developer unfamiliar with Ogma Library. | 1 d | P23-WP6-T4 | L.8 |
| P23-WP9-T3 | Write extension architecture guide: `docs/sdk/EXTENSION-ARCHITECTURE.md` — how extensions are loaded (`AssemblyLoadContext`), the DI container boundary, the egress policy model, the privacy tier model for extensions, versioning policy (major version bump = breaking change), and the deprecation policy (2 major versions). | 0.5 d | P23-WP6-T4..T5 | L.8 |
| P23-WP9-T4 | Write importer documentation: `docs/sdk/importers/ZOTERO.md`, `CALIBRE.md`, `GOODREADS.md` — data-mapping tables, example export files, and any known limitations. | 0.5 d | P23-WP7-T1..T4 | L.8 |
| P23-WP9-T5 | Write MCP extension guide: `docs/sdk/MCP-EXTENSION.md` — what MCP is, how the loopback listener works, the built-in tools (future: `search_books`, etc.), and how to build a custom MCP extension. | 0.5 d | P23-WP8-T1..T2 | L.8 |
| P23-WP9-T6 | Update `CONTRIBUTING.md`: add Extension SDK contribution section (how to submit a community extension, review process, code quality expectations, the versioning policy); confirm CODE_OF_CONDUCT.md is still current. | 0.5 d | P23-WP9-T3 | L.7, L.8 |

---

## Work Package 10: Open-Source Release Readiness

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP10-T1 | Run `gitleaks` on full git history: `gitleaks detect --source .`; if any secrets found, execute the BFG clean procedure, rotate the credential, and re-scan. Zero findings required before the repo is made public. | 0.25 d | All prior phases | L.7, R2 |
| P23-WP10-T2 | Verify XML doc coverage: `dotnet build --warningsAsErrors`; confirm `GenerateDocumentationFile=true` is set in all public projects; fix any remaining `CS1591` (missing XML comment) warnings. | 0.25 d | All prior phases | L.7 |
| P23-WP10-T3 | Run `/init` to update `CLAUDE.md`: verify it accurately describes the current project structure, build commands, test commands, and architecture; commit any updates. | 0.25 d | All prior phases | L.7 |
| P23-WP10-T4 | Confirm `LICENSE`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md` are at repo root and current; confirm the CLA/DCO mechanism is operational (test a dummy PR through the DCO check). | 0.25 d | Phase 00 | L.7 |

---

## Work Package 11: V1/V2 Roadmap

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P23-WP11-T1 | Write `docs/roadmap/V1-V2-ROADMAP.md`: V1 items (FR-READ-012, FR-AI-008, FR-SEARCH-002/004/005, Phases 16-18, Phase 15, MCP-server built-in extension, es/it/de complete from Phase 21) and V2 items (OQ-07 EPUB/CBZ, OQ-08 cloud sync with DPIA, advanced classroom admin, split view, answer mode). Include rationale, dependencies, and indicative effort ranges. | 0.5 d | All prior phases | V1/V2 roadmap |
| P23-WP11-T2 | Owner sign-off on `docs/roadmap/V1-V2-ROADMAP.md`; record name + date in the document. | — (owner time) | P23-WP11-T1 | Owner commitment |
