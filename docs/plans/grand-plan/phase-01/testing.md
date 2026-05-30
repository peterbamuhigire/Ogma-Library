# Phase 01 — Test Plan

> Phase 01 produces spike code only (throwaway). The "tests" are spike
> verification checks: pass/fail assertions on the measured results against
> the pre-specified acceptance thresholds. No production test layer (Domain,
> Infrastructure, etc.) is instantiated in this phase.

---

## Applicable test layers

| Layer | Applies in Phase 01? | Notes |
| --- | --- | --- |
| Domain | No | No domain model code yet |
| Infrastructure | Minimal | Spike DB and HTTP clients are spike-scoped; not in the production test suite |
| PDF | Partial | Spike 2 uses 3 golden-corpus fixtures for the render benchmark; the fixture manifest must be cleared (CON-9) before the spike runs |
| Search | Partial | Spike 5 uses a synthetic corpus; no golden-corpus PDF fixtures required |
| AI | Minimal | Spike 6 runs a live API call in a controlled test harness; not a repeatable CI test (requires a live API key) |
| UI | No | Spike 3/4 Avalonia apps are not tested with automated UI tests |
| 3D | Partial | Spike 4 FPS measurement is a manual test on real hardware; CI can only confirm the scene initializes without error |
| Performance | Yes | Spikes 2, 4, 5 are performance tests; they are the first benchmarks in the project |
| Packaging | No | Not applicable in this phase |

---

## Spike verification checks

### S1: .NET 10 Dependency Matrix

| Check | ID | Oracle | Automated? |
| --- | --- | --- | --- |
| All packages resolve on net10.0 (Windows) | P01-TEST-S1-01 | `dotnet restore` exit 0; zero `NU1202` errors | Yes (CI Windows runner) |
| All packages resolve on net10.0 (macOS arm64) | P01-TEST-S1-02 | `dotnet restore` exit 0; zero `NU1202` errors | Yes (CI macOS runner) |
| Native libs load on macOS arm64 (SkiaSharp, Tesseract) | P01-TEST-S1-03 | Spike console app loads and calls a no-op method on each native lib without `DllNotFoundException` | Yes (CI macOS runner, if runner has GPU/native support) |

### S2: PDFium Wrapper Benchmark

| Check | ID | Oracle | Automated? |
| --- | --- | --- | --- |
| Candidate A renders all 3 corpus fixtures | P01-TEST-S2-01 | Returns a non-null bitmap for pages 1-5 of each fixture; no exception thrown | Yes (BenchmarkDotNet run) |
| Candidate B renders all 3 corpus fixtures | P01-TEST-S2-02 | Same as above | Yes |
| Winning wrapper P95 render ≤ 100 ms (Windows reference HW) | P01-TEST-S2-03 | BenchmarkDotNet P95 value ≤ 100 ms for `gc-simple-text` page 1 (NFR-OGMA-005 proxy); recorded in `RESULTS.md §S2` | Manual verification of recorded value |
| Native lib loads on macOS arm64 without crash | P01-TEST-S2-04 | No `DllNotFoundException` or `SEHException` on load | Yes (CI macOS runner) |
| License permits App Store + Windows Store redistribution | P01-TEST-S2-05 | Engineer reads the license file; records "confirmed" or "blocked" in `RESULTS.md §S2` | Manual |

**Corpus fixtures used:** `gc-simple-text`, `gc-large-1000pp`, `gc-two-column`.
All must be in `tests/golden-corpus/fixtures/` with matching entries in
`MANIFEST.sha256` before this spike runs (see Phase 00 CON-9 task).

### S3: WebView↔JS Bridge

| Check | ID | Oracle | Automated? |
| --- | --- | --- | --- |
| WebView2 loads on Windows and renders an HTML page | P01-TEST-S3-01 | No `InvalidOperationException` on WebView2 init; a `document.title` ping returns the expected string | Manual (requires GUI) |
| WKWebView loads on macOS and renders an HTML page | P01-TEST-S3-02 | Same as above for WKWebView | Manual (requires GUI) |
| C# → JS → C# round-trip completes (Windows) | P01-TEST-S3-03 | The "pong" response is received within 500 ms on 10 of 10 iterations | Manual (recorded in `RESULTS.md §S3`) |
| C# → JS → C# round-trip completes (macOS) | P01-TEST-S3-04 | Same as above | Manual |
| P95 bridge round-trip ≤ 100 ms (both platforms) | P01-TEST-S3-05 | Satisfies NFR-PROD-005 ("no UI stall > 100 ms") | Manual (recorded value) |
| `BridgeContract.cs` is committed to `spikes/s03-webview-bridge/` | P01-TEST-S3-06 | File exists; types compile on net10.0 | Yes (CI build) |

### S4: 3D macOS WKWebView WebGL2

