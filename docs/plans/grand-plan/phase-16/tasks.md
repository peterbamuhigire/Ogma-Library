# Phase 16 — Tasks

Work packages and granular tasks for the LAN Library Server (Host Mode).
Task IDs follow the convention `P16-WP{n}-T{m}`.

---

## Work Package 1 — ADR-0010 & Transport Integration

**Goal:** Finalize transport architecture from Phase 01 spike; author ADR-0010;
wire certificate provisioner and mDNS advertiser.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP1-T1 | Read Phase 01 LAN spike output; extract transport decision (Kestrel vs HttpListener, mDNS library, certificate strategy) | Phase 01 DoD | 0.5 d | ADR-0010 |
| P16-WP1-T2 | Author `docs/architecture/adr-0010-lan-host-mode.md` covering: CI-2 scope amendment, transport choice, mDNS library, certificate TOFU flow, file-stream vs page-render decision, isolation mandate | P16-WP1-T1 | 1 d | ADR-0010, CI-2 amendment |
| P16-WP1-T3 | Owner sign-off on ADR-0010 (Peter must ratify CI-2 amendment — Owner ask §14.1) | P16-WP1-T2 | 0 d (gate) | CI-2 (amended) |
| P16-WP1-T4 | Implement `ICertificateProvisioner`: generate self-signed root CA on first start; persist CA private key in OS credential store (DPAPI / macOS Keychain); expose fingerprint hex | P16-WP1-T3 | 1 d | FR-LAN-002, CTRL-OGMA-001 |
| P16-WP1-T5 | Implement `IMdnsAdvertiser`: register `_ogma-library._tcp.local` service with name, port, CA fingerprint TXT record; deregister on stop | P16-WP1-T3 | 0.5 d | FR-LAN-003 |
| P16-WP1-T6 | Unit tests: `CertificateProvisioner_GeneratesValidX509Root`, `CertificateProvisioner_FingerprintStable`, `MdnsAdvertiser_RegistersAndDeregisters` | P16-WP1-T4, P16-WP1-T5 | 0.5 d | FR-LAN-002, FR-LAN-003 |
| P16-WP1-T7 | Update `SOURCE-SUMMARY.md` §F to note `LanHost` bounded context and ADR-0010 | P16-WP1-T2 | 0.25 d | Documentation |

---

## Work Package 2 — LanHost Bounded Context Scaffold

**Goal:** Establish the `LanHost` namespace, all interfaces, DI wiring, and
architecture isolation tests before any implementation lands.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP2-T1 | Create `src/OgmaLibrary.Application/LanHost/` with interface files: `ILibraryHostService`, `IClientSessionService`, `ICertificateProvisioner`, `IMdnsAdvertiser`, `IHostModeSettingsRepository` — all with XML doc comments | P16-WP1-T3 | 0.5 d | FR-LAN-001, bounded-context discipline |
| P16-WP2-T2 | Create `src/OgmaLibrary.Infrastructure/LanHost/` namespace; stub implementations; register in DI composition root | P16-WP2-T1 | 0.5 d | FR-LAN-001 |
| P16-WP2-T3 | Add architecture tests: `ArchTests_LanHost_HasNoCredentialStoreOrWorkerDependency`, `ArchTests_LanHost_HasNoAiProviderDependency`, `ArchTests_StandaloneMode_HasNoOpenListener` | P16-WP2-T2 | 0.5 d | FR-LAN-010, CI-2 (amended) |
| P16-WP2-T4 | CI pipeline: add architecture test step to both Windows and macOS runners | P16-WP2-T3 | 0.25 d | NFR-OGMA global |

---

## Work Package 3 — Catalogue Projection Endpoint

