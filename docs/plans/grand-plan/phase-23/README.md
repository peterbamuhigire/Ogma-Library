# Phase 23 — Beta, Launch & Post-Launch Operations + Extension SDK

One sentence: Clear all eight public-beta gates, execute the public beta and
soak period against defined SLOs, stand up the operational runbooks and incident
response, and deliver the Extension SDK with full developer documentation as the
open-source foundation for community extensibility.

---

## 1. Status & metadata

| Field | Value |
| --- | --- |
| **Status** | Not started |
| **Tier** | MVP (beta go-live, SLOs, runbooks) + Final (Extension SDK, open-source release, V1/V2 roadmap) |
| **Estimate** | 3 engineer-weeks (+ ongoing beta soak) |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD Phase 7 (launch, operations, extensibility) |
| **Platforms** | Windows 10 1903+ + macOS 13 Ventura+ (both required for beta soak) |
| **Baseline date** | 2026-05-30 |

---

## 2. Objectives

1. All eight public-beta gates (G1–G8) are green, verified, and recorded in
   `docs/qa/BETA-GATES-STATUS.md` (carried forward from Phase 21); the
   release-engineering and ops/infra readiness checklist from
   Deployment & Ops is complete; the go-live readiness gate is signed off
   by the owner.
2. The public beta is live on the Beta Velopack feed and GitHub Releases;
   the beta soak runs for a minimum of two weeks; SLOs are monitored.
3. SLOs are operational and metered:
   - Update-success rate ≥ 99.0%.
   - Crash-free session rate ≥ 99.5%.
   - Release-host availability ≥ 99.9%.
   - Median update download ≤ 8 s @25 Mbps.
4. Error budgets are defined; SEV-1/2/3 tiers are documented; four runbooks
   are written and verified: signing-key compromise, malicious update injection,
   crash spike, corrupted catalogue/database.
5. The **Extension SDK** is delivered: stable, versioned public interfaces for
   metadata enrichment, export, OCR, and AI providers; a documented read API
   over catalogue and search; theme/icon pack support; Zotero/Calibre/Goodreads
   importers; an MCP-server extension surface — all with full developer docs
   (API reference, tutorials, contribution guide).
6. All off-device traffic from extensions routes through the existing AI gateway
   and privacy-tier model (CTRL-OGMA-007); extensions cannot bypass the egress
   chokepoint.
7. Open-source release readiness is confirmed: LICENSE, CONTRIBUTING,
   CODE_OF_CONDUCT, and CLA mechanism are finalized; the developer guide and
   XML doc comments are complete; the repository is ready for public visibility.
8. The V1/V2 roadmap is documented and owner-signed: split view (FR-READ-012),
   AI answer mode with local-evidence citation (FR-AI-008), full LAN/classroom
   hardening (Phases 16–18 complete), EPUB/CBZ revisit (OQ-07), cloud sync
   DPIA (OQ-08).

---

## 3. Scope

### In scope

#### 3a. Beta go-live readiness gate

Review all eight gates (SOURCE-SUMMARY.md §J):
- **G1** WebView bridge stability.
- **G2** PDFium wrapper benchmark.
- **G3** 500-book responsiveness.
- **G4** 2,000-book responsiveness.
- **G5** Write-back backup/restore.
- **G6** AI payload preview.
- **G7** Index rebuild.
- **G8** Interrupted-job recovery.

Plus the Deployment & Ops release-engineering checklist:
- Signed artifacts (Phase 22 CI pipeline).
- Velopack beta feed live.
- GitHub Release (beta) published.
- Windows Store submission accepted (or in review with a fallback plan).
- MAS submission accepted (or in review with a fallback plan).
- SLO monitoring operational.
- Crash-free reporting operational (device-local aggregation; no PII).
- Runbooks written and dry-run tested.
- On-call rotation defined (even if it is one person for MVP launch).

#### 3b. Public beta + soak

- Promote the Phase 22 beta channel artifact to the public beta Velopack feed.
- Announce beta via GitHub Releases.
- Soak period: minimum 14 calendar days.
- Beta feedback collection: GitHub Issues labeled `beta-feedback`.
- Soak exit criterion: SLOs are met for 7 consecutive days within the soak
  period; no SEV-1 incident open at soak close.

