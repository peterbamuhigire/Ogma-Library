# Phase 01 — Tasks

> Work packages and tasks for Risk Spikes & Technical Proof.
> ID format: `P01-WP<n>-T<m>`. Every task is throwaway unless explicitly
> marked "(keep)" — keep artifacts are bridge-contract skeletons and ADR
> amendments only.

---

## WP1 — Spike 1: .NET 10 Dependency Matrix

**Goal:** confirm that all planned libraries resolve and build on .NET 10 on
both platforms before the `Directory.Build.props` is written in Phase 02.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P01-WP1-T1 | Create `spikes/s01-dotnet-matrix/` directory and a `Matrix.sln`. For each planned library (see README §6 table), add a minimal `.csproj` that targets `net10.0` and references the library. | Phase 00 done, ADR-0001 Accepted | 0.25 d | ADR-0001, .NET 10 compat |
| P01-WP1-T2 | Run `dotnet restore && dotnet build` on the matrix solution on a Windows runner. Record: every package version, any `NU1202` errors, any `NU1701` warnings (package targeting a lower TFM). | P01-WP1-T1 | 0.25 d | ADR-0001, Spike 1 Windows result |
| P01-WP1-T3 | Run `dotnet restore && dotnet build` on the matrix solution on a macOS arm64 runner. Record native-lib load results for SkiaSharp and Tesseract. | P01-WP1-T1 | 0.25 d | ADR-0001, Spike 1 macOS result |
| P01-WP1-T4 | Record all resolved package versions and any required pins in `spikes/RESULTS.md §S1`. Flag any library that is only available for `net8.0` (not `net10.0`) as a version-pin risk for Phase 02. | P01-WP1-T2, P01-WP1-T3 | 0.25 d | ADR-0001, Phase 02 input |

---

## WP2 — Spike 2: PDFium Wrapper Benchmark

**Goal:** pick one PDFium wrapper; amend ADR-0004 with measured evidence.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P01-WP2-T1 | Create `spikes/s02-pdfium/`. Implement `IPdfRendererSpike` interface with two methods: `RenderPage(string path, int pageIndex, int dpi)` and `GetPageCount(string path)`. | Phase 00 OQ-02/ADR-0004, owner confirms two candidates | 0.25 d | ADR-0004, FR-READ-001/003 |
| P01-WP2-T2 | Implement `WrapperARenderer : IPdfRendererSpike` using Candidate A. Wire in the native PDFium binary for Windows x64 and macOS arm64. | P01-WP2-T1 | 0.5 d | ADR-0004 Candidate A |
| P01-WP2-T3 | Implement `WrapperBRenderer : IPdfRendererSpike` using Candidate B. Wire in the native PDFium binary for Windows x64 and macOS arm64. | P01-WP2-T1 | 0.5 d | ADR-0004 Candidate B |
| P01-WP2-T4 | Write the benchmark harness using `BenchmarkDotNet`. Benchmark: render pages 1-5 of `gc-simple-text`, `gc-large-1000pp`, `gc-two-column` (use the 3 cleared corpus fixtures from Phase 00). Measure: mean render latency per page, P95 latency, peak managed-heap delta. Run on Windows reference hardware. | P01-WP2-T2, P01-WP2-T3, golden corpus fixtures cleared (CON-9) | 0.5 d | ADR-0004, NFR-OGMA-005 |
| P01-WP2-T5 | Run the same benchmark on macOS arm64 reference hardware (or CI runner for compilation; real hardware for latency). Confirm native lib loads without crash. | P01-WP2-T4 | 0.25 d | ADR-0004, macOS parity |
| P01-WP2-T6 | Confirm license compatibility: read the license of Candidate A and Candidate B's PDFium binary. Confirm LGPL or BSD-style permits MSIX + Mac App Store + Windows Store redistribution. Record in results. | P01-WP2-T4/T5 | 0.25 d | ADR-0004, distribution (ADR-0009) |
| P01-WP2-T7 | Amend ADR-0004: record the winning wrapper (name, version), the measured P50/P95 render latency, the peak memory figure, the license confirmation, and the `IPdfRendererSpike` → `IPdfRenderer` production-contract note (keep). | P01-WP2-T4..T6 | 0.25 d | ADR-0004 amendment (keep) |

