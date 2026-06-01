# Phase 16 — LAN Library Server (Host Mode)

Introduce an opt-in "Library Host mode" that makes the central classroom computer
a deliberately-designed, authenticated, LAN-bounded inbound server, reconciling
SRS constraint CI-2 via ADR-0010.

---

## 1. Title & one-line mission

**Phase 16 — LAN Library Server (Host Mode)**
Turn a single Ogma installation into an authenticated, LAN-scoped library server
that serves catalogue projections, cover/spine assets, and page-rendered or
file-streamed PDF content to up to 40 concurrent classroom clients — without
compromising the local-first principles of every other installation.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Release tier** | V2 |
| **Estimate** | 4 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | New (LAN classroom expansion, PRD §10 owner delta) |
| **Platforms** | Windows 10/11 (WebView2) + macOS 13+ (WKWebView) |
| **Status** | In progress — WP1/WP2/WP10 scaffold and persistence started 2026-06-01 |
| **Depends on** | Phase 00 (ADR ratification, LAN transport spike), Phase 01 (LAN spike retired to ADR-0010), Phase 04-05 (catalogue + ingestion), Phase 08 (reader core), Phase 10-11 (search), Phase 12 (AI gateway) |
| **ADRs introduced** | ADR-0010 (proposed — see §7) |

---

## 3. Objectives

When this phase is done, all of the following are true:

1. An admin can start "Library Host mode" through an explicit opt-in UI gesture;
   the inbound HTTPS listener is active **only** while Host mode is running.
2. LAN clients can discover the Host via mDNS/DNS-SD (zero-config) or by typing
   a host address (manual fallback), and establish a mutually-authenticated HTTPS
   session within the trust model decided by the Phase 01 LAN spike.
3. The Host serves catalogue projections (full read-model), cover/spine/thumbnail
   assets, and either page-renders (Host renders, streams images) or file streams
   (client downloads and renders locally) — the mode is a per-deployment admin
   setting.
4. The Library Catalogue context remains the sole source of truth; clients are
   projection consumers and cannot write to book identity or shared curation
   except through explicitly authorized, audited use cases.
5. The Host's server surface is isolated from the OS credential store and from
   untrusted-PDF workers, exactly as those workers are isolated (CTRL-OGMA-005).
6. Capacity is validated: the Host sustains 20 concurrent catalogue-browsing
   clients and 10 concurrent page-render streaming clients within the NFR budgets
   defined in §5 (benchmarked definitively in Phase 20).
7. CI-2 is formally amended via ADR-0010; the amendment is traceable to every
   requirement it affects.

---

## 4. Scope

### In scope

- New bounded context: **Library Sharing / Host** (`OgmaLibrary.LanHost` project
  or namespace within `Infrastructure`), owning all inbound listener logic.
- Host-mode opt-in: UI toggle in Settings > Sharing, explicit admin start/stop,
  status indicator showing connected client count.
- Transport: HTTPS over LAN. Self-signed root CA provisioned at first Host-mode
  start; trust-pinning mechanism for clients (QR code + manual certificate
  fingerprint copy). Mutual authentication optional — exact scheme per ADR-0010
  (resolved from Phase 01 LAN spike).
- Discovery: mDNS/DNS-SD registration (`_ogma-library._tcp.local`) on start;
  manual host-address entry UI on client side.
- Catalogue projection endpoint: serves the same read-model the grid/list/3D
  consume (book identity, metadata, shelf membership, availability, cover asset
  URLs) as JSON over HTTPS, paginated, filterable.
- Asset serving: cover images, spine textures, and thumbnails via `ogma://`-
  equivalent HTTPS asset endpoint. Cache-control headers for client caching.
- Content delivery mode (admin setting per library):
  - **Page-render mode** (default, privacy-preserving): Host renders PDF pages to
    images via the existing PDFium pipeline and streams images; PDF bytes never
    leave the Host.
  - **File-stream mode** (admin opt-in): raw PDF file streamed to client; client
    renders locally. Requires content-serving trust justification in DPIA.