#### 3c. SLOs and error budgets

| SLO | Threshold | Measurement window | Error budget |
| --- | --- | --- | --- |
| Update-success rate | ≥ 99.0% | 7-day rolling | 1.0% failure rate |
| Crash-free session rate | ≥ 99.5% | 7-day rolling | 0.5% crash rate |
| Release-host availability (GitHub Releases) | ≥ 99.9% | 30-day rolling | 43.8 min/month |
| Median update download time | ≤ 8 s @25 Mbps | per-download sample | P50 budget |

Measurement: opt-in device-local telemetry (Phase 20) aggregated at the client;
no data leaves the device. SLO compliance is self-reported by the device and
aggregated only if the user opts in. At MVP launch, SLOs are monitored via opt-in
telemetry + manual GitHub Releases download analytics (GitHub provides aggregate,
non-identifiable download counts).

Error budget policy: if the crash-free SLO error budget is consumed by >50% in
any 7-day window, all new feature work pauses and the team shifts to reliability
fixes until the budget is restored. This is the only automated policy at MVP;
advanced error-budget alerting is a V1 ops improvement.

#### 3d. Incident response: SEV tiers and runbooks

**SEV tier definitions:**

| Tier | Criteria | Response SLA | Escalation |
| --- | --- | --- | --- |
| SEV-1 | Signed malicious update distributed; signing key compromised; crash-free < 95% within any 1-hour window | 15 min detect → 1 hour contain | Owner + team immediate; GitHub Security Advisory |
| SEV-2 | Crash-free 95–99.5% over 24 hours; update-success < 95% over 24 hours; corrupted catalogue affecting multiple users | 4 hours detect → 8 hours contain | Owner notified within 4 hours |
| SEV-3 | Single-user corrupted catalogue; individual update failure; search index corruption | 24 hours detect → 48 hours contain | Tracked in GitHub Issues; normal sprint |

**Runbooks (four written and dry-run tested):**

1. **Signing-key compromise runbook** (`docs/ops/RUNBOOK-KEY-COMPROMISE.md`):
   Detect (monitoring alert or user report) → revoke the Velopack feed (push a
   `revoked: true` flag in the signed descriptor; clients refuse to apply) →
   generate new Ed25519 key pair → re-sign all channel feed descriptors with
   new key → push an emergency update embedding the new public key → announce
   via GitHub Security Advisory → postmortem within 72 hours.

2. **Malicious update injection runbook** (`docs/ops/RUNBOOK-MALICIOUS-UPDATE.md`):
   Detect (user report or CTRL-OGMA-013 verification failure in client) →
   immediately push a feed descriptor update that removes the malicious version
   from the `available` list → if private key not compromised, re-sign; if
   compromised, execute signing-key compromise runbook simultaneously →
   GitHub Security Advisory → postmortem.

3. **Crash spike runbook** (`docs/ops/RUNBOOK-CRASH-SPIKE.md`):
   Detect (opt-in telemetry crash-free rate drop, or concentrated GitHub Issue
   reports with `crash` label) → triage: is it a specific OS version / hardware
   / locale / library size → if deterministic: hotfix branch, rapid fix, promote
   to beta soak, then stable → if non-deterministic: roll back to prior stable
   on the Velopack feed (push prior version as latest); notify users via update
   notification; postmortem.

4. **Corrupted catalogue/database runbook** (`docs/ops/RUNBOOK-CORRUPTED-DB.md`):
   Detect (user-reported `SQLite corruption` error or `integrity_check` failure
   in startup check) → display a localized recovery wizard → attempt
   `PRAGMA integrity_check` repair → if repair fails, restore from the most
   recent migration backup → if no backup: `ATTACH DATABASE` on the original
   PDFs and re-scan into a fresh catalogue, preserving file paths and content
   hashes (rebuilding metadata from the source) → document case in
   `docs/ops/INCIDENT-LOG.md`.

**Incident lifecycle:** Detect → Triage → Contain → Eradicate → Recover →
Postmortem. Every SEV-1 and SEV-2 incident produces a postmortem in
`docs/ops/INCIDENT-LOG.md` within 72 hours of resolution (SEV-1) or
1 week (SEV-2).

