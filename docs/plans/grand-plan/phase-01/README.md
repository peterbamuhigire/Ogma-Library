# Phase 01 — Risk Spikes & Technical Proof

One sentence: Run time-boxed, throwaway engineering spikes on every
technically-uncertain decision so every ADR is backed by measured evidence
before a line of production architecture is written.

---

## 1. Status & metadata

| Field | Value |
| --- | --- |
| **Status** | Not started |
| **Tier** | MVP (spikes gate the MVP architecture choices) |
| **Estimate** | 2 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD Phase 0 (spikes) + beta gates G1 (WebView bridge) and G2 (PDFium wrapper) |
| **Platforms** | All spikes run on **both Windows 10+ (WebView2) and macOS 13+ (WKWebView)**; platform-specific failures are first-class results, not edge cases |
| **Depends on** | Phase 00 complete (ADRs ratified, reference hardware confirmed, CON-2 minimums set) |

---

## 2. Objectives

1. The .NET 10 dependency matrix is confirmed: all 9 planned project libraries
   (Avalonia, EF Core, PDFium wrapper, SkiaSharp, PdfPig, Tesseract, Velopack,
   and the AI gateway client) resolve cleanly on .NET 10 on both platforms with
   no blocking incompatibilities.
2. ADR-0004 is amended with measured results from a two-wrapper PDFium benchmark
   (throughput ≥ NFR-OGMA-005 baseline; memory within acceptable bounds; license
   compatibility with Mac App Store + Windows Store confirmed).
3. ADR-0003 is confirmed or amended: WebGL2 renders on macOS 13+ WKWebView at
   ≥ 60 FPS for a 500-spine scene (NFR-OGMA-006); fallback grid confirmed
   available if WebGL2 is absent.
4. The WebView2↔C# and WKWebView↔C# bridge (typed JSON message passing) is
   validated on both platforms; the typed bridge contract is defined for the
   Phase 14 3D shelf.
5. FTS5 external-content indexing in SQLite is validated: a synthetic 2,000-book
   corpus returns full-text results within NFR-OGMA-004 (≤ 500 ms P95 warm) on
   the reference hardware.
6. The AI gateway spike confirms the provider-neutral `IAiProvider` interface
   can route to at least two providers (OpenAI-compatible and Ollama local) with
   a measurable round-trip latency baseline.
7. The LAN transport spike (pulled forward from Phase 16) validates the
   HTTPS-over-LAN + mDNS discovery approach for ADR-0010; transport choice is
   recorded as a measured ADR amendment.
8. Every spike is retired: results are committed as ADR amendments or new
   measurement records; no spike code is promoted to production without Phase 02
   review.

---

## 3. Scope

### In scope

- **Spike 1: .NET 10 dependency matrix** — create a minimal .csproj per library,
  confirm package resolution on .NET 10 on Windows and macOS CI runners;
  record any version pins or workarounds required.
- **Spike 2: PDFium wrapper benchmark** — implement the same render operation
  with two candidate wrappers; measure render throughput (pages/second),
  peak memory per PDF, and confirm the adapter-pattern interface is feasible.
  Amend ADR-0004 with the winning wrapper and measured results.
- **Spike 3: WebView↔JS bridge** — build a minimal Avalonia app with an
  embedded WebView (WebView2 on Windows, WKWebView on macOS) and a Three.js
  scene; validate bidirectional typed message passing; record the bridge
  contract skeleton.
- **Spike 4: 3D macOS WKWebView WebGL2** — run a Three.js scene with 500 plane
  geometries (spine textures) in WKWebView on macOS 13+; measure FPS and GPU
  memory; confirm the ADR-0003 gate (≥ 60 FPS) or record the counter-evidence
  that triggers an ADR-0003 amendment.
- **Spike 5: FTS5 indexing** — build a SQLite FTS5 external-content table over
  a synthetic 2,000-book text corpus; run representative queries; measure P95
  latency on the reference Windows and macOS hardware.