- Authentication: client sessions authenticated with session tokens issued by the
  Host after certificate handshake; token scope includes the requesting client's
  identity (enrolled profile, established in Phase 17).
- Audit: every Host-served request is written to `AuditEvents` with client
  identity, resource, action, timestamp (CTRL-OGMA-018).
- Isolation: `LanHost` bounded context has no compile-time dependency on
  `CredentialStore` or `Workers.UntrustedPdf`; architecture tests enforce this.
- CI-2 amendment: ADR-0010 authored and linked from every affected requirement.
- Win + macOS: mDNS via `Zeroconf`/`Manatee.Dns.ServiceDiscovery` or equivalent
  cross-platform library; HTTPS via `System.Net.HttpListener` or Kestrel (spike
  decides — ADR-0010); platform-specific certificate store integration.

### Explicitly out of scope

- Client-side connection and browsing UI (Phase 17).
- Student profiles, per-student private state, and sync (Phase 17).
- Admin console and school-managed AI (Phase 18).
- Cloud sync or internet-facing exposure of the Host.
- Linux platform (CON-6 gap — not an MVP/V1/V2 gate).
- Performance benchmarking at full 40-client load (Phase 20).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-LAN-001 | V2 | Admin can enable/disable Library Host mode explicitly | Integration test: toggle ON → `HttpListener` binds; toggle OFF → port released |
| FR-LAN-002 | V2 | Host generates and provisions a self-signed root CA on first start | Unit test: `CertificateProvisioner` creates valid X.509 root; fingerprint matches stored value |
| FR-LAN-003 | V2 | Clients discover Host via mDNS/DNS-SD | Integration test: mDNS service advertised; test client resolves `_ogma-library._tcp.local` |
| FR-LAN-004 | V2 | Manual host-address entry as fallback | Integration test: client configured with IP:port connects and authenticates |
| FR-LAN-005 | V2 | Catalogue projection endpoint returns full read-model | Integration test: GET `/api/v1/catalogue` returns books matching catalogue state; pagination correct |
| FR-LAN-006 | V2 | Cover/spine/thumbnail asset endpoint served | Integration test: asset HTTPS request returns correct bytes matching sidecar hash |
| FR-LAN-007 | V2 | Page-render mode: Host renders page images; PDF bytes do not leave Host | Audit test: no PDF file bytes in network responses; rendered image matches golden oracle page |
| FR-LAN-008 | V2 | File-stream mode: raw PDF served only when admin has opted in | Integration test: file-stream disabled by default; opt-in allows raw PDF endpoint |
| FR-LAN-009 | V2 | All LAN requests produce `AuditEvents` records | Integration test: 10 requests → 10 audit rows with correct client identity and resource |
| FR-LAN-010 | V2 | LanHost context has no dependency on CredentialStore or UntrustedPdf workers | Architecture test: `ArchTests_LanHost_HasNoCredentialStoreOrWorkerDependency` passes |
| NFR-LAN-001 | V2 | 20 concurrent catalogue clients, P95 response ≤ 800 ms on reference hardware | Load test fixture: 20 concurrent GET `/api/v1/catalogue`; P95 latency measured |
| NFR-LAN-002 | V2 | 10 concurrent page-render streams, P95 first-image ≤ 2 s on reference hardware | Load test: 10 simultaneous page-render requests; first PNG byte P95 ≤ 2 s |
| NFR-LAN-003 | V2 | Host-mode toggle does not disrupt the Standalone user's local catalogue access | Regression test: local grid/reader/search functions while Host mode is running |
| CTRL-OGMA-018 | V2 | All off-device / LAN-served access logged to tamper-evident audit trail | Audit-trail integration test: entries present, ordering monotonic, no gap |
| CI-2 (amended) | V2 | Inbound listener is opt-in, LAN-bounded, admin-started only — CI-2 scope amended | ADR-0010 authored; architecture test: no `HttpListener`/`Kestrel` binding in Standalone mode |

---

## 6. Dependencies

### Depends on

- **Phase 00**: ADR-0010 proposed; LAN transport question assigned to Phase 01
  spike; jurisdiction/DPIA gap noted.