---

## WP3 — Spike 3: WebView↔JS Bridge

**Goal:** validate bidirectional typed message passing between C# (Avalonia)
and a Three.js scene on both platforms; define the bridge contract skeleton.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P01-WP3-T1 | Create `spikes/s03-webview-bridge/`. Create a minimal Avalonia app (`net10.0`) that embeds a WebView control. Confirm `Avalonia.WebView` NuGet version from Spike 1. | P01-WP1-T4, ADR-0002/ADR-0003 Accepted | 0.25 d | ADR-0003, FR-CAT-001 |
| P01-WP3-T2 | Implement a simple HTML/Three.js page (served from a local embedded resource or `data:` URI) that listens for messages from C# and posts messages back. C# side: use `WebView.PostMessageAsync` and `WebView.WebMessageReceived` (or platform equivalents). | P01-WP3-T1 | 0.5 d | ADR-0003, typed bridge |
| P01-WP3-T3 | Define the `BridgeMessage` record (see README §6 Spike 3 skeleton). Send a `{ "type": "ping", "payload": {} }` from C# → JS → C# round trip. Measure round-trip latency (10 iterations, `Stopwatch`). | P01-WP3-T2 | 0.25 d | NFR-PROD-005 (< 100 ms bridge) |
| P01-WP3-T4 | Run the bridge spike on a **Windows** machine (WebView2). Record: does WebView2 load? Does the round-trip complete? What is the P95 latency? | P01-WP3-T3 | 0.25 d | G1 (WebView bridge gate, Windows) |
| P01-WP3-T5 | Run the bridge spike on **macOS** (WKWebView). Record: does WKWebView load? Does the round-trip complete? What is the P95 latency? | P01-WP3-T3 | 0.25 d | G1 (WebView bridge gate, macOS) |
| P01-WP3-T6 | Commit the `BridgeMessage` / `BridgeCommand` / `BridgeEvent` record definitions to `spikes/s03-webview-bridge/BridgeContract.cs` as a **(keep)** artifact. This is the seed of the Phase 14 typed bridge. Record bridge latency in `spikes/RESULTS.md §S3`. | P01-WP3-T4/T5 | 0.25 d | G1, Phase 14 input (keep) |

---

## WP4 — Spike 4: 3D macOS WKWebView WebGL2