#### 3e. Extension SDK (Owner delta #7 and #8)

The Extension SDK exposes stable, versioned public interfaces so the community
can build on the curated book databases without forking Ogma Library.

**Extension SDK surfaces (all documented, all routed through the privacy/egress model):**

| Surface | Interface | Purpose |
| --- | --- | --- |
| Metadata provider | `IMetadataProvider` | Supply alternative metadata sources (e.g., Open Library v3, LibraryThing) |
| Export provider | `IExportProvider` | Export catalogue, annotations, and reading data to custom formats |
| OCR provider | `IOcrProvider` | Replace or supplement Tesseract with a custom OCR engine |
| AI provider | `IAiProvider` (existing) | Already extensible; SDK formalizes the contract and provides test harness |
| Catalogue read API | `ICatalogueReadApi` | Read-only, stable query API over the SQLite catalogue: books, authors, shelves, reading progress, annotations |
| Search read API | `ISearchReadApi` | Execute metadata, FTS5, and semantic searches programmatically from an extension |
| Theme/icon pack | `IThemeProvider` | Supply alternative color-token sets and icon PNG directories |
| Importers | `ILibraryImporter` | Zotero RDF/JSON, Calibre metadata.opf, Goodreads CSV — convert to Ogma catalogue entries |
| MCP-server surface | `IMcpExtension` | Expose a local MCP (Model Context Protocol) server that lets AI clients query the catalogue and search index |

**Extension loading mechanism:**
Extensions are .NET assemblies placed in `%LocalAppData%\OgmaLibrary\Extensions\`
(Windows) or `~/Library/Application Support/OgmaLibrary/Extensions/` (macOS);
loaded at startup via `AssemblyLoadContext.LoadFromAssemblyPath`; sandboxed by
the plugin DI container (extensions cannot access `ICredentialStore`,
`IAuditTrailService`, or any `Infrastructure.Security` namespace). Extensions
that call off-device resources must declare an `IEgressPolicy` at registration;
the gateway enforces the privacy tier.

**MCP extension surface:**
An opt-in local HTTP listener (separate from the LAN Host; loopback only;
default off) that implements the MCP protocol, exposing tools: `search_books`,
`get_book_metadata`, `get_annotations`, `read_page`. AI clients (Claude Desktop,
Cursor, etc.) can connect to this MCP server to query the user's curated library.
This is a V1/V2 feature; the interface is defined and documented in Phase 23
but the implementation is scheduled for V1 roadmap execution.

**Privacy constraint (CTRL-OGMA-007):** All extension egress (metadata lookups,
AI calls, OCR cloud APIs) is routed through `IAiProvider` / the egress gateway.
An architecture test (`Extension_CannotCallHttpDirectly`) asserts that the
extension DI container does not register `HttpClient` directly; only the
gateway's `IHttpClientFactory` is available.

**Developer documentation (open-source mandate, Owner delta #7):**
- API reference: XML doc comments compiled to HTML via DocFX (or equivalent);
  hosted on GitHub Pages at `chwezi.github.io/ogma-library/sdk/`.
- Getting started tutorial: "Build a metadata provider in 30 minutes."
- Extension architecture guide: how extensions are loaded, the DI container
  boundary, the egress policy model, the privacy tier model for extensions.
- Contribution guide: how to submit an extension to the community index;
  review process; code quality expectations.
- Importer documentation: one guide per importer (Zotero, Calibre, Goodreads)
  with data-mapping tables.

#### 3f. Open-source release readiness

- `LICENSE` (from Phase 00) confirmed current and correct at the release tag.
- `CONTRIBUTING.md` updated with the Extension SDK contribution workflow.
- `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1) confirmed.
- CLA / DCO mechanism confirmed operational.
- All public types/members have XML doc comments
  (`GenerateDocumentationFile=true` enforced since Phase 02).
- `CLAUDE.md` is current (the `init` slash command is used to verify and
  update it before the public release tag is pushed).
- No secrets, keys, or proprietary lock-in in the repository history
  (`gitleaks` scan passes on the full history).