- **Spike 6: AI gateway** — implement a minimal `IAiProvider` interface with
  two concrete implementations (OpenAI-compatible HTTP client and Ollama local
  client); run a representative prompt; measure round-trip latency; confirm the
  privacy-tier routing logic is implementable with the chosen interface.
- **Spike 7: LAN transport** — stand up a minimal .NET 10 Kestrel HTTPS server
  on the LAN; implement mDNS/DNS-SD discovery (using a .NET DNS-SD library or
  Avahi/Bonjour); confirm a client on the same LAN can discover and connect;
  measure connection time and throughput for serving a 10 MB PDF page stream.
  Record the transport choice as ADR-0010 amendment (or new ADR-0010 subsection).
- ADR amendments for ADR-0003, ADR-0004 (required); ADR-0010 transport section
  (required); ADR-0001 (if any .NET 10 blocker found — not expected).
- Spike code committed to `spikes/` directory (throwaway; not in
  `src/`; not covered by architecture tests; explicitly excluded from the
  production `.editorconfig` rules).
- A `spikes/RESULTS.md` document summarizing all spike outcomes, measurements,
  go/no-go decisions, and the ADR amendment status.

### Explicitly out of scope

- Promoting any spike code to production `src/`. Spike code is **throwaway**.
- Full PDFium integration (Phase 08), full 3D bookshelf (Phase 14), full LAN
  host (Phase 16).
- UI design, icons, or i18n work (Phase 03).
- Any database schema creation (Phase 04).
- Writing architecture tests (Phase 02).
- Benchmarking against the full golden corpus (Phase 02 harness is not yet built).

---

## 4. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| ADR-0001 | MVP | .NET 10 LTS dependency matrix confirmed | Spike 1 pass; `spikes/RESULTS.md` §S1 |
| ADR-0003 | MVP | WebView Three.js 3D + WebGL2 on macOS WKWebView | Spike 4 measured FPS ≥ 60 on reference hardware; ADR-0003 amended |
| ADR-0004 | MVP | PDFium wrapper benchmark (2 wrappers) | Spike 2 measured results; ADR-0004 amended with winning wrapper |
| ADR-0010 | V2 (LAN) | LAN transport choice (HTTPS + mDNS) | Spike 7 measured results; ADR-0010 transport section completed |
| FR-CAT-001 | MVP | 3D shelf view technically feasible on both platforms | Spike 3 + 4 bridge and render confirmed |
| NFR-OGMA-004 | V1 | Full-text search ≤ 500 ms P95 warm (FTS5) | Spike 5 P95 latency measurement on reference hardware |
| NFR-OGMA-005 | MVP | Page turn ≤ 100 ms P95 cached (PDFium) | Spike 2 render throughput measurement |
| NFR-OGMA-006 | MVP | 3D ≥ 60 FPS (500 books) | Spike 4 FPS measurement on macOS 13+ WKWebView |
| NFR-PROD-005 | MVP | No UI stall > 100 ms (bridge latency) | Spike 3 bridge round-trip measurement |
| FR-AI-001/002 | MVP | AI gateway provider-neutral interface feasible | Spike 6 two-provider implementation confirmed |
| FR-SEARCH-002 | V1 | FTS5 index feasibility | Spike 5 confirmed |
| LAN-CLASS §3 transport | V2 | HTTPS-over-LAN + mDNS discovery | Spike 7 confirmed |
| G1 | MVP | WebView bridge beta gate (evidence) | Spike 3 typed bridge contract defined |
| G2 | MVP | PDFium wrapper beta gate (evidence) | Spike 2 adapter interface confirmed |

---

## 5. Dependencies

### Depends on

- Phase 00 complete:
  - ADR-0001..ADR-0009 ratified (Spikes need to know which decisions to stress-test).
  - ADR-0010 drafted as Proposed (LAN spike needs the CI-2 amendment context).
  - Reference hardware confirmed (CON-1) so spike measurements are meaningful.
  - .NET 10 confirmed as runtime (OQ-01) so Spike 1 has a clear target.
  - PDFium wrapper candidates identified (OQ-02/ADR-0004) so Spike 2 has two
    concrete wrappers to benchmark.