**Goal:** Authenticated HTTPS endpoint serving the book read-model to LAN clients.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP3-T1 | Define `CatalogueProjectionDto` (book identity, metadata summary, shelf memberships, cover asset URL, availability) — OpenAPI-style contract; keep backward-compatible for Phase 17 client | P16-WP2-T1 | 0.5 d | FR-LAN-005 |
| P16-WP3-T2 | Implement `ICatalogueProjectionService` adapter in `LanHost`: delegates to the `Library Catalogue` context via `Application` interfaces; applies field projection (no internal IDs leaked) | P16-WP3-T1 | 1 d | FR-LAN-005, bounded-context discipline |
| P16-WP3-T3 | Implement HTTPS route handler `GET /api/v1/catalogue` with pagination (`page`, `pageSize`, `sortBy`, `filterBy`) and auth middleware (session token Bearer) | P16-WP3-T2 | 1 d | FR-LAN-005 |
| P16-WP3-T4 | Implement `GET /api/v1/catalogue/{bookId}` single-book projection endpoint | P16-WP3-T3 | 0.25 d | FR-LAN-005 |
| P16-WP3-T5 | Implement `GET /api/v1/catalogue/search?q=` metadata search projection (delegates to `ISearchService` metadata path only — not FTS or embeddings in this phase) | P16-WP3-T4 | 0.5 d | FR-LAN-005 |
| P16-WP3-T6 | Integration tests: `CatalogueEndpoint_ReturnsPaginatedBooks_MatchingCatalogueState`, `CatalogueEndpoint_Returns401_WithoutToken`, `CatalogueEndpoint_SearchReturnsFilteredResults` | P16-WP3-T5 | 0.5 d | FR-LAN-005 |

---

## Work Package 4 — Asset Serving

**Goal:** Serve cover images, spine textures, and thumbnails over HTTPS with
proper caching.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP4-T1 | Implement `GET /api/v1/assets/cover/{bookId}`, `/spine/{bookId}`, `/thumb/{bookId}` — reads sidecar folder; validates path is within library root (CTRL-OGMA-008); sets `Cache-Control: max-age=86400` | P16-WP3-T3 | 0.75 d | FR-LAN-006, CTRL-OGMA-008..009 |
| P16-WP4-T2 | Byte-range (`Range:` header) support for large cover images | P16-WP4-T1 | 0.25 d | FR-LAN-006, NFR-LAN-001 |
| P16-WP4-T3 | Integration tests: `AssetEndpoint_ServesCoverMatchingSidecarHash`, `AssetEndpoint_Returns404_ForUnknownBookId`, `AssetEndpoint_RejectsPathTraversal` | P16-WP4-T2 | 0.5 d | FR-LAN-006, CTRL-OGMA-008 |

---

## Work Package 5 — Page-Render Mode

**Goal:** Host renders PDF pages to images and streams them; PDF bytes never
leave the Host.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP5-T1 | Implement `GET /api/v1/books/{bookId}/page/{pageNumber}` — calls `IPageRenderer.RenderPageAsync(bookId, pageNumber, resolution)` and streams resulting PNG/WebP bytes | P16-WP4-T1 | 1 d | FR-LAN-007 |
| P16-WP5-T2 | Concurrency limiter: max 10 simultaneous render requests (configurable); queued requests return `202 Accepted` with a polling URL or use Server-Sent Events for notification | P16-WP5-T1 | 0.5 d | NFR-LAN-002 |
| P16-WP5-T3 | Resolution parameter: clients may request `72dpi`, `150dpi`, `300dpi`; Host caps at `150dpi` unless admin overrides | P16-WP5-T1 | 0.25 d | NFR-LAN-002, NFR-OGMA-005 |
| P16-WP5-T4 | Integration tests: `PageRenderEndpoint_StreamsPngBytes_MatchingGoldenOracle`, `PageRenderEndpoint_NoPdfBytesInResponse`, `PageRenderEndpoint_EnforcesResolutionCap`, `PageRenderEndpoint_ConcurrencyLimiterQueues` | P16-WP5-T3 | 0.75 d | FR-LAN-007, NFR-LAN-002 |
| P16-WP5-T5 | Render-response audit: each render request → `AuditEvents` row (bookId, page, clientId, resolution, durationMs) | P16-WP5-T4 | 0.25 d | CTRL-OGMA-018 |

---

## Work Package 6 — File-Stream Mode