**Goal:** validate WebGL2 rendering at ≥ 60 FPS in WKWebView on macOS 13+
(ADR-0003 gate) using the bridge established in Spike 3.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P01-WP4-T1 | Create `spikes/s04-3d-macos/`. Build on top of the Spike 3 bridge app. Add a Three.js scene with 500 `PlaneGeometry` meshes (64x96 px canvas-textured, simulating spines) in a grid layout. Add a `Stats.js` FPS overlay (or a JS-to-C# FPS ping using the bridge). | P01-WP3-T6, ADR-0003 | 0.25 d | ADR-0003, NFR-OGMA-006, FR-CAT-001 |
| P01-WP4-T2 | Run the 3D scene on the reference macOS hardware (M1 MacBook Air 8 GB, macOS 13). Record: mean FPS over 10 s, GPU memory usage (Activity Monitor), any WebGL2 error console output. | P01-WP4-T1, Owner provides macOS hardware access | 0.5 d | ADR-0003 gate, NFR-OGMA-006 |
| P01-WP4-T3 | Run the 3D scene on Windows reference hardware (WebView2). Record: mean FPS, any rendering differences from macOS. | P01-WP4-T1 | 0.25 d | ADR-0003, cross-platform parity |
| P01-WP4-T4 | Amend ADR-0003: record measured FPS on both platforms. If macOS FPS ≥ 60: confirm ADR-0003 as Accepted/Confirmed. If macOS FPS < 45: record the constraint and the evaluated fallback options (reduce polygon count, reduce texture resolution, macOS-specific FPS target). Owner sign-off required if the ADR-0003 target is changed. | P01-WP4-T2/T3 | 0.25 d | ADR-0003 amendment, NFR-OGMA-006 |

---

## WP5 — Spike 5: FTS5 Indexing

**Goal:** confirm FTS5 external-content tables in SQLite meet the P95 ≤ 500 ms
full-text search budget on the reference hardware.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P01-WP5-T1 | Create `spikes/s05-fts5/`. Write a C# console app that: (a) creates an in-memory or temp-file SQLite DB, (b) inserts a synthetic `ExtractedPages` table with 6,000 rows (2,000 books × 3 pages, each ~300 words of Lorem Ipsum text generated by a seeded RNG), (c) creates an FTS5 external-content virtual table over it using `CONTENT="ExtractedPages" CONTENT_ROWID="PageId"`. | P01-WP1-T4 (SQLite package confirmed) | 0.25 d | ADR-0006, FR-SEARCH-002, NFR-OGMA-004 |
| P01-WP5-T2 | Implement 10 benchmark queries: 2 single-term, 2 two-term phrase, 2 boolean AND, 2 boolean OR, 2 prefix. Use `Stopwatch` with 20 warm-up iterations and 100 measured iterations; compute P50/P95. | P01-WP5-T1 | 0.25 d | NFR-OGMA-004 |
| P01-WP5-T3 | Run on Windows reference hardware. Record P50/P95 per query type. Flag any P95 > 500 ms with a note on the query plan (`EXPLAIN QUERY PLAN`). | P01-WP5-T2 | 0.25 d | NFR-OGMA-004, G7 (index rebuild context) |
| P01-WP5-T4 | Run on macOS reference hardware. Record P50/P95. Compare with Windows results. | P01-WP5-T3 | 0.25 d | NFR-OGMA-004, macOS parity |
| P01-WP5-T5 | Record all results in `spikes/RESULTS.md §S5`. If any P95 > 500 ms, file a Phase 10 issue with the spike data attached and note the mitigation options (trigram tokenization, pre-built rank column, column filtering). | P01-WP5-T3/T4 | 0.1 d | NFR-OGMA-004, Phase 10 input |

---

## WP6 — Spike 6: AI Gateway

**Goal:** validate the `IAiProvider` interface design and confirm both an
OpenAI-compatible and an Ollama local provider are implementable.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P01-WP6-T1 | Create `spikes/s06-ai-gateway/`. Define the `IAiProvider` interface: `Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct)` where `AiRequest` has `Prompt` (string) and `PrivacyTier` (enum: Offline/MetadataOnly/ContentAware/Local). `AiResponse` has `Content` (string) and `TokensUsed` (int). | ADR-0007 Accepted | 0.25 d | ADR-0007, FR-AI-001/002 |
| P01-WP6-T2 | Implement `OpenAiCompatibleProvider`: HTTP POST to `/chat/completions` using `System.Net.Http.HttpClient`. API key sourced from `Environment.GetEnvironmentVariable("SPIKE_OPENAI_KEY")` only. Use `Polly` for retry (2 retries, exponential backoff). | P01-WP6-T1 | 0.25 d | ADR-0007, FR-AI-002 |
| P01-WP6-T3 | Implement `OllamaProvider`: HTTP POST to Ollama local `/api/generate` endpoint. Default base URL `http://localhost:11434`. | P01-WP6-T1 | 0.25 d | ADR-0007, FR-AI-002/006 |
| P01-WP6-T4 | Write a test runner that: (a) sends a static test prompt ("Summarize in one sentence: the sky is blue.") through `OpenAiCompatibleProvider`; (b) sends the same prompt through `OllamaProvider` if Ollama is available locally; (c) records round-trip latency and response token count. The test prompt must not contain any user data. | P01-WP6-T2/T3 | 0.25 d | ADR-0007, FR-AI-007 (latency NFR-OGMA-007 baseline) |
| P01-WP6-T5 | Security review of the spike: confirm API key is never logged; confirm no user data in the test prompt; confirm retry logic does not amplify cost. Record in `spikes/RESULTS.md §S6`. | P01-WP6-T4 | 0.1 d | CTRL-OGMA (privacy, R2), ADR-0007 |

---

## WP7 — Spike 7: LAN Transport

**Goal:** validate HTTPS-over-LAN + mDNS discovery as the Phase 16 transport
baseline; complete ADR-0010 transport section.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P01-WP7-T1 | Research and select a .NET mDNS/DNS-SD library (e.g. `Makaretu.Dns`, `dotnet-mdns`, or `nServiceDiscovery`). Confirm the library builds on .NET 10 on both Windows and macOS. | P01-WP1-T4 | 0.25 d | ADR-0010, LAN-CLASSROOM §3 |
| P01-WP7-T2 | Create `spikes/s07-lan-transport/LanHost/`. Implement a .NET 10 minimal Kestrel HTTPS server on port 7890 (self-signed dev cert via `dotnet dev-certs`). Serve a 10 MB static payload at `GET /test-payload`. Register the service via mDNS (`_ogma._tcp.local`, instance name "OgmaSpike"). | P01-WP7-T1, ADR-0010 Proposed | 0.5 d | ADR-0010, LAN-CLASSROOM §3, NFR (LAN throughput) |
| P01-WP7-T3 | Create `spikes/s07-lan-transport/LanClient/`. Implement mDNS discovery: query for `_ogma._tcp.local`; record the host address when found. Measure: time from discovery query to first response. Connect via HTTPS (trust the dev cert via a pinned thumbprint for the spike). Download the 10 MB payload and measure throughput. | P01-WP7-T2 | 0.5 d | ADR-0010, LAN-CLASSROOM §3 |
| P01-WP7-T4 | Run host and client as two separate processes on the same machine (loopback) — simulates CI environment. Record: discovery time, throughput. Note: real LAN test requires two physical machines; document that real-LAN validation is deferred to Phase 16 with the actual reference hardware. | P01-WP7-T3 | 0.25 d | ADR-0010, Phase 16 input |
| P01-WP7-T5 | Run on macOS (confirm mDNS works with Bonjour / macOS system mDNS). Record any platform differences from Windows (where the mDNS library may use its own multicast stack vs macOS's built-in Bonjour). | P01-WP7-T4 | 0.25 d | ADR-0010, macOS parity |
| P01-WP7-T6 | Complete ADR-0010 transport section: record the chosen mDNS library, measured discovery time, measured throughput (loopback), platform notes, and the security note (development trust only; production trust-pin in Phase 16). Mark ADR-0010 transport section as "spike-complete; Phase 16 to finalize security model." | P01-WP7-T4/T5 | 0.25 d | ADR-0010 amendment (keep) |

---

## WP8 — Results consolidation and phase close

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P01-WP8-T1 | Write `spikes/RESULTS.md`: one section per spike (S1..S7), each with: measured values, pass/no-go decision, ADR amendment reference, and any open issues filed. | All WP1..WP7 tasks | 0.5 d | Phase 01 DoD |
| P01-WP8-T2 | Code review (`/code-review --effort medium`) of all spike code and the ADR amendments. The review focuses on: (a) no API keys or secrets committed, (b) spike code is in `spikes/` only, (c) benchmark methodology is sound (warmup, sample size, P95 computation), (d) ADR amendments are complete and traceable. | P01-WP8-T1 | 0.5 d | Global DoD §8, Phase 01 DoD |
| P01-WP8-T3 | Run the global Phase 01 DoD checklist. File any open items as GitHub issues with `phase-01` label. Merge the feature branch. | P01-WP8-T2 | 0.25 d | Phase 01 DoD |