### Unblocks

- Phase 02: the solution scaffolding can begin only when the dependency matrix
  (Spike 1) is confirmed — so `Directory.Build.props` pins the correct package
  versions from the start.
- Phase 03: the typed WebView bridge contract (Spike 3) is the foundation of the
  Avalonia WebView hosting pattern used in Phase 14 but prototyped in Phase 03.
- Phase 14 (3D Bookshelf): depends on Spike 3 + 4 results for WebView bridge
  and macOS 3D feasibility.
- Phase 16 (LAN Host): depends on Spike 7 for the transport and discovery
  architecture.
- Any phase using PDFium: depends on Spike 2's ADR-0004 amendment for the
  winning wrapper.

---

## 6. Architecture & approach

### Spike isolation

All spike code lives in `spikes/<spike-id>/` at the repo root. Each spike is:

- A minimal, self-contained .NET 10 console app or Avalonia test harness.
- Excluded from the `src/` directory and from the production solution file.
- Excluded from architecture tests (Phase 02 will add `[assembly: InternalsVisibleTo]`
  exclusions for spike assemblies).
- Committed to `feature/phase-01-spikes` branch and merged as a single PR
  that also includes `spikes/RESULTS.md` and the ADR amendments.

### Spike 1: .NET 10 dependency matrix

Create a `spikes/s01-dotnet-matrix/` directory with one minimal .csproj per
planned library. Libraries to validate:

| Library | Role | Known risk |
| --- | --- | --- |
| `Avalonia` 11.x | Shell | .NET 10 RC support timing |
| `Microsoft.EntityFrameworkCore.Sqlite` 9.x | ORM | EF 9 → net10 compat |
| PDFium wrapper (candidate A) | PDF render | TBD by spike 2 |
| PDFium wrapper (candidate B) | PDF render | TBD by spike 2 |
| `PdfPig` | Text/metadata extraction | net8 TFM, net10 compat |
| `SkiaSharp` | Thumbnails/spines | native libs on macOS arm64 |
| `Tesseract` .NET binding | OCR | native tesseract libs on both platforms |
| `Velopack` | Auto-update | macOS notarization compat |
| OpenAI-compatible HTTP client | AI gateway | net10 compat |
| Ollama .NET client (or raw HttpClient) | AI gateway local | simple; low risk |
| `Microsoft.Data.Sqlite` | SQLite direct | bundled native libs |

Pass criterion: `dotnet restore` and `dotnet build` succeed with no
`NU1202` (package incompatible with framework) errors on both platforms.

### Spike 2: PDFium wrapper benchmark

Candidate wrappers (chosen based on OQ-02 answer in Phase 00; placeholders below
— confirm actual candidates in decisions.md):

- **Candidate A:** PdfiumViewer-based wrapper (e.g. `PdfiumViewer.WPF` adapted
  for non-WPF, or a community .NET 6+ port).
- **Candidate B:** PDFiumSharp or a direct P/Invoke wrapper over the native
  PDFium binary (which is LGPL, permitting app-store distribution).

Benchmark methodology (on reference hardware, both Windows and macOS):

1. Render pages 1-5 of each of three golden-corpus fixtures: `gc-simple-text`,
   `gc-large-1000pp`, `gc-two-column`.
2. Measure: render latency per page (warm, 10 iterations), peak managed-heap
   memory delta, and whether the native library loads without error on macOS
   arm64.
3. Confirm the adapter interface is wrappable with the HLD §F `IPdfRenderer`
   contract.
4. Confirm PDFium license (BSD-style) permits redistribution inside MSIX and
   notarized DMG App Store distributions.

ADR-0004 amendment records: winning wrapper name, version, measured page-render
latency (P50/P95), peak memory, and the license confirmation.

### Spike 3 & 4: WebView bridge + macOS WebGL2

Platform-specific approach:

- **Windows:** `Avalonia.WebView` control backed by `WebView2`
  (`Microsoft.Web.WebView2` NuGet). Windows 10 21H2+ ships WebView2.
