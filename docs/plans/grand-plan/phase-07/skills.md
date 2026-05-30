# Phase 07 — Skills & Slash Commands

Phase-scoped detail for metadata enrichment and collection health. Every skill is
tied to a specific work package and a concrete artifact it must produce.

---

## Always-on (inherited)

| Skill / command | When | Artifact |
| --- | --- | --- |
| `superpowers:brainstorming` | Before WP3 (confidence merge formula), WP9 (health dashboard UX) — required before design decisions | Options explored; preferred formula and dashboard layout documented before implementation |
| `superpowers:writing-plans` | Before WP1 | Ordered WP checklist |
| `superpowers:executing-plans` | Drive each WP | Completed, tested WP |
| `superpowers:test-driven-development` | Before each service (WP1-WP5, WP6, WP8) | Failing test first |
| `superpowers:verification-before-completion` | Before each WP close | All tests green; no hard-coded strings |
| `superpowers:requesting-code-review` + `/code-review` | After WP6 (write-back, R1-critical); WP9 (health dashboard); before phase close | Review findings resolved |
| `superpowers:systematic-debugging` | Any failing test | Root-cause documented before fix |
| `superpowers:using-git-worktrees` | Branch `feature/P07-metadata-enrichment` | Isolated branch; PR after DoD |

---

## Phase-07-specific skills

### `backend-databases:database-design-engineering`
**When:** WP5 (provenance upsert), WP8 (quality score migration), WP9 (health queries).
**Task linkage:** P07-WP5-T1 (`ApplyMergedMetadataAsync` upsert pattern),
P07-WP8-T1 (migration for `QualityScore` column), P07-WP9-T2 (health dashboard
GROUP BY queries with covering indexes).
**Artifact:** All five health queries use covering indexes and are verified with
`EXPLAIN QUERY PLAN`; `QualityScore` column migration has a down migration.

### `architecture:api-error-handling`
**When:** WP2 (provider HTTP clients).
**Task linkage:** P07-WP2-T1 (Polly retry policy), P07-WP2-T7 (one-provider-fails
test).
**Artifact:** Both clients have retry (3 attempts, exponential backoff + jitter),
circuit-breaker, and timeout wired via Polly; documented in XML comments.

### `devops-cloud:reliability-engineering`
**When:** WP7 (batch enrichment rate limiter, pause/resume).
**Task linkage:** P07-WP7-T2 (token-bucket rate limiter), P07-WP7-T5 (pause/resume
via `CancellationTokenSource` swap), P07-WP7-T6 (rate-limit integration test).
**Artifact:** `TokenBucketRateLimiter` tested to respect configured RPM; pause/resume
leaves jobs in `Queued` state, not lost.

### `sdlc-meta:advanced-testing-strategy`
**When:** WP6 (PDF write-back fault injection), WP11 (R1 verification).
**Task linkage:** P07-WP6-T7 (`PdfWriteBack_RestoredOnFailure` — inject exception;
assert byte-identical restore), P07-WP11-T1 (cross-platform fault-injection run).
**Artifact:** Fault-injection test for write-back with both Windows and macOS atomic
rename semantics confirmed; documented in `testing.md`.

### `frontend-ux:data-visualization`
**When:** WP9 (health dashboard section counts and quality score display).
**Task linkage:** P07-WP9-T3 (count badges per section), P07-WP9-T5 (< 500 ms load
gate).
**Artifact:** Health dashboard count badges update reactively; quality score rendered
as a numeric percentage and a colored progress bar (sage ≥ 0.8, clay < 0.5).

### `frontend-ux:premium-ui-ux-design`
**When:** WP4 (enrichment review panel), WP9 (health dashboard).
**Task linkage:** P07-WP4-T2 (per-field old/new value display, confidence bar),
P07-WP9-T3 (5-tab health panel).
**Artifact:** Enrichment panel conveys confidence visually (color-coded confidence
bar: sage high, clay low) without color being the only carrier (numeric % also shown).

### `documentation-generation:architecture-decision-records`
**When:** WP6 (ADR-0008 ratification); WP3 (confidence formula document).
**Task linkage:** P07-WP6-T9 (`pdf-writeback-protocol.md`), P07-WP3-T8
(`confidence-merge-formula.md`), P07-WP11-T4 (ADR-0008).
**Artifact:** `docs/adr/ADR-0008.md` (Accepted); both architecture documents present
and cross-referenced from their phase README.

---

## Slash commands

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review` | After WP6 (write-back, safety-critical); before phase close | Correctness + safety; escalate to `--effort high` for WP6 |
| `/security-review` | WP2 (provider clients) and WP10 (payload preview) | Confirm no content leaks to providers; payload = ISBN + title only |
| `/simplify` | After WP3 (merge service) and WP9 (health queries) | Remove unnecessary complexity in formula or query logic |
| `/verify` | Before phase done | Build + test + arch tests on both CI runners |
| `/init` | End of phase | Update `CLAUDE.md` with enrichment services, write-back protocol, and health dashboard entry |