- **Phase 01**: LAN transport spike — retires the transport choice (Kestrel vs
  `HttpListener`, mDNS library, certificate strategy) into ADR-0010.
- **Phase 02**: solution structure; `Infrastructure` project for the `LanHost`
  context.
- **Phase 04-05**: `ICatalogueProjectionService` and sidecar asset paths stable;
  this phase consumes them without modification.
- **Phase 08**: PDFium render pipeline (`IPageRenderer`) available for page-render
  mode.
- **Phase 10-11**: Search index projected through catalogue context (search on
  Host is out of scope here but index must be non-blocking).
- **Phase 12**: `IAiProvider` gateway pattern understood; Host's AI egress not
  wired in this phase (Phase 18).

### Unblocks

- **Phase 17**: Client / Classroom Mode — requires the Host endpoints defined here.
- **Phase 18**: School Admin — requires Host authentication and audit infrastructure.
- **Phase 19**: Security Hardening — the Host inbound surface is the highest-risk
  new asset; its threat model is owned in Phase 19 but the surface is built here.
- **Phase 20**: LAN load benchmarks at 40 clients.

---

## 7. Architecture & approach

### ADR-0010 (proposed)

**Title:** Opt-in Library Host mode amends CI-2; LAN server surface, transport,
and isolation.

**Context:** SRS CI-2 states the application opens no inbound network listener.
The classroom vision requires exactly such a listener on the Host machine. The
tension is resolved by scoping CI-2 to the Standalone mode: the default product
remains inbound-listener-free. Library Host mode is an explicit, audited opt-in
that introduces a deliberately-designed, LAN-bounded server surface.

**Decision:**

1. CI-2 is amended to: *"In Standalone mode, the application opens no inbound
   network listener. In Library Host mode (opt-in, admin-started), a single HTTPS
   listener on a LAN-local port is permitted, bounded by the controls in
   ADR-0010."*
2. Transport: HTTPS (TLS 1.2+) using a Host-generated self-signed root CA. Clients
   trust-pin the Host CA fingerprint on first connection (TOFU with QR-code
   delivery). Mutual TLS is the authenticated mode; client certificates issued by
   the Host CA at enrollment (Phase 17). Exact implementation (Kestrel embedded
   vs. `System.Net.HttpListener`) is resolved by the Phase 01 LAN spike.
3. Discovery: mDNS/DNS-SD (`_ogma-library._tcp.local`) on Host start; the service
   record includes the Host name, port, and CA fingerprint hint. Manual IP entry
   is always available.
4. Isolation: the `LanHost` bounded context owns the inbound surface. It depends
   on `ICatalogueProjectionService` and `IPageRenderer` through application-layer
   interfaces; it has **no** compile-time dependency on `CredentialStore`,
   `UntrustedPdfWorker`, or `IAiProvider`. Architecture tests enforce this.
5. Content delivery: page-render mode is the default (PDF bytes stay on Host).
   File-stream mode is an admin opt-in recorded in the audit trail.
6. All authenticated LAN requests are written to `AuditEvents`
   (CTRL-OGMA-018).

**Consequences:**

- CI-2 violation is retired from the risk register for Host mode.
- A new risk is registered: Host inbound surface is the highest-risk new asset;
  full threat model is Phase 19's responsibility.
- Standalone users: zero change.

**Status:** Proposed 2026-05-30. Ratify in Phase 00 / Phase 16 start.

---

### Bounded context: Library Sharing / Host

Location: `OgmaLibrary.Infrastructure.LanHost` (namespace partition within
`Infrastructure`; may be promoted to a separate project if size warrants —
recorded as a task in Phase 16).

Interfaces consumed (from `Application` layer):
- `ICatalogueProjectionService` — read-model queries (books, shelves, covers).
- `IPageRenderer` — PDFium page render to PNG/WebP bytes.
- `IAuditService` — write `AuditEvents`.
- `IHostModeSettingsRepository` — read/write Host mode configuration.