#### 3g. V1/V2 roadmap documentation

`docs/roadmap/V1-V2-ROADMAP.md` documents the prioritized backlog:
- **V1:** FR-READ-012 split view, FR-AI-008 answer mode with local evidence,
  FR-SEARCH-002/003/004/005 full semantic search hardening, LAN/classroom
  Phases 16–18 productized, OCR Phase 15 hardened, MCP-server extension live.
- **V2:** EPUB/CBZ revisit (OQ-07), cloud sync with DPIA (OQ-08), full
  classroom admin hardening, advanced AI entitlements.

### Explicitly out of scope

- Cloud telemetry pipeline (device-local only at MVP).
- Implementing the MCP-server extension (interface defined; implementation V1).
- Implementing cloud sync (DPIA required; OQ-08 explicitly deferred).
- Android/iOS mobile apps (not in product scope at any tier).
- LAN Host hardening beyond Phase 16 baseline (V1/V2 roadmap).

---

## 4. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| G1 | All | WebView bridge stability | `docs/qa/BETA-GATES-STATUS.md` green |
| G2 | All | PDFium wrapper benchmark | `docs/qa/BETA-GATES-STATUS.md` green |
| G3 | All | 500-book responsiveness | `docs/qa/BETA-GATES-STATUS.md` green |
| G4 | All | 2,000-book responsiveness | `docs/qa/BETA-GATES-STATUS.md` green |
| G5 | All | Write-back backup/restore | `docs/qa/BETA-GATES-STATUS.md` green |
| G6 | All | AI payload preview | `docs/qa/BETA-GATES-STATUS.md` green |
| G7 | All | Index rebuild | `docs/qa/BETA-GATES-STATUS.md` green |
| G8 | All | Interrupted-job recovery | `docs/qa/BETA-GATES-STATUS.md` green |
| SLO-001 | MVP | Update-success ≥ 99.0% | Opt-in telemetry + GitHub download analytics |
| SLO-002 | MVP | Crash-free ≥ 99.5% (= NFR-PROD-006) | Opt-in telemetry; crash-free rate |
| SLO-003 | MVP | Release-host availability ≥ 99.9% | GitHub uptime SLA |
| SLO-004 | MVP | Median update download ≤ 8 s | Client-side timing in opt-in telemetry |
| L.7 | MVP | Open-source readiness | `gitleaks` scan; XML docs enforced; governance files present |
| L.8 | Final | Extension SDK + developer docs | SDK interfaces; DocFX reference; tutorials |
| FR-AI-002 | MVP | IAiProvider is extensible | `IMetadataProvider`/`IAiProvider` in Extension SDK |
| CTRL-OGMA-007 | MVP | Single egress chokepoint for extensions | `Extension_CannotCallHttpDirectly` architecture test |
| NFR-PROD-009 | MVP | Portability / no lock-in | Open-source license; standard formats; importer documentation |

---

## 5. Dependencies

### Depends on

- **Phase 21**: All G1-G8 gates green; WCAG 2.2 AA sign-off; comprehensive
  review resolved; golden-corpus E2E signed off.
- **Phase 22**: Signed artifacts on the beta Velopack feed; GitHub Releases
  (beta) created; store submissions in review.
- **Phase 12**: `IAiProvider` interface — the Extension SDK formalizes this
  as the primary extensibility contract.
- **Phase 10-11**: `ICatalogueReadApi` and `ISearchReadApi` are read-model
  projections of the Search and Catalogue contexts.

### Unblocks

- **Community**: the public open-source release and Extension SDK unblock
  third-party metadata providers, importers, and AI integrations.
- **V1/V2 roadmap**: the roadmap document is the contract between the owner
  and contributors for what comes next.

---

## 6. Architecture & approach

### Extension SDK architecture

The Extension SDK is a separate NuGet package: `OgmaLibrary.Extensions.Sdk`.
It contains only the public interfaces, the base classes, and the attribute types.
It has no dependency on `OgmaLibrary.Infrastructure` or `OgmaLibrary.Application`.
Only `OgmaLibrary.Domain` types and primitive types cross the SDK boundary.