| Check | ID | Oracle | Automated? |
| --- | --- | --- | --- |
| Three.js scene with 500 plane meshes initializes without WebGL error (macOS) | P01-TEST-S4-01 | No `THREE.WebGLRenderer: Context Lost` error in JS console; scene renders at least 1 frame | Manual (real macOS hardware) |
| Mean FPS ≥ 60 over 10 s (macOS M1 reference hardware) | P01-TEST-S4-02 | NFR-OGMA-006 gate: recorded mean FPS in `RESULTS.md §S4`. If < 60, an ADR-0003 amendment issue is filed. | Manual (real hardware required) |
| Three.js scene renders on Windows WebView2 | P01-TEST-S4-03 | No WebGL2 error; scene renders | Manual (real Windows hardware) |
| CI build check: spike code compiles | P01-TEST-S4-04 | `dotnet build spikes/s04-3d-macos/` exits 0 on both CI runners | Yes (CI) |

**Note:** Because GitHub-hosted macOS CI runners do not have a discrete GPU,
the FPS measurement (P01-TEST-S4-02) cannot be run in CI. It requires the real
reference macOS hardware. This is an accepted limitation of the spike; the
measurement is a **one-time** manual test, recorded in `spikes/RESULTS.md §S4`
with the machine spec and the date.

### S5: FTS5 Indexing

| Check | ID | Oracle | Automated? |
| --- | --- | --- | --- |
| FTS5 virtual table created without SQLite error | P01-TEST-S5-01 | `SQLiteException` is not thrown on `CREATE VIRTUAL TABLE` | Yes (CI) |
| 10 queries return non-empty result sets | P01-TEST-S5-02 | Each query returns ≥ 1 row (the synthetic corpus is designed so every term has at least one match) | Yes (CI) |
| P95 query latency ≤ 500 ms (Windows reference HW) | P01-TEST-S5-03 | NFR-OGMA-004: recorded P95 value in `RESULTS.md §S5`. If > 500 ms, Phase 10 issue filed. | Manual (real hardware, recorded value) |
| P95 query latency ≤ 500 ms (macOS reference HW) | P01-TEST-S5-04 | Same as above for macOS | Manual (real hardware, recorded value) |
| Benchmark runs deterministically from seed 42 | P01-TEST-S5-05 | Running the synthetic corpus generator twice with seed 42 produces identical row counts and content hashes | Yes (CI) |

### S6: AI Gateway

| Check | ID | Oracle | Automated? |
| --- | --- | --- | --- |
| `IAiProvider` interface compiles on net10.0 | P01-TEST-S6-01 | `dotnet build` exits 0 | Yes (CI) |
| `OpenAiCompatibleProvider` returns a non-empty response (when key is set) | P01-TEST-S6-02 | `AiResponse.Content.Length > 0` for the static test prompt | Manual (requires API key in env var; skipped in CI if key absent) |
| `OllamaProvider` returns a non-empty response (when Ollama is running) | P01-TEST-S6-03 | Same as above | Manual (skipped in CI if Ollama not running) |
| No API key is present in any committed file or CI log artifact | P01-TEST-S6-04 | `git grep -r "sk-"` in the spike directory returns no results; CI log reviewed for key leakage | Yes (CI grep check) |

### S7: LAN Transport

| Check | ID | Oracle | Automated? |
| --- | --- | --- | --- |
| `LanHost` starts and registers mDNS service | P01-TEST-S7-01 | Host starts without exception; mDNS registration completes within 2 s | Yes (CI, loopback) |
| `LanClient` discovers the host via mDNS | P01-TEST-S7-02 | Client receives the mDNS response within 5 s on loopback | Yes (CI, loopback) |
| HTTPS connection established and 10 MB payload downloaded | P01-TEST-S7-03 | `HttpClient.GetAsync` returns 200; response body length = 10,485,760 bytes | Yes (CI, loopback) |
| Throughput ≥ 5 MB/s (loopback; real LAN validated in Phase 16) | P01-TEST-S7-04 | Elapsed time for 10 MB download ≤ 2 s on loopback (loopback >> real LAN; this is a sanity check only) | Yes (CI, recorded value) |
| mDNS registration and discovery work on macOS | P01-TEST-S7-05 | Same checks as S7-01..S7-03 on macOS CI runner | Yes (CI macOS runner) |

---

## Beta gate evidence produced by Phase 01

| Gate | ID | Evidence produced |
| --- | --- | --- |
| G1 — WebView bridge | S3 results | Bridge round-trip P95 measured; typed contract defined |
| G2 — PDFium wrapper | S2 results | Winning wrapper identified; render P95 measured; license confirmed |

The other gates (G3-G8) require production code (Phases 04-12) to be
instantiated. Phase 01 provides the architectural foundation evidence only.

---

## Open issues from spikes (filed in Phase 01, addressed later)

Any spike where a threshold is missed generates a GitHub issue with:
- Label: `spike-finding`, `phase-<target>`, `risk-<tier>`
- Body: spike ID, measured value, threshold, mitigation options from
  `spikes/RESULTS.md`
- Assigned to the phase that will address it (e.g. Phase 10 for FTS5 latency)

No spike finding may be silently dropped; every finding is either resolved in
this phase or has a tracked issue before Phase 01 closes.
