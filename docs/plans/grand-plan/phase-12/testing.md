# Phase 12 — Test Plan

---

## 1. Test layers active in this phase

| Layer | Active | Notes |
| --- | --- | --- |
| Domain unit | Yes | `AiPrivacyTier`, `AiConsentRecord` invariant tests |
| Infrastructure unit | Yes | Provider adapters against WireMock fixtures |
| Integration | Yes | `AiGateway` end-to-end flows; DB repositories |
| UI / ViewModel | Yes | `PayloadPreviewViewModel`, `PrivacyCenterViewModel` command tests |
| Architecture | Yes | Egress-chokepoint test — **mandatory gate** |
| Performance | Yes | Gateway overhead benchmark |
| Security/Privacy | Yes | R2-tier tests; `/security-review` |
| Golden corpus | Yes | `simple-text` fixture for payload inspection |
| Accessibility | Yes | Privacy Center + payload preview dialog keyboard/SR |
| E2E | No | Phase 21 full E2E; Phase 12 uses integration tests |

---

## 2. Test fixtures required

| Fixture | Source | Used by |
| --- | --- | --- |
| `simple-text` golden-corpus PDF | Phase 05 corpus | P12-WP9-T2: payload contains metadata only |
| WireMock fixture: OpenAI-compatible response | Recorded from OpenAI API sandbox | P12-WP4-T5 |
| WireMock fixture: Anthropic Messages API response (with cache stats) | Recorded from Anthropic sandbox | P12-WP4-T5, P12-WP4-T6, P12-WP4-T7 |
| WireMock fixture: Ollama `/api/chat` response | Recorded from local Ollama instance | P12-WP4-T5 |
| Synthetic 500-book metadata corpus | Phase 02 perf seed | P12-WP9-T2 payload size test |

---

## 3. Deterministic oracles

| Test | Oracle / assertion |
| --- | --- |
| `Tier1_Payload_ContainsOnly_MetadataFields` | Payload JSON keys are a subset of `{title, author, tags, categories, descriptions, notes}`; zero `content_chunks` key present |
| `AiGateway_IsTheOnly_EgressPoint` | NetArchTest rule: no type outside `OgmaLibrary.Infrastructure.Ai` namespace has a transitive dependency on `HttpClient`, `AnthropicProvider`, `OpenAiCompatProvider`, or `OllamaProvider` |
| `AuditRepository_AppendIsImmutable` | After `AppendAsync`, assert EF Core change tracker has zero Update or Delete entries for `AiAuditEvent` |
| `PayloadPreview_Shown_Before_Every_EgressCall` | Mock `IPreviewGate` call count == 1 per `SendAsync` at Tier-1/2; call count == 0 at Tier-0 and Tier-3 |
| `CostEstimate_FormattedPer_Locale` | For `en-US`: `"$0.001234"`; for `fr-FR`: `"0,001234 $US"` (using `CultureInfo` formatting, not hard-coded) |
| `AnthropicProvider_Sends_NoTraining_Header` | Captured HTTP request headers contain `X-Anthropic-No-Training: 1` |
| `AnthropicProvider_Sets_CacheControl_On_System_Block` | Captured Anthropic Messages payload: `system` block has `cache_control: {"type": "ephemeral"}` |
| `AiGateway_Overhead_Under_50ms` | P95 of 100 gateway calls (mocked provider responding immediately) < 50 ms on CI reference runner |

---

## 4. R2 (privacy-breach) tests — unwaivable release blockers

These tests must pass with zero failures before the phase gate is cleared.

| Test ID | Scenario | Assertion |
| --- | --- | --- |
| R2-AI-001 | Tier-1 call with content chunks | `TierViolationException` thrown; zero HTTP calls made |
| R2-AI-002 | Tier-2 call without consent record | `ConsentRequiredException` thrown; zero HTTP calls made |
| R2-AI-003 | Tier-0 (disabled) — any AI call | `AiDisabledException` thrown; zero HTTP calls made |
| R2-AI-004 | Direct call to `HttpClient` bypassing `AiGateway` | Architecture test fails build |
| R2-AI-005 | Export audit after delete-history | Exported JSON contains all `AiAuditEvent` rows; zero `AiQueryHistoryEntry` rows |

---

## 5. Golden-corpus participation

Phase 12 adds one new golden-corpus scenario:

**`corpus/ai-payload-inspection/`**
- Input: metadata record for `simple-text` fixture (title, author, ISBN, tags,
  description, notes).
- Expected payload (Tier-1): JSON object with exactly the whitelisted metadata
  fields; SHA-256 hash recorded as oracle.
- Expected payload (Tier-2, 3 pages selected): same fields + `content_chunks`
  array with 3 items; SHA-256 hash recorded.
- Oracle type: deterministic hash comparison (payload shape is deterministic given
  the input).
- Location: `tests/golden-corpus/ai-payload-inspection/`

---

## 6. Accessibility tests

| Surface | Test |
| --- | --- |
| `PayloadPreviewDialog` | Keyboard: Tab order reaches all fields and buttons; Enter triggers Send; Escape triggers Cancel. Screen-reader: dialog role announced; payload size and tier label read aloud. |
| `PrivacyCenterView` | Keyboard: all sections reachable via Tab; Delete and Export buttons reachable without mouse. Screen-reader: tier badges have text equivalents; cost totals announced. |
| Tier badges | Color is never the sole carrier of tier state; text label always present alongside color badge (WCAG 2.2 1.4.1). |

---

## 7. Performance gate

| Budget | Threshold | Measurement |
| --- | --- | --- |
| Gateway overhead (NFR-OGMA-007 partial) | P95 < 50 ms (excluding provider latency) | `AiGateway_Overhead_Under_50ms` benchmark; mocked provider |
| Audit write | P95 < 10 ms per write | `AuditRepository_Append_PerformanceTest` benchmark |
| Privacy Center page load (100 audit rows) | P95 < 200 ms | `PrivacyCenter_Load_100Rows_PerformanceTest` |

---

## 8. CI integration

- All tests run on both Windows (x64) and macOS (arm64/x64) CI runners.
- Architecture test runs in its own project (`OgmaLibrary.ArchitectureTests`) as
  part of `dotnet test`; failure blocks merge.
- WireMock fixture files are committed to the test project under
  `tests/Fixtures/Ai/`; recorded responses are version-pinned with a SHA note.
- R2 tests are tagged `[Category("R2")]` and run with zero-tolerance in the CI
  privacy gate step.
- `AiGateway_Overhead_Under_50ms` is a BenchmarkDotNet job run nightly (not on
  every PR to avoid noise) and results committed to `docs/benchmarks/phase-12/`.