- **macOS:** `Avalonia.WebView` control backed by `WKWebView`.

Bridge contract (Spike 3 output — feeds Phase 14):

```csharp
// Proposed bridge message type skeleton (to be ratified in Phase 14)
// Direction: C# → JS
record BridgeCommand(string Type, JsonElement Payload);
// Direction: JS → C#
record BridgeEvent(string Type, JsonElement Data);
```

Spike 4 macOS FPS measurement:
- Scene: 500 `PlaneGeometry` meshes with `MeshBasicMaterial` textured with a
  64x96 px solid-color canvas (simulating spine textures).
- Measurement tool: Three.js `Stats.js` overlay; record mean FPS over 10 s.
- Pass criterion: mean FPS ≥ 60 on the reference macOS hardware (M1 8 GB).
- Fail criterion: mean FPS < 45 → triggers ADR-0003 amendment to document
  the macOS WKWebView constraint and evaluate fallback strategies.

### Spike 5: FTS5 indexing

- Create a `spikes/s05-fts5/` console app that:
  1. Generates a synthetic 2,000-row `ExtractedPages` table with realistic
     text (~300 words/page, 3 pages/book = 6,000 rows total).
  2. Creates a FTS5 external-content virtual table over it.
  3. Runs 10 representative queries (single term, phrase, boolean) and measures
     P50/P95 latency using `System.Diagnostics.Stopwatch`.
- Pass criterion: P95 ≤ 500 ms on the reference Windows hardware (NFR-OGMA-004).

### Spike 6: AI gateway

- Create a `spikes/s06-ai-gateway/` console app that:
  1. Defines a minimal `IAiProvider` interface:
     `Task<string> CompleteAsync(string prompt, CancellationToken ct)`.
  2. Implements `OpenAiProvider` (calls the OpenAI-compatible `/chat/completions`
     endpoint; API key from env var, not hardcoded).
  3. Implements `OllamaProvider` (calls Ollama local `/api/generate`).
  4. Routes a test prompt through both providers; records latency.
- Pass criterion: both providers return a non-empty response; the interface is
  wrappable as the `IAiProvider` contract for Phase 12.
- Privacy note: the spike test prompt must not contain any user data.
  Use a static test string (e.g. "What is the capital of France?").

### Spike 7: LAN transport

- Create a `spikes/s07-lan-transport/` solution with two projects:
  - `LanHost`: a .NET 10 minimal Kestrel HTTPS server (self-signed cert,
    development trust store) that serves a 10 MB static byte range and
    registers itself via mDNS (`Makaretu.Dns` or `dotnet-mdns` library).
  - `LanClient`: discovers the host via mDNS, connects via HTTPS, streams the
    10 MB payload, measures throughput.
- Pass criteria: discovery within 5 s on a standard Wi-Fi LAN; throughput ≥ 5
  MB/s (sufficient for page-render streams per LAN-CLASSROOM §3 capacity notes).
- Security note: the spike uses development trust (localhost certificate
  pinned manually); production trust-pinning mechanism is scoped to Phase 16.
- ADR-0010 amendment: record the transport choice (Kestrel HTTPS + mDNS/DNS-SD),
  the .NET library chosen for mDNS, and the measured performance.

### Cross-platform approach (Windows + macOS)

Every spike runs on both platforms:

- CI matrix (GitHub Actions): `windows-latest` and `macos-latest` runners.
- Spike 3/4 (WebView/WebGL2) require actual GUI runners or a headless WebView
  test harness; note that GitHub-hosted macOS runners do not have a GPU.
  For Spike 4 FPS measurement, a real macOS machine (the reference hardware)
  must be used; CI can only validate that the code compiles and the scene
  initializes without error.
- Spike 7 (LAN transport) requires two processes on the same network; in CI
  this is simulated by running host and client as separate processes on the
  same runner with loopback networking.

---

## 7. Work breakdown (summary)

