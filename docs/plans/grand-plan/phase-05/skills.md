# Phase 05 — Skills & Slash Commands

Phase-scoped detail for the ingestion pipeline. Every skill is tied to a specific
work package and a concrete artifact it must produce.

---

## Always-on (inherited)

| Skill / command | When | Artifact |
| --- | --- | --- |
| `superpowers:writing-plans` | Before WP1 | Ordered WP checklist with per-WP acceptance criteria |
| `superpowers:executing-plans` | Drive each WP | Completed, tested WP |
| `superpowers:test-driven-development` | Before each service in WP3-WP7 | Failing tests exist before implementation code |
| `superpowers:verification-before-completion` | Before marking each WP done | All tests green, build zero warnings |
| `superpowers:requesting-code-review` + `/code-review` | After WP6 (worker) and WP9 (UI); before phase close | Review findings resolved |
| `superpowers:systematic-debugging` | Any failing test | Root-cause documented |
| `superpowers:using-git-worktrees` | Branch `feature/P05-ingestion-pipeline` | Isolated branch; PR after DoD |

---

## Phase-05-specific skills

### `architecture:system-architecture-design`
**When:** WP2 (channel pipeline), WP6 (worker isolation).
**Task linkage:** P05-WP2-T1 (channel design), P05-WP6-T1 (BackgroundService pattern),
P05-WP6-T3 (per-file isolation architecture).
**Artifact:** Channel pipeline design rationale in `docs/architecture/ingestion-pipeline.md`;
`BackgroundService` worker structure reviewed for cancellation-correctness.

### `devops-cloud:reliability-engineering`
**When:** WP6 (job idempotency/recovery), WP8 (incremental rescan).
**Task linkage:** P05-WP6-T2 (`JobRecoveryService`), P05-WP6-T4 (recovery test),
P05-WP8-T1 (fast-path logic).
**Artifact:** `JobRecoveryService` with documented idempotency contract; test
`JobRecovery_AtStartup_RequeuesRunningJobs` passing; incremental-rescan fast-path
confirmed as safe under concurrent access.

### `sdlc-meta:advanced-testing-strategy`
**When:** WP4 (unavailable-file R1 tests), WP6 (fault-injection per-file isolation).
**Task linkage:** P05-WP4-T2, P05-WP4-T3 (data-loss prevention tests),
P05-WP6-T3 (sibling-job isolation), P05-WP11-T1 (golden-corpus ingestion).
**Artifact:** R1 tests named and passing; fault-injection documented in `testing.md`.

### `frontend-ux:practical-ui-design` + `avalonia-desktop-development`
**When:** WP9 (scan progress panel), WP10 (health report).
**Task linkage:** P05-WP9-T3 (ScanProgressView), P05-WP10-T2 (ScanHealthView).
**Artifact:** Both Avalonia UserControls pass a keyboard-navigation walkthrough;
bindings use the design tokens from Phase 03; no magic constants in XAML.

> Reference: `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` (authored in
> parallel) — apply its MVVM, binding, and DataTemplate conventions.

### `frontend-ux:design-audit`
**When:** After WP9 and WP10 icons are wired.
**Task linkage:** P05-WP9-T4, P05-WP10-T3 (icon coherence).
**Artifact:** Scan icons (oak-amber / clay / sage) consistent with Phase 03 tokens;
no placeholder icon ships without a tracking item.

---

## Slash commands

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review` | After WP6 (reliability-critical), WP7 (native libs), and WP9 (UI); before phase close | Correctness + safety; escalate to `--effort high` for WP6 |
| `/verify` | Before marking phase done | Confirm `dotnet build`, `dotnet test`, arch tests, and CI matrix all green |
| `/simplify` | After WP6 and WP8 | Remove over-engineering in worker dispatch or rescan logic |
| `/init` | End of phase | Update `CLAUDE.md` with new service interfaces and `Workers` project |