**Goal:** Optional raw PDF endpoint, disabled by default, gated behind admin
opt-in.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP6-T1 | Implement `GET /api/v1/books/{bookId}/file` — returns `403 Forbidden` with descriptive error unless `HostModeSettings.ContentMode == FileStream` | P16-WP5-T1 | 0.25 d | FR-LAN-008 |
| P16-WP6-T2 | When enabled: stream raw PDF bytes (range-request support); validate `bookId` is in catalogue (no path traversal); write audit entry noting `ContentMode=FileStream` | P16-WP6-T1 | 0.5 d | FR-LAN-008, CTRL-OGMA-008, CTRL-OGMA-018 |
| P16-WP6-T3 | Integration tests: `FileStreamEndpoint_Returns403_WhenDisabled`, `FileStreamEndpoint_StreamsPdfBytes_WhenEnabled`, `FileStreamEndpoint_WritesAuditEntry` | P16-WP6-T2 | 0.25 d | FR-LAN-008 |

---

## Work Package 7 — Authentication & Session Management

**Goal:** Authenticated HTTPS sessions using client certificates (issued at
enrollment in Phase 17) with session-token exchange; subnet validation.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP7-T1 | Implement `POST /api/v1/auth/session` — accepts client certificate (Phase 17 enrollment) or pre-shared enrollment code (Phase 16 bootstrap); returns signed session JWT with `profileId`, `role`, `exp` | P16-WP1-T4 | 1 d | FR-LAN-001, FR-LAN-002 |
| P16-WP7-T2 | Session token middleware: validate Bearer token on all protected routes; reject expired/revoked tokens with `401` | P16-WP7-T1 | 0.5 d | FR-LAN-001 |
| P16-WP7-T3 | Subnet validation: at listener level, accept connections only from RFC-1918 subnets (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16); log rejected attempts | P16-WP7-T2 | 0.25 d | CTRL-OGMA (Phase 19 CTRL-OGMA-020 scope — enforced here) |
| P16-WP7-T4 | `IClientSessionService.RevokeAllAsync()` — called on Host stop; all sessions invalidated | P16-WP7-T2 | 0.25 d | FR-LAN-001 |
| P16-WP7-T5 | Certificate TOFU: QR-code generation (encodes `ogma://host?addr=<ip>:<port>&fp=<sha256>`) displayed in Host UI | P16-WP7-T1 | 0.5 d | FR-LAN-002, FR-LAN-004 |
| P16-WP7-T6 | Unit tests: `SessionToken_IsExpired_ReturnsTrue`, `SubnetValidator_RejectsPublicIp`, `SubnetValidator_AcceptsRfc1918`, `SessionRevoke_AllTokensInvalidated` | P16-WP7-T5 | 0.5 d | FR-LAN-001..002 |
| P16-WP7-T7 | Integration test: `AuthFlow_CertificateHandshake_IssuesToken_EndToEnd` on both Windows and macOS | P16-WP7-T6 | 0.5 d | FR-LAN-001, CI cross-platform |

---

## Work Package 8 — Host Mode UI

**Goal:** Settings > Sharing panel; start/stop Host mode; status chip;
connected-client count; QR fingerprint display.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP8-T1 | Add "Sharing" section to Settings navigation; route to `SharingSettingsView.axaml` | P16-WP2-T2 | 0.25 d | FR-LAN-001 |
| P16-WP8-T2 | Host mode toggle (`ToggleSwitch`): bound to `ILibraryHostService`; shows confirmation dialog before enabling; ARIA role `switch`, label localized en/fr | P16-WP8-T1 | 0.5 d | FR-LAN-001 |
| P16-WP8-T3 | Status chip: `Stopped` (slate) / `Starting…` (clay) / `Running` (sage) / `Error` (clay); text + icon; `aria-live="polite"` | P16-WP8-T2 | 0.25 d | FR-LAN-001 |
| P16-WP8-T4 | Connected-client count display: `aria-live="polite"`; updates every 5 s while running; icon `ic_clients_connected` | P16-WP8-T3 | 0.25 d | FR-LAN-001 |
| P16-WP8-T5 | QR code panel: displays QR encoding the join URL + CA fingerprint; also shows fingerprint hex for manual verification; copy-to-clipboard button | P16-WP8-T4 | 0.5 d | FR-LAN-002, FR-LAN-004 |
| P16-WP8-T6 | Content delivery mode selector: `Radio` group `Page Render` (default) / `File Stream` (admin opt-in); File Stream selection shows a privacy notice before saving | P16-WP8-T5 | 0.25 d | FR-LAN-007, FR-LAN-008 |
| P16-WP8-T7 | i18n: add all string keys for this view to `Strings.en.resx` and `Strings.fr.resx`; pseudolocale check passes | P16-WP8-T6 | 0.25 d | I18N-STRATEGY, Phase DoD |
| P16-WP8-T8 | Accessibility walkthrough: keyboard-only navigation of entire Sharing settings panel; screen-reader audit; fix any contrast/focus issues | P16-WP8-T7 | 0.25 d | NFR-PROD-008, WCAG 2.2 AA |