```
OgmaLibrary.Extensions.Sdk (NuGet, public)
  ├── Interfaces/
  │   ├── IMetadataProvider.cs
  │   ├── IExportProvider.cs
  │   ├── IOcrProvider.cs
  │   ├── ILibraryImporter.cs
  │   ├── IThemeProvider.cs
  │   ├── IMcpExtension.cs
  │   └── IEgressPolicy.cs
  ├── ReadApi/
  │   ├── ICatalogueReadApi.cs
  │   └── ISearchReadApi.cs
  └── Registration/
      └── OgmaExtensionAttribute.cs
```

Extensions are discovered via `[OgmaExtension]` attribute on the implementing
class; no MEF or reflection-heavy discovery. The host app uses a simple
`ExtensionLoader` that scans the extensions directory, loads each assembly, and
resolves `IOgmaExtension`-attributed types via the extension DI container.

### Catalogue and Search read APIs

```csharp
/// <summary>
/// A stable, read-only API over the Ogma Library catalogue.
/// Extensions use this to query books, authors, shelves, and annotations.
/// All queries execute against a read-only snapshot; write operations
/// are not available through this API.
/// </summary>
public interface ICatalogueReadApi
{
    Task<IReadOnlyList<BookSummary>> SearchAsync(CatalogueQuery query,
        CancellationToken ct = default);
    Task<BookDetail?> GetBookAsync(BookId id, CancellationToken ct = default);
    Task<IReadOnlyList<ShelfSummary>> GetShelvesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AnnotationSummary>> GetAnnotationsAsync(BookId id,
        CancellationToken ct = default);
    Task<ReadingProgress?> GetReadingProgressAsync(BookId id,
        CancellationToken ct = default);
}

/// <summary>
/// A stable, read-only API over the Ogma Library search index.
/// Extensions use this to execute metadata, full-text, and semantic searches.
/// All off-device search calls are routed through the egress gateway.
/// </summary>
public interface ISearchReadApi
{
    Task<SearchResult> SearchMetadataAsync(string query, SearchOptions? options = null,
        CancellationToken ct = default);
    Task<SearchResult> SearchFullTextAsync(string query, SearchOptions? options = null,
        CancellationToken ct = default);
    Task<SearchResult> SearchSemanticAsync(string query, SearchOptions? options = null,
        CancellationToken ct = default);
}
```

### Importers

Three bundled importers (delivered as part of the SDK, not as external extensions):

- **Zotero importer** (`ZoteroRdfImporter`, `ZoteroJsonImporter`): reads
  Zotero RDF export or Better BibTeX JSON; maps to `BookImportRecord` containing
  title, authors, ISBN, tags, publisher, year, notes. Handles Zotero's
  multi-author array and tag hierarchy.

- **Calibre importer** (`CalibreMetadataImporter`): reads Calibre's
  `metadata.opf` (Dublin Core XML) or `metadata.db` (SQLite); maps to
  `BookImportRecord`. Handles Calibre's custom column schema.

- **Goodreads importer** (`GoodreadsCSVImporter`): reads the Goodreads export
  CSV; maps `Title`, `Author`, `ISBN`, `Shelves`, `Rating`, `Date Read` to
  `BookImportRecord`.

All importers implement `ILibraryImporter`:
```csharp
public interface ILibraryImporter
{
    string Name { get; }
    string Description { get; }
    IAsyncEnumerable<BookImportRecord> ImportAsync(ImportSource source,
        CancellationToken ct = default);
}
```

The import flow: importer → `BookImportRecord` → `ImportReviewService`
(user reviews/rejects each record) → `CatalogueService.AddBookAsync`
(only for records the user accepts).

### MCP-server extension surface (interface defined, implementation V1)

```csharp
/// <summary>
/// Defines a local MCP (Model Context Protocol) server extension that exposes
/// catalogue and search capabilities to AI clients (Claude Desktop, Cursor, etc.).
/// The MCP listener is loopback-only, opt-in, and default-off.
/// All tool calls route through ICatalogueReadApi and ISearchReadApi.
/// </summary>
public interface IMcpExtension
{
    string ServerId { get; }
    IReadOnlyList<McpTool> Tools { get; }
    Task<McpToolResult> ExecuteToolAsync(McpToolCall call, CancellationToken ct = default);
}
```