| WP | Work package | Est. |
| --- | --- | --- |
| WP1 | Spike 1: .NET 10 dependency matrix | 1 d |
| WP2 | Spike 2: PDFium wrapper benchmark | 2 d |
| WP3 | Spike 3: WebView↔JS bridge (both platforms) | 2 d |
| WP4 | Spike 4: 3D macOS WKWebView WebGL2 FPS | 1 d |
| WP5 | Spike 5: FTS5 indexing | 1 d |
| WP6 | Spike 6: AI gateway | 1 d |
| WP7 | Spike 7: LAN transport | 2 d |

Detail in `tasks.md`.

---

## 8. Cross-cutting checklist

- [x] **Colorful icons + manifest:** Phase 01 has no UI surface (spike test
  harnesses are not shipped UI). `icons.md` = stub.
- [x] **i18n (en/fr):** No user-facing strings produced. The spike harnesses
  are throwaway tools with no UI text that needs localizing.
- [x] **Accessibility:** No production UI produced.
- [x] **Privacy/egress:** Spike 6 (AI gateway) uses a static test prompt with
  no user data; API key sourced from environment variable only, never hardcoded
  or committed. This is the first enforcement of the egress chokepoint pattern.
- [x] **Reversibility:** No user data is touched. Spike code is throwaway;
  no schema changes.
- [x] **Performance budgets:** Spikes 2, 4, 5 produce the first measured
  baselines against NFR-OGMA-004, -005, -006. These are not pass/fail gates yet
  (no golden corpus, no production code), but they are the baseline trend data.
- [x] **Bounded-context tests:** Not applicable (spike code is excluded from
  architecture tests). The isolation policy (spike code never in `src/`) is
  the enforcement mechanism.
- [x] **Documentation:** `spikes/RESULTS.md` documents all measurements; ADR
  amendments committed with rationale and evidence. Developer guide in Phase 02
  references these results.

---

## 9. Definition of Done

### Global DoD (Phase 01 slice)

- [ ] All 7 spikes completed and results recorded in `spikes/RESULTS.md`.
- [ ] ADR-0004 amended with measured PDFium wrapper benchmark results; a single
  winning wrapper identified.
- [ ] ADR-0003 confirmed or amended with the macOS WKWebView WebGL2 FPS
  measurement on reference hardware.
- [ ] ADR-0010 transport section completed with Spike 7 results.
- [ ] Spike code is in `spikes/` only, not in `src/`; CI confirms the production
  solution file does not reference spike projects.
- [ ] `dotnet build` (warnings-as-errors) passes for each spike project on
  both Windows and macOS CI runners.
- [ ] No open R1 or R2 defect. Spike 6 AI gateway reviewed for any accidental
  user-data exposure (none expected; verified).
- [ ] `spikes/RESULTS.md` is committed and reviewed (WP code review pass).

### Phase-specific exit criteria

- The dependency matrix (Spike 1) is confirmed with no unresolved blocking
  incompatibility; any version pins required are recorded in `spikes/RESULTS.md`.
- The PDFium adapter interface (`IPdfRenderer` skeleton) is defined in
  `spikes/s02-pdfium/` and is consistent with the HLD §F contract.
- The WebView bridge typed message contract skeleton is defined in
  `spikes/s03-webview-bridge/` and is consistent with the Phase 14 bridge
  design intent.
- The FTS5 P95 query latency on the reference Windows hardware is ≤ 500 ms
  (NFR-OGMA-004); if > 500 ms, an issue is filed and a mitigation plan recorded.
- The LAN mDNS discovery latency is ≤ 5 s on a loopback simulation; any
  platform-specific mDNS library issues are documented.

---

## 10. Skills to use

See `skills.md` for full invocation guidance. Summary:

- `architecture:system-architecture-design` — validate that spike results
  align with the planned 9-project architecture.
- `sdlc-meta:advanced-testing-strategy` — design the benchmark methodology
  (sample sizes, warmup, P95 measurement).
- `ai:ai-model-gateway` — inform the IAiProvider interface design (Spike 6).
- `backend-databases:database-internals` — inform the FTS5 external-content
  table design (Spike 5).
- `security:network-security` — review the LAN transport spike for security
  properties (Spike 7).