Interfaces owned (exposed to `App`/DI):
- `ILibraryHostService` — start/stop, status, connected-client count.
- `IClientSessionService` — issue/revoke session tokens.
- `ICertificateProvisioner` — generate/load Host CA + leaf certificates.
- `IMdnsAdvertiser` — register/deregister mDNS service record.

### API surface (HTTPS, LAN-local)

```
GET  /api/v1/catalogue          — paginated book projection list
GET  /api/v1/catalogue/{bookId} — single book projection
GET  /api/v1/assets/cover/{id}  — cover image (HTTPS, cache-control)
GET  /api/v1/assets/spine/{id}  — spine texture
GET  /api/v1/assets/thumb/{id}  — thumbnail
GET  /api/v1/books/{id}/page/{n}— page render (page-render mode)
GET  /api/v1/books/{id}/file    — file stream (admin opt-in only)
GET  /api/v1/catalogue/search   — metadata search projection (Phase 17 client use)
POST /api/v1/auth/session       — exchange client cert for session token
```

All endpoints require a valid session token (Bearer) except `/auth/session`.
Unauthenticated requests receive HTTP 401. Non-LAN IP ranges are rejected at
the listener level (subnet validation).

### Cross-platform notes

| Concern | Windows | macOS |
| --- | --- | --- |
| HTTPS listener | Kestrel (System.Net) or HttpListener — ADR-0010 | Same; no App Transport Security issue (LAN only, self-signed pinned) |
| mDNS library | `Manatee.Dns` or `ZeroconfSharp` | Same cross-platform library; macOS Bonjour coexists |
| Certificate store | DPAPI-backed file store for CA private key | macOS Keychain via `SecKeychain` API |
| Network interface enumeration | `NetworkInterface.GetAllNetworkInterfaces()` | Same .NET API; filter by `OperationalStatus.Up` + not loopback |

### Data / schema changes

