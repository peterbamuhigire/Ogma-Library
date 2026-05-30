# Phase 15 — Skills & Slash Commands

---

## Primary skills

### `sdlc-meta:advanced-testing-strategy`

- **Tasks:** P15-WP2-T7, P15-WP2-T8, P15-WP9-T1..T2 — OCR golden-corpus and
  fault-injection tests.
- **Why:** The OCR pipeline requires per-file isolation (one scanned PDF fixture
  per scenario), deterministic golden-corpus oracles, and a correct resume-after-
  interruption fault-injection approach.
- **Artifact:** `OcrJob_Recovery_AfterInterruption_NoDuplicatePages` fault-
  injection test; `OcrJob_ScannedPdf_BecomesSearchable` golden-corpus test; OCR
  scenario fixture directory.

### `devops-cloud:reliability-engineering`

- **Tasks:** P15-WP2-T3..T4, P15-WP6-T1..T2 — resumable jobs; batch enrichment
  recovery.
- **Why:** The OCR job and batch enrichment job are long-running background
  processes that must survive interruption (NFR-OGMA-009). The skill provides
  patterns for idempotent job design, progress checkpointing, and graceful
  cancellation.
- **Artifact:** `OcrJobWorker` with idempotent page-processing and checkpoint
  queries; `BatchEnrichmentJob` chunk recovery.

### `security:code-safety-scanner`

- **Tasks:** P15-WP4-T2..T3, P15-WP4-T8 — password credential flow; memory
  hygiene.
- **Why:** The PDF password is a secret; it must not appear in logs, the
  catalogue, or managed memory after use. The skill provides a checklist for
  secret-handling in .NET: `SecureString` vs char array; pinned GC handles;
  explicit zero-on-dispose.
- **Artifact:** `WindowsPasswordProvider` and `MacOsPasswordProvider` with inline
  security comments; `Password_NeverStoredInCatalogue` test.

### `security-scanning:security-hardening`

- **Tasks:** P15-WP4-T2..T3, P15-WP4-T8 — credential flow security review.
- **Why:** OS credential store key format, DPAPI/Keychain entitlement requirements,
  and correct API usage are security-critical.
- **Artifact:** Code review checklist applied to `WindowsPasswordProvider` and
  `MacOsPasswordProvider`; `/security-review` findings resolved.

### `frontend-ux:interaction-design-patterns`

- **Tasks:** P15-WP3-T1..T5, P15-WP5-T2..T3 — OCR status UI; split-view scaffold.
- **Why:** The OCR status surface (pause/cancel/retry) and the V2-placeholder
  split-view must be clear and non-frustrating. The skill provides patterns for
  progressive-disclosure status UIs and informative placeholder states.
- **Artifact:** `OcrJobStatusViewModel`; `V2PlaceholderPanel` with honest copy.

### `backend-databases:database-internals`

- **Tasks:** P15-WP7-T1..T3 — SQLite query plan review and composite indices.
- **Why:** `EXPLAIN QUERY PLAN` analysis and index design require deep SQLite
  knowledge — index covering, partial indices, and the cost model.
- **Artifact:** `query-plans.md`; migration M015b with documented index rationale;
  benchmark fixture.

### `frontend-ux:frontend-performance`

- **Task:** P15-WP7-T3 — smart-shelf benchmark.
- **Why:** The 2 s P95 budget must be asserted against the 2,000-book synthetic
  corpus; the benchmark fixture design follows the same pattern as Phase 10/11.
- **Artifact:** `SmartShelf_QueryBenchmark_2000Books` BenchmarkDotNet job;
  baseline JSON.

---

## Always-on skills

| Skill | How applied |
| --- | --- |
| `superpowers:test-driven-development` | Fault-injection and golden-corpus tests written before implementations (WP2, WP4) |
| `superpowers:verification-before-completion` | `dotnet test` + fault-injection run + benchmark before claiming WP done |
| `superpowers:requesting-code-review` + `/code-review --effort high` | WP2 (OCR worker) and WP4 (password) at high effort |
| `/security-review` | WP4 and WP9 — mandatory for credential handling |
| `superpowers:systematic-debugging` | Any OCR recognition failure or duplicate-page fault-injection failure |
| `documentation-generation:docs-architect` | ADR-0013 authored after WP2 (Tesseract binary decision) |

---

## Slash commands

| Command | When |
| --- | --- |
| `/code-review --effort high` | WP2 (OcrJobWorker); WP4 (password flow); WP9 final |
| `/security-review` | WP4 (password credential flow); WP9-T3 mandatory |
| `/run` | After WP3: run app to confirm OCR status UI visible; after WP4: run on Windows + macOS to confirm password prompt works |
| `/verify` | After WP7: confirm smart-shelf benchmark passes; after WP9: confirm OCR golden-corpus test green |
| `/simplify` | After WP6 (batch enrichment hardening) and WP2 (OcrJobWorker) |