- `devops-cloud:reliability-engineering` — spike rig discipline (isolated,
  reproducible, time-boxed).
- `superpowers:verification-before-completion` — confirm all spike pass
  criteria before declaring Phase 01 done.

---

## 11. Deliverables

| Artifact | Location |
| --- | --- |
| `spikes/s01-dotnet-matrix/` | repo root |
| `spikes/s02-pdfium/` (two wrapper implementations + benchmark harness) | repo root |
| `spikes/s03-webview-bridge/` (bridge message contract skeleton) | repo root |
| `spikes/s04-3d-macos/` (Three.js scene + FPS measurement) | repo root |
| `spikes/s05-fts5/` (FTS5 bench harness) | repo root |
| `spikes/s06-ai-gateway/` (IAiProvider + 2 impls) | repo root |
| `spikes/s07-lan-transport/` (host + client projects) | repo root |
| `spikes/RESULTS.md` (all measurements, go/no-go, ADR amendment refs) | repo root |
| `docs/adrs/ADR-0003.md` (amended with Spike 4 results) | `docs/adrs/` |
| `docs/adrs/ADR-0004.md` (amended with Spike 2 results) | `docs/adrs/` |
| `docs/adrs/ADR-0010.md` (transport section completed with Spike 7 results) | `docs/adrs/` |

---

## 12. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| macOS WKWebView WebGL2 FPS < 60 on reference hardware (ADR-0003 gate failure) | R5 | Spike 4 is designed to produce a measured result either way. If FPS < 45, an ADR-0003 amendment documents the constraint and we evaluate: (a) reduce spine-texture resolution, (b) reduce initial scene polygon count, (c) accept macOS-specific FPS target of ≥ 45 FPS as a documented exception. Owner sign-off required for any target change. |
| PDFium native binary fails to load on macOS arm64 | R5 | Spike 2 explicitly tests macOS arm64 load. If a wrapper cannot provide an arm64 binary, it is disqualified; the other candidate or a PdfPig render path (slower but pure-managed) becomes the fallback. ADR-0004 records the outcome. |
| mDNS library unavailable or unreliable on Windows | R5 | Spike 7 tests on both platforms. If mDNS is unreliable on Windows, the LAN transport design falls back to manual IP entry (always the documented fallback per LAN-CLASSROOM §3). |
| .NET 10 Avalonia package not yet on stable channel at spike time | R5 | Spike 1 verifies at the actual build time. If Avalonia 11.x is not yet net10-TFM stable, the ADR-0001 note ("bridge to .NET 8 if needed") is activated and an issue filed to revisit when Avalonia releases a net10 stable package. |
| AI gateway spike exposes API key in CI logs | R2 | Spike 6 uses environment variables; the GitHub Actions workflow masks the `OPENAI_API_KEY` secret. A CI audit step confirms the key is not present in any log artifact. |
| Spike 5 FTS5 P95 > 500 ms on reference hardware | R5 | If the latency exceeds the budget, the spike records the observed value and an investigation note (index size, query plan). The FTS5 design may require trigram tokenization or pre-computed ranking columns. A Phase 10 issue is filed with the spike data attached. |

---

## 13. Owner asks

1. **Spike 4 hardware access:** Spike 4 FPS measurement requires the reference
   macOS hardware (M1 MacBook Air 8 GB). If the team does not have this machine,
   Peter should provide access or perform the measurement personally, recording
   the FPS result in `spikes/RESULTS.md`.
2. **PDFium wrapper candidates confirmation:** Peter or the lead engineer must
   confirm the two specific wrapper packages to benchmark in Spike 2, based on
   the OQ-02 decision from Phase 00. The spike cannot start without knowing which
   two wrappers to compare.
3. **AI gateway API key for Spike 6:** A test/sandbox OpenAI-compatible API key
   (not the production key) is needed to validate the `OpenAiProvider` path in
   Spike 6. Peter should provide a test key via the CI secrets mechanism.

---

## 14. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand Plan authoring | v1.0 baseline created |