New tables (in the Host's local SQLite catalogue):

```sql
-- Client session tokens (scoped to Host catalogue)
CREATE TABLE HostClientSessions (
    Id          TEXT PRIMARY KEY,
    ProfileId   TEXT NOT NULL,          -- FK to Profiles (Phase 17)
    IssuedAt    TEXT NOT NULL,
    ExpiresAt   TEXT NOT NULL,
    RevokedAt   TEXT,
    IpAddress   TEXT NOT NULL
);

-- Host configuration (singleton row)
CREATE TABLE HostModeSettings (
    Id              INTEGER PRIMARY KEY CHECK (Id = 1),
    IsEnabled       INTEGER NOT NULL DEFAULT 0,
    Port            INTEGER NOT NULL DEFAULT 7473,
    ContentMode     TEXT NOT NULL DEFAULT 'PageRender', -- 'PageRender'|'FileStream'
    CaFingerprintHex TEXT,
    UpdatedAt       TEXT NOT NULL
);
```

New migration: `M016_AddLanHostTables`. Reversible (DOWN drops both tables).

---

## 8. Work breakdown (summary)

Full task detail in `tasks.md`.

| Work package | Key tasks | Est. |
| --- | --- | --- |
| **WP1 — ADR-0010 & transport spike integration** | Finalize ADR-0010 from Phase 01 spike; wire Kestrel/HttpListener choice; certificate provisioner; mDNS advertiser | 5 d |
| **WP2 — LanHost bounded context scaffold** | Project/namespace; interfaces; DI wiring; architecture tests for isolation | 2 d |
| **WP3 — Catalogue projection endpoint** | REST handler; `ICatalogueProjectionService` adapter; pagination; auth middleware | 3 d |
| **WP4 — Asset serving** | Cover/spine/thumb HTTPS endpoint; cache-control; byte-range support | 2 d |
| **WP5 — Page-render mode** | PDF page render via `IPageRenderer`; streaming response; concurrency limiter | 3 d |
| **WP6 — File-stream mode** | Raw PDF endpoint (gated by admin setting); DPIA note; audit entry | 1 d |
| **WP7 — Authentication & session management** | Certificate TOFU flow; session token issue/revoke; subnet validation | 3 d |
| **WP8 — Host mode UI** | Settings > Sharing toggle; status chip; connected-client count; QR fingerprint | 2 d |
| **WP9 — Audit integration** | All requests → `AuditEvents`; audit test assertions | 1 d |
| **WP10 — DB migration & schema** | `M016_AddLanHostTables`; UP/DOWN; EF Core model | 1 d |
| **WP11 — Testing & CI** | Unit, integration, architecture, load-smoke tests; CI pipeline | 3 d |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons + manifest**: `icons.md` defines `ic_host_sharing`,
  `ic_network_lan`, `ic_clients_connected`, `ic_publish_folder`,
  `ic_host_start`, `ic_host_stop`, `ic_certificate`, `ic_qr_fingerprint`
  — all `⬜ to procure`.
- [x] **i18n (en/fr)**: all UI strings in Settings > Sharing, status chips,
  error messages, QR-flow prompts externalized to `.resx`; `fr` translations in
  same PR; pseudolocale check in CI.
- [x] **Accessibility**: Host mode toggle has keyboard focus, ARIA role
  `switch`, label `"Library Host Mode"` (en) / `"Mode hôte de bibliothèque"` (fr);
  status chip is not color-only (text label + icon); connected-client count has
  `aria-live="polite"`.
- [x] **Privacy/egress**: no off-device egress in this phase; all traffic is
  LAN-only; subnet validation enforced; file-stream mode off by default; audit
  trail written for every request (CTRL-OGMA-018).
- [x] **Reversibility**: Host mode stop releases the port and revokes all active
  sessions; no irreversible state; `HostModeSettings` migration is reversible.
- [x] **Performance budgets**: NFR-LAN-001/002 instrumented; smoke load test
  asserts 20 catalogue clients ≤ 800 ms P95; full 40-client benchmark in Phase 20.
- [x] **Bounded-context tests**: `ArchTests_LanHost_HasNoCredentialStoreOrWorkerDependency`
  and `ArchTests_LanHost_HasNoAiProviderDependency` added to architecture test suite.
- [x] **Documentation**: ADR-0010 authored; XML doc comments on all public
  interfaces; `CLAUDE.md` updated; `SOURCE-SUMMARY.md` §F amended to note the
  new bounded context.

---

## 10. Definition of Done

### Global DoD (from `CONVENTIONS.md`)

- [ ] Every in-scope FR/NFR/CTRL ID (FR-LAN-001..010, NFR-LAN-001..003,
      CTRL-OGMA-018) has a passing test or a tagged gap.
- [ ] Golden-corpus suite green; no open R1/R2 defect.
- [ ] `dotnet format --verify-no-changes`, `dotnet build` (warnings = errors),
      `dotnet test`, and architecture tests all pass.
- [ ] Builds and tests pass on **both Windows and macOS** CI runners.
- [ ] New user strings externalized and present in **en + fr**; pseudolocale CI
      check passes.
- [ ] Every new control has a colorful icon **and** an accessible label;
      keyboard + screen-reader walkthrough passes; `icons.md` complete.
- [ ] ADRs/decisions recorded; reference docs updated; hybrid validation gate
      passes where applicable.
- [ ] Performance budgets touched are instrumented and within budget (or trend).
- [ ] `/code-review` and `/security-review` done; findings resolved.

### Phase-16-specific exit criteria

- [ ] ADR-0010 is authored, CI-2 scope amendment is explicit, and the ADR is
      cross-referenced in `SOURCE-SUMMARY.md`.
- [ ] `ILibraryHostService.StartAsync()` binds HTTPS listener; `StopAsync()`
      releases port; verified by integration test on Windows and macOS.
- [ ] mDNS service record is discoverable by a test client on the same subnet;
      manual IP fallback also verified.
- [ ] Catalogue projection endpoint returns correct pagination and matches
      catalogue state (deterministic oracle test).
- [ ] Page-render mode verified: no PDF file bytes appear in any HTTP response
      body (assertion on response content-type and body for `/books/{id}/file`
      returning 403 when file-stream disabled).
- [ ] File-stream mode requires explicit admin opt-in; default state is
      `ContentMode = PageRender`.
- [ ] 20-concurrent-client smoke load test passes: P95 catalogue response ≤ 800 ms.
- [ ] Architecture isolation tests pass: `LanHost` context has no compile-time
      dependency on `CredentialStore`, `UntrustedPdfWorker`, or `IAiProvider`.
- [ ] All authenticated LAN requests produce `AuditEvents` rows; verified by
      integration test.
- [ ] `M016_AddLanHostTables` UP and DOWN migrations both succeed in isolation.
- [ ] Standalone mode: no `HttpListener`/Kestrel binding present when Host mode
      is off (verified by architecture test + integration test asserting no open
      port after cold start without enabling Host mode).

---

## 11. Skills to use

Full guidance in `skills.md`. Key skills for this phase:

- `architecture:system-architecture-design` — design the `LanHost` bounded
  context and interface contracts (WP1-WP2).
- `architecture:realtime-systems` — concurrency limiter and streaming response
  design for page-render mode (WP5).
- `security:network-security` — transport hardening, subnet validation,
  certificate TOFU flow (WP7).
- `documentation-generation:architecture-decision-records` — author ADR-0010
  (WP1).
- `superpowers:test-driven-development` — write architecture tests and
  integration tests before implementation (WP2, WP11).
- `/security-review` — review WP7 (auth) and WP5/WP6 (content delivery) before
  merge.
- `frontend-design:frontend-design` — Host mode settings UI (WP8).
- `devops-cloud:reliability-engineering` — concurrency limits and graceful
  Host shutdown (WP5, WP9).

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| ADR-0010 | `docs/architecture/adr-0010-lan-host-mode.md` |
| `OgmaLibrary.Infrastructure.LanHost` namespace | `src/OgmaLibrary.Infrastructure/LanHost/` |
| `ICertificateProvisioner`, `ILibraryHostService`, `IClientSessionService`, `IMdnsAdvertiser` | `src/OgmaLibrary.Application/LanHost/` |
| `M016_AddLanHostTables` migration | `src/OgmaLibrary.Infrastructure/Migrations/` |
| Architecture isolation tests | `src/OgmaLibrary.Tests/Architecture/LanHostIsolationTests.cs` |
| Integration tests (catalogue, assets, page-render, auth, audit) | `src/OgmaLibrary.Tests/Integration/LanHost/` |
| Load smoke test (20 clients) | `src/OgmaLibrary.Tests/Performance/LanHostSmokeTest.cs` |
| Host mode UI (Settings > Sharing) | `src/OgmaLibrary.App/Views/Settings/SharingSettingsView.axaml` |
| `icons.md` (this phase) | `docs/plans/grand-plan/phase-16/icons.md` |
| `SOURCE-SUMMARY.md` §F update | `docs/plans/grand-plan/SOURCE-SUMMARY.md` |

---

## 13. Risks

| Risk | R-tier | Mitigation |
| --- | --- | --- |
| mDNS library cross-platform reliability on restricted school networks | R3 | Spike in Phase 01; manual IP fallback always available; document firewall/mDNS requirements for admins |
| Certificate TOFU is vulnerable to MITM on first connection | R2 | QR-code delivery of fingerprint; warn admin if fingerprint changes; mutual TLS after enrollment |
| Page-render mode throughput insufficient for 40 clients on a low-end desktop | R3 | Concurrency limiter; render queue; graceful degradation to lower resolution; benchmarked in Phase 20 |
| Host mode accidentally enabled (listener open unexpectedly) | R2 | Toggle requires admin confirmation dialog; no auto-start on launch unless explicitly configured; CI test verifies no listener in Standalone mode |
| File-stream mode leaks PDF off Host without DPIA justification | R2 | Off by default; admin opt-in writes audit entry; DPIA note in Phase 19 |
| Schema migration failure on upgrade | R1 | UP/DOWN migration isolated test; backup before migration (CTRL-OGMA-014 pattern) |

---

## 14. Owner asks

1. **ADR-0010 sign-off**: Peter must explicitly ratify the CI-2 amendment text in
   ADR-0010 before Phase 16 build begins. This is a baselined-requirement change.
2. **Icon procurement**: Please procure the following premium PNG icons in the
   agreed Ogma style/color tokens (see `icons.md` for full manifest):
   `ic_host_sharing`, `ic_network_lan`, `ic_clients_connected`,
   `ic_publish_folder`, `ic_host_start`, `ic_host_stop`,
   `ic_certificate`, `ic_qr_fingerprint`.
   Sizes: 16/24/32/48 px @1x, @2x, @3x. Light + dark variants.
3. **Content delivery mode default**: confirm that page-render mode (PDFs never
   leave Host) is the correct school default, and whether file-stream mode should
   be surfaced in admin settings or hidden until Phase 18.
4. **Port number**: confirm the default LAN port (proposed: 7473). Advise if a
   well-known or IANA-registered port is preferred.
5. **LAN transport spike outcome (from Phase 01)**: confirm the Phase 01 spike
   result (Kestrel vs `HttpListener`; mDNS library choice) is signed off before
   WP1 begins.

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Planning agent | Initial v1.0 draft |
| 2026-06-01 | Implementation | Started WP1/WP2: added LanHost application contracts, no-listener infrastructure scaffold, DI registration, session revocation behavior, and architecture guardrails for listener/credential/worker/AI isolation. |
| 2026-06-01 | Implementation | Advanced WP2/WP10: added EF-backed Host-mode settings and client-session persistence, generated `20260601184330_Phase16LanHostTables`, persisted only session token hashes, and added migration/session/settings tests. |
| 2026-06-01 | Implementation | Advanced WP1: replaced the deterministic certificate stub with `LocalCertificateProvisioner`, generating a real self-signed X.509 Host CA with stable SHA-256 fingerprint reload and Windows DPAPI-protected PFX storage. |
| 2026-06-01 | Implementation | Advanced WP1: replaced the no-op mDNS scaffold with `MdnsAdvertiser`, wrapping `Makaretu.Dns.Multicast` behind `IMdnsAdvertiser` with DNS-SD service/TXT validation and registration lifecycle tests. |
| 2026-06-01 | Implementation | Advanced WP3/WP7: added `KestrelHostModeListener` for opt-in loopback HTTPS health, session issue, and authenticated catalogue projection endpoints; unauthenticated catalogue requests now return `401`. |
| 2026-06-01 | Implementation | Advanced WP9: added `LanHostRequestServed` audit rows for Host requests, including unauthorized catalogue access and authenticated catalogue projection, without writing raw bearer tokens. |
| 2026-06-01 | Implementation | Advanced WP4: added authenticated cover/spine/thumbnail sidecar asset endpoint with SHA-256 hash validation and malformed asset rejection before file I/O. |
| 2026-06-01 | Implementation | Advanced WP6 guardrail: added `/api/v1/books/{bookId}/file` default `403` behavior in page-render mode so raw PDF bytes do not leave the Host unless file-stream mode is explicitly implemented and enabled. |
| 2026-06-01 | Implementation | Advanced WP6: implemented explicit FileStream-mode PDF streaming with catalogue-backed path resolution, traversal/rooted-path protection, missing/unavailable file rejection, range-enabled responses, and resolver/endpoint tests. |
| 2026-06-01 | Implementation | Advanced WP3: hardened the catalogue projection contract with bounded `page`/`pageSize` response metadata, `hasMore`, optional shelf filtering, and authenticated single-book detail lookup. |
| 2026-06-01 | Implementation | Advanced WP3: added authenticated `/api/v1/catalogue/search` metadata search projection using the existing `IMetadataSearchService` application seam and bounded LAN result size. |
| 2026-06-01 | Implementation | Advanced WP5: added authenticated page-render endpoint that resolves catalogue PDFs, renders 1-based page requests to PNG bytes, clamps render width, and returns `403` when FileStream mode is active. |
| 2026-06-01 | Implementation | Advanced WP7/network boundary: added LAN bind-address selection that prefers active RFC1918 IPv4 adapters, falls back to loopback, and publishes the selected address in the mDNS TXT record. |