---

## Work Package 9 — Audit Integration

**Goal:** Every authenticated LAN request produces an `AuditEvents` row.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP9-T1 | Middleware: `AuditMiddleware` wraps all `LanHost` routes; captures `clientId`, `profileId`, `method`, `resource`, `statusCode`, `durationMs`, `timestamp`; calls `IAuditService.RecordAsync()` | P16-WP7-T2 | 0.5 d | CTRL-OGMA-018 |
| P16-WP9-T2 | Integration test: `AuditMiddleware_WritesRowForEvery_AuthenticatedRequest`; assert 10 requests → 10 audit rows; assert ordering monotonic | P16-WP9-T1 | 0.25 d | CTRL-OGMA-018 |
| P16-WP9-T3 | Unauthenticated (rejected) requests: write a reduced audit row (no profileId, `statusCode=401`, `ipAddress`) to detect brute-force | P16-WP9-T2 | 0.25 d | CTRL-OGMA-018, Phase 19 threat model |

---

## Work Package 10 — Database Migration & Schema

**Goal:** EF Core migration for `HostClientSessions` and `HostModeSettings`.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP10-T1 | Add EF Core entity models: `HostClientSession`, `HostModeSettings` to `Infrastructure.LanHost.Data` | P16-WP2-T2 | 0.25 d | FR-LAN-001 |
| P16-WP10-T2 | Generate migration `M016_AddLanHostTables` (UP: create tables; DOWN: drop tables) | P16-WP10-T1 | 0.25 d | FR-LAN-001, NFR-PROD-012 |
| P16-WP10-T3 | Migration isolation test: apply M016 UP on clean DB; verify schema; apply DOWN; verify clean | P16-WP10-T2 | 0.25 d | NFR-PROD-012, R1 |
| P16-WP10-T4 | Implement `IHostModeSettingsRepository` with EF Core; default row seed (IsEnabled=false, Port=7473, ContentMode=PageRender) | P16-WP10-T3 | 0.25 d | FR-LAN-001 |

---

## Work Package 11 — Testing, CI & Platform Verification

**Goal:** Complete test suite; CI pipeline green on Windows and macOS.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P16-WP11-T1 | 20-client catalogue smoke load test: spin 20 concurrent `HttpClient` instances; assert P95 ≤ 800 ms over 100 requests per client | All WP3 done | 0.5 d | NFR-LAN-001 |
| P16-WP11-T2 | 10-client page-render smoke load test: 10 concurrent page-render requests; assert P95 first-byte ≤ 2 s | All WP5 done | 0.5 d | NFR-LAN-002 |
| P16-WP11-T3 | Golden-corpus regression: all 11 golden-corpus PDFs accessible via page-render endpoint; oracle images match pre-computed hashes | All WP5 done | 0.5 d | FR-LAN-007, quality |
| P16-WP11-T4 | Standalone mode regression: full golden-corpus suite passes with Host mode off; no `HttpListener` open | P16-WP2-T3 | 0.25 d | NFR-LAN-003, CI-2 (amended) |
| P16-WP11-T5 | macOS CI runner: verify mDNS advertisement, certificate provisioning to Keychain, HTTPS binding — all pass | All WPs | 0.5 d | Cross-platform mandate |
| P16-WP11-T6 | Windows CI runner: verify DPAPI credential store, mDNS, HTTPS — all pass | All WPs | 0.25 d | Cross-platform mandate |
| P16-WP11-T7 | `/security-review` on WP7 (auth) and WP5/WP6 (content delivery); resolve all findings | P16-WP7-T7, P16-WP6-T3 | 0.5 d | Phase DoD §9 |
| P16-WP11-T8 | `/code-review` on all WPs; resolve findings | All WPs | 0.5 d | Phase DoD §9 |