Phase 23 delivers the interface, the MCP server scaffold (opt-in listener
infrastructure), and the developer documentation for building MCP extensions.
The built-in `OgmaLibraryCatalogueMcpExtension` (exposing `search_books`,
`get_book_metadata`, `get_annotations`, `read_page` tools) is a V1 deliverable.

### Cross-platform approach

- The Extension SDK (`OgmaLibrary.Extensions.Sdk`) targets .NET 10; compatible
  with both Windows and macOS extension authors.
- Extension loading via `AssemblyLoadContext` is cross-platform.
- The MCP listener uses `System.Net.HttpListener` (cross-platform in .NET 10).
- All operational runbooks have both Windows and macOS paths where relevant
  (e.g., the corrupted-DB runbook's file paths differ by OS).
- Beta soak monitoring includes both OS telemetry streams.

---

## 7. Work breakdown (summary)

| WP | Work package | Estimate |
| --- | --- | --- |
| P23-WP1 | Beta go-live readiness: G1-G8 final verification; Deployment & Ops checklist; owner sign-off | 1 d |
| P23-WP2 | SLO monitoring setup: telemetry aggregation, error-budget dashboard (GitHub Pages or local HTML), SLO alerting | 1.5 d |
| P23-WP3 | Incident response: 4 runbooks written + dry-run tested; SEV-tier definitions; on-call setup | 1.5 d |
| P23-WP4 | Public beta promotion: promote Phase 22 beta artifact to public beta feed; GitHub Release announced | 0.5 d |
| P23-WP5 | Beta soak monitoring: 14-day soak; daily SLO check; GitHub Issues triage (ongoing, not blocking) | 2 wk soak |
| P23-WP6 | Extension SDK: interfaces, base classes, extension loader, architecture test | 3 d |
| P23-WP7 | Bundled importers: Zotero, Calibre, Goodreads | 2 d |
| P23-WP8 | MCP-server extension surface: interface, scaffold, loopback listener (opt-in, off by default) | 1 d |
| P23-WP9 | Extension SDK developer docs: DocFX reference, getting-started tutorial, architecture guide, contribution guide, importer docs | 3 d |
| P23-WP10 | Open-source release readiness: CONTRIBUTING update, CODE_OF_CONDUCT confirm, gitleaks scan, CLAUDE.md update, XML docs check | 1 d |
| P23-WP11 | V1/V2 roadmap documentation: `docs/roadmap/V1-V2-ROADMAP.md`; owner sign-off | 0.5 d |

Detail in `tasks.md`.

---

## 8. Cross-cutting checklist

- [x] **Colorful icons + manifest:** `icons.md` is a stub (no new UI surfaces
  in Phase 23 core). The import wizard UI (WP7) reuses existing import-flow
  icons from the Phases 05/07 manifest. Any new icons for the import wizard
  are noted in `icons.md`.
- [x] **i18n (en/fr/es/it/de):** All Extension SDK user-facing strings
  (importer names, error messages, the "LAN Host not available in MAS" notice
  from Phase 22 already localized) are externalized in all 5 locales. The
  `CONTRIBUTING.md` and developer docs are English-only (technical docs).
- [x] **Accessibility (keyboard + SR):** The import wizard UI (WP7) follows
  the Phase 21 a11y standards; all controls are keyboard-operable and
  screen-reader-announced. No new accessibility technical debt is introduced.
- [x] **Privacy/egress:** `Extension_CannotCallHttpDirectly` architecture test
  enforces that extensions route all egress through the gateway.
  The MCP listener is loopback-only and opt-in.
- [x] **Reversibility:** The import flow (WP7) shows a preview before writing
  to the catalogue; user confirms each record; no bulk irreversible import.
- [x] **Performance budgets:** No changes to the core performance paths; the
  extension loader is measured (extension load time < 500 ms for a 10-extension
  set) and documented.
- [x] **Bounded-context tests:** `Extension_SdkDoesNotDependOnInfrastructure`
  architecture test; `Extension_CannotCallHttpDirectly`; `Extension_CannotAccessCredentialStore`.
- [x] **Documentation:** SDK API reference (DocFX); tutorials; CONTRIBUTING
  updated; CLAUDE.md current; `docs/roadmap/V1-V2-ROADMAP.md` committed.

---

## 9. Definition of Done

### Global DoD (Phase 23 slice)

- [ ] All G1-G8 gates confirmed green in `docs/qa/BETA-GATES-STATUS.md`;
  owner sign-off on go-live readiness recorded.
- [ ] Public beta live on Velopack beta feed; GitHub Release (beta) published.
- [ ] SLO monitoring operational; SLOs met for 7 consecutive days within
  the 14-day beta soak.
- [ ] All four runbooks written and dry-run tested; SEV tiers documented;
  `docs/ops/` directory committed.
- [ ] `OgmaLibrary.Extensions.Sdk` NuGet package (prerelease) published or
  ready to publish; all extension interfaces documented with XML doc comments.
- [ ] Bundled importers (Zotero, Calibre, Goodreads) pass all tests against
  the golden-corpus import fixtures.
- [ ] `Extension_CannotCallHttpDirectly` and
  `Extension_SdkDoesNotDependOnInfrastructure` architecture tests pass.
- [ ] DocFX API reference generated and verified; getting-started tutorial
  reviewed by the owner.
- [ ] `gitleaks` scan on full git history: zero secrets found.
- [ ] All public types/members in `OgmaLibrary.Extensions.Sdk` have XML doc
  comments; `GenerateDocumentationFile=true` produces no warnings.
- [ ] `docs/roadmap/V1-V2-ROADMAP.md` committed and owner-signed.
- [ ] `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`,
  architecture tests all pass on both CI runners.
- [ ] `/code-review` completed on Extension SDK; findings resolved.
- [ ] No open R1 or R2 defect.

### Phase-specific exit criteria

- The beta soak exits with all SLOs in budget and no open SEV-1 or SEV-2 incident.
- The `OgmaLibrary.Extensions.Sdk` package is versioned `1.0.0-preview` or
  higher, with a stable `CHANGELOG.md` entry.
- Owner countersignature on `docs/roadmap/V1-V2-ROADMAP.md`.

---

## 10. Skills to use

See `skills.md` for full invocation guidance. Summary:

- `sdlc-meta:sdlc-user-deploy` + `sdlc-meta:sdlc-post-deployment` — go-live
  checklist and beta promotion.
- `devops-cloud:reliability-engineering` — SLO definition, error budgets,
  runbook design.
- `devops-cloud:observability-monitoring` — SLO monitoring instrumentation.
- `ai:ai-incident-response` — AI-specific incident runbooks (malicious update,
  key compromise).
- `documentation-generation:api-documenter` + `reference-builder` — Extension
  SDK API reference.
- `documentation-generation:tutorial-engineer` — getting-started tutorial.
- `sdlc-meta:mcp-builder` — MCP-server extension surface definition.
- `product-business:product-led-growth` — beta feedback loop.

---

## 11. Deliverables

| Artifact | Location |
| --- | --- |
| Beta go-live sign-off record | `docs/qa/BETA-GATES-STATUS.md` (updated) |
| Go-live readiness checklist | `docs/ops/GO-LIVE-CHECKLIST.md` |
| SLO definitions + error-budget policy | `docs/ops/SLO-DEFINITIONS.md` |
| SLO monitoring dashboard | `docs/ops/SLO-DASHBOARD.md` (or GitHub Pages) |
| Signing-key compromise runbook | `docs/ops/RUNBOOK-KEY-COMPROMISE.md` |
| Malicious update runbook | `docs/ops/RUNBOOK-MALICIOUS-UPDATE.md` |
| Crash spike runbook | `docs/ops/RUNBOOK-CRASH-SPIKE.md` |
| Corrupted catalogue runbook | `docs/ops/RUNBOOK-CORRUPTED-DB.md` |
| SEV-tier definitions | `docs/ops/SEV-TIERS.md` |
| Incident log | `docs/ops/INCIDENT-LOG.md` (blank template committed) |
| `OgmaLibrary.Extensions.Sdk` project | `src/OgmaLibrary.Extensions.Sdk/` |
| `OgmaLibrary.Extensions.Sdk` NuGet package | NuGet.org (or GitHub Packages) |
| Bundled importers | `src/OgmaLibrary.Infrastructure/Importers/` |
| Importer tests | `tests/OgmaLibrary.Tests.Importers/` |
| MCP extension surface | `src/OgmaLibrary.Extensions.Sdk/Interfaces/IMcpExtension.cs` |
| DocFX API reference | `docs/sdk/api/` (GitHub Pages source) |
| Getting-started tutorial | `docs/sdk/tutorials/01-build-a-metadata-provider.md` |
| Extension architecture guide | `docs/sdk/EXTENSION-ARCHITECTURE.md` |
| Contribution guide (updated) | `CONTRIBUTING.md` (updated) |
| V1/V2 roadmap | `docs/roadmap/V1-V2-ROADMAP.md` |
| Beta soak report | `docs/ops/BETA-SOAK-REPORT.md` |

---

## 12. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Store reviews (Windows/MAS) not accepted before beta go-live target date | R5 | Beta go-live does not require store acceptance (Velopack direct feed is the primary beta channel); stores can follow when accepted. Document the fallback in the go-live checklist. |
| Beta soak uncovers a crash-free SLO breach (crash-free < 99.5%) | R3 | The crash-spike runbook activates; the team has the beta soak period to remediate before stable promotion. The 14-day soak is specifically designed to provide this buffer. |
| Extension SDK breaks backward compatibility in V1 | R5 | All interfaces are versioned (`ISince(1, 0)` attribute); the SDK major version must be bumped for any breaking change; a deprecation policy (2 major versions) is documented in the contribution guide. |
| gitleaks scan finds historical secret in git log | R2 | Initiate BFG Repo Cleaner process to rewrite history; notify anyone who has cloned the repository; rotate the exposed credential immediately. This is an R2 risk; if found, it is a SEV-1 incident. |
| Beta feedback volume overwhelms the team | R5 | GitHub Issues `beta-feedback` label with a triage template; allocate 50% of team capacity to triage during the soak; close-won't-fix for out-of-scope requests with a reference to the V1/V2 roadmap. |
| MCP-server extension creates a new attack surface (loopback listener) | R2 | Opt-in, default-off; loopback only (no LAN exposure); the listener requires an explicit user action to enable; documents the threat model in `EXTENSION-ARCHITECTURE.md`; Phase 19 security model is extended to cover the MCP surface in the threat model review. |

---

## 13. Owner asks

1. **Go-live sign-off:** Review and sign off on `docs/ops/GO-LIVE-CHECKLIST.md`
   confirming that all G1-G8 gates are green and the product is ready for
   public beta. This is the formal launch authorization.
2. **SLO policy approval:** Review and approve `docs/ops/SLO-DEFINITIONS.md`
   including the error-budget policy (feature work pause when crash-free budget
   is 50% consumed). This is a team-level operational commitment.
3. **Extension SDK review:** Review the Extension SDK interfaces for stability
   and confirm the `1.0.0-preview` versioning is appropriate. The SDK becomes
   a public API commitment once published.
4. **V1/V2 roadmap countersignature:** Review and countersign
   `docs/roadmap/V1-V2-ROADMAP.md`; this is the owner's commitment to the
   community on what comes next.
5. **Getting-started tutorial review:** Read through the first tutorial
   ("Build a metadata provider in 30 minutes") and confirm it is accessible
   to a .NET developer unfamiliar with Ogma Library.
6. **Open-source release authorization:** Confirm that the repository is
   ready to be made public (or that public visibility is to be deferred until
   the stable release after the beta soak). This is the owner's decision.
7. **NuGet publishing authorization:** Confirm the NuGet package name
   (`OgmaLibrary.Extensions.Sdk`) and the publishing account (NuGet.org or
   GitHub Packages) for the Extension SDK.
8. **On-call setup:** Confirm the on-call arrangement for SEV-1 response during
   the beta soak period (minimum: one team member is reachable within 15 minutes
   during business hours; incident response expectations documented).

---

## 14. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand Plan authoring | v1.0 baseline created |
