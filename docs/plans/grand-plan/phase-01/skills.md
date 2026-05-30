# Phase 01 — Skills & Slash Commands

> Phase-scoped invocation guide. Bird's-eye map: `SKILLS-INDEX.md §Part I Phase 01`.
> Each skill is tied to a specific spike and expected artifact.

---

## Always-on

| Skill / command | When | Expected artifact |
| --- | --- | --- |
| `superpowers:test-driven-development` | Before implementing each spike benchmark harness | Test cases for the benchmark (pass/fail criteria, oracle values) written before the harness code |
| `superpowers:verification-before-completion` | Before closing each WP and before Phase 01 close | Checklist confirming all DoD items are green; specifically: `spikes/RESULTS.md` complete, ADR amendments committed |
| `superpowers:systematic-debugging` | If any spike fails to build or produces unexpected results | Structured root-cause analysis before modifying the spike |
| `superpowers:requesting-code-review` + `/code-review --effort medium` | After WP8-T1 (`spikes/RESULTS.md` complete) | Code review of all spike code; security focus on Spike 6 (no secrets committed) |

---

## WP1 — .NET 10 dependency matrix

| Skill | Task | What to produce |
| --- | --- | --- |
| `language-standards` (C# / .NET standards) | P01-WP1-T1 | Minimal `.csproj` files following the project file conventions that will be used in Phase 02 (no `PackageReference` version wildcards; explicit version pins) |
| `devops-cloud:reliability-engineering` | P01-WP1-T2/T3 | A repeatable matrix-check script (PowerShell + bash) that CI can run; records results to `spikes/RESULTS.md §S1` in a machine-parseable format (Markdown table) |

---

## WP2 — PDFium wrapper benchmark

| Skill | Task | What to produce |
| --- | --- | --- |
| `sdlc-meta:advanced-testing-strategy` | P01-WP2-T4/T5 (benchmark methodology) | A benchmark design document (within `spikes/s02-pdfium/BENCHMARK.md`) covering: sample size justification (why 10 iterations per page per fixture), warmup protocol (5 iterations discarded), P95 computation method, and the acceptance threshold (NFR-OGMA-005: ≤ 100 ms P95 per page on reference hardware) |
| `architecture:system-architecture-design` | P01-WP2-T7 (ADR-0004 amendment) | The `IPdfRenderer` production contract note in ADR-0004: what the interface must expose (render to bitmap, page count, page dimensions, dispose) and what must NOT be in the interface (platform-specific types) |

### Concrete invocation: BenchmarkDotNet setup for Spike 2

Use `sdlc-meta:advanced-testing-strategy` to design the BenchmarkDotNet
configuration. The benchmark class should use:
- `[GlobalSetup]` to open the PDF (amortized over all iterations)
- `[Benchmark]` to render a single page
- `[Params("gc-simple-text", "gc-large-1000pp", "gc-two-column")]` for fixtures
- A custom `[Config]` with `WarmupCount = 5`, `IterationCount = 20`
- The `MemoryDiagnoser` attribute to capture managed-heap allocation

---

## WP3 & WP4 — WebView bridge + macOS WebGL2

| Skill | Task | What to produce |
| --- | --- | --- |
| `frontend-design:frontend-design` | P01-WP3-T2 (HTML/Three.js page) | A minimal but well-structured Three.js entry point (`spike.html`) that can be evolved into the Phase 14 bookshelf scene; the file follows the JavaScript patterns that will be used in Phase 14 |
| `typescript-effective` (or `javascript-modern` for the spike) | P01-WP3-T3/T6 (bridge contract) | A typed JavaScript message handler (`bridgeHandlers.js`) that maps `BridgeCommand.Type` to functions; this pattern is the seed of the Phase 14 `BridgeDispatcher` |
| `architecture:validation-contract` | P01-WP3-T6 (keep: `BridgeContract.cs`) | A `BridgeContract.cs` file defining `BridgeCommand` and `BridgeEvent` records with `[JsonPropertyName]` annotations; this record is reused in Phase 14 |
| `frontend-ux:frontend-performance` | P01-WP4-T2 (FPS measurement) | A measurement protocol note in `spikes/s04-3d-macos/MEASUREMENT.md`: how Stats.js FPS is read, the 10-second measurement window, the 2-second warmup exclusion, and the device state requirements (no other GPU-intensive apps running) |

---

## WP5 — FTS5 indexing

| Skill | Task | What to produce |
| --- | --- | --- |
| `backend-databases:database-internals` | P01-WP5-T1..T3 | (a) The SQLite FTS5 virtual table DDL with the correct `CONTENT=` and `CONTENT_ROWID=` clauses; (b) the `EXPLAIN QUERY PLAN` outputs for each query type; (c) guidance on whether a trigram tokenizer or a `rank` precomputed column would be needed if P95 > 500 ms |
| `sdlc-meta:advanced-testing-strategy` | P01-WP5-T2 (benchmark methodology) | A benchmark design: seeded RNG generation for synthetic text (seed = 42, fixed), warm-up (20 iterations), measured (100 iterations), P95 computation via sorted array |

---

## WP6 — AI gateway

| Skill | Task | What to produce |
| --- | --- | --- |
| `ai:ai-model-gateway` | P01-WP6-T1 (IAiProvider interface) | Guidance on the interface design: specifically, whether the `PrivacyTier` enum belongs on the `AiRequest` or on the provider configuration; the skill informs the "provider-neutral gateway" pattern that ADR-0007 adopts |
| `ai:ai-llm-integration` | P01-WP6-T2/T3 (provider implementations) | The HTTP client implementation patterns for OpenAI-compatible and Ollama APIs; request/response schema; error handling (rate limit 429, server error 5xx) |
| `claude-api` (document-skills variant) | P01-WP6-T2 | If the OpenAI-compatible provider is validated against the Anthropic Messages API (which is OpenAI-compatible via the Anthropic SDK), note how prompt caching (`cache_control` beta) would be added in Phase 12; record as a note in the spike, not production code |

### Security note for Spike 6 invocation

Before running `OpenAiCompatibleProvider` in CI, invoke
`superpowers:verification-before-completion` with a specific check: confirm
that `Environment.GetEnvironmentVariable("SPIKE_OPENAI_KEY")` returns null in
a local run without the env var set, and that the code handles null gracefully
(skips the live test, records "skipped" in results). This prevents accidental
key commits and accidental charges.

---

## WP7 — LAN transport

| Skill | Task | What to produce |
| --- | --- | --- |
| `security:network-security` | P01-WP7-T2/T3 (Kestrel HTTPS + mDNS) | A security note in `spikes/s07-lan-transport/SECURITY-NOTE.md` covering: (a) why self-signed certs are acceptable for the spike but not for production, (b) what Phase 16 must do (admin-provisioned root CA, client trust-pinning), (c) known mDNS security properties (no authentication; addressed in Phase 16 by requiring HTTPS auth after discovery) |
| `architecture:system-architecture-design` | P01-WP7-T6 (ADR-0010 transport section) | The architecture note for the LAN transport: the bounded-context boundary (Library Sharing / Host context is the only place with an inbound listener), the Kestrel configuration (HTTP/2, HTTPS, LAN-scoped binding), and the mDNS service record structure (`_ogma._tcp`, TXT record with `version` and `requires-auth=true`) |

---

## Slash commands in this phase

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review --effort medium` | After P01-WP8-T1 (all spike code complete) | Review all spike code with emphasis on: no secrets committed, benchmark methodology soundness, spike code not leaking into `src/` |
| `/security-review` | Specifically for Spike 6 (AI gateway) after P01-WP6-T5 | Review the AI gateway spike for privacy properties: API key handling, no user data in prompts, retry amplification risk |
| `/verify` | After P01-WP8-T2 (review complete) | Run `dotnet build` on all spike projects on both platforms; confirm all pass before merging |
| `/run` | During Spike 3/4 (WebView bridge app) | Drive the Avalonia bridge spike app to confirm the WebView loads and the bridge round-trip completes visually |

---

## Notes on skills NOT used in Phase 01

- `avalonia-desktop-development` / `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` —
  Spike 3/4 use Avalonia, but spike code does not need to meet production
  AVALONIA-STANDARDS.md rules (it is throwaway). Reference the standards only
  to ensure the spike's bridge contract pattern is compatible with what Phase 14
  will require.
- `frontend-ux:premium-ui-ux-design` — no design work in spikes.
- `security-scanning:security-sast` — SAST applies to production code (Phase 19);
  the spike security review (`/security-review` on Spike 6) is a manual check,
  not a full SAST run.
- `documentation-generation:architecture-decision-records` — ADR amendments in
  Phase 01 are narrow (measured evidence sections only); invoke the skill only
  for ADR-0010 completion (WP7-T6), which is the only new ADR work in this phase.
