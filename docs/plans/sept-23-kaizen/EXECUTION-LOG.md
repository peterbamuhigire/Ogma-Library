# Sept-23 Kaizen execution log

Append-only. The newest entries go at the bottom. Every resumed session reads this log first, then
continues from the last entry.

## Authority

On 2026-09-25 the owner (Peter Bamuhigire) granted full authority to act and decide on their
behalf, guided by the skills engines, to complete the entire assignment. Their instructions were
to commit and push to `main`, to spawn subagents where useful, and to resume automatically after
usage limits.

A session-only hourly cron job (`:17`) re-enters the work while the session is idle.

## Owner-proxy decisions

These decisions were taken under that authority. Each is the recommended option in
[08-master-plan.md §3](08-master-plan.md#3-owner-decisions-needed), unless noted.

| ID | Decision | Basis |
|---|---|---|
| D-01 | **Restore the deleted tests** (`git restore tests/`) | The owner chose this explicitly on 2026-09-25 |
| D-02 | Sept-23 Kaizen is the **execution sequence**; the Aug-39 matrix stays the **requirement accountability** authority | Implied by the owner's "start implementation" instruction |
| D-03 | Support **multiple library roots**, with add, remove and enable in Settings | The backend `ILibraryRootService.AddAsync` exists; LIB-001 intent |
| D-04 | Derived covers, spines and caches go in the **app-data folder** by default; an opt-in portable sidecar mode is deferred | Privacy and least surprise (design and security engines); K27 |
| D-05 | Local semantic search: **keep the provider seam; offer Ollama detection and setup guidance first**; bundling an ONNX model is re-evaluated in Phase 14 after licence and size review | Avoids an unreviewed model download; the currentness gate is required |
| D-06 | AI providers: a **local-only / offline deterministic mode first**; a cloud provider profile is configurable by the user with their own key; exact model IDs come from the Phase 15 currentness review | Fail-closed privacy doctrine |
| D-07 | Icons: use an **open-licence, consistent colourful icon set with recorded licence** as the interim; the owner's premium PNG purchase is a drop-in later | Purchasing needs the owner's payment; no untraceable assets |
| D-08 | Installers: **keep ADR-0009** (Velopack plus MSIX plus notarised DMG) | Accepted ADR |
| D-09 | Classroom clients: **stream, with a bounded, encrypted, expiring cache** | Privacy of minors (DPIA) |
| D-10 | Extensibility: **defer the plugin loader and local API**; Calibre/Zotero import is optional in Phase 25 | Scope control |
| D-11 | Teacher dashboard: a **minimal** view (connected clients, published content) | CLIENT-012 minimum |
| D-12 | Beta cohort: plan and protocol prepared; **recruiting and running it is NOT ASSESSED** until the owner recruits participants | Needs real people |

## Entries

- **2026-09-25** — Audit and plan committed (`d9585a2`) and pushed. Temporary files cleaned.
- **2026-09-25** — Phase 00 started. T00.1: tests restored (D-01); the solution builds in Release with 0 errors and 0 warnings.
- **2026-09-25** — Phase 00 COMPLETE (commits `b11bd14`..`e2606bc`). Fast suite 1,149 tests in 4.5 min; format gate green; OCR 10/10; no temp or docs pollution. See `docs/implementation/execution/phase-sept23-00-completion.md`.
- **2026-09-25** — Phase 03 in progress in the main tree. Done: pager relocated (T03.1), layout and status tests written (T03.2, T03.4), filtered-empty state (T03.3), wrap-panel toolbar stopgap with a More menu (T03.5), `Brush.Accent.OnAccent` token. Next: build, run UI tests, real-window verification, commit.
- **Execution strategy:** the machine has 4 cores, so at most two parallel lanes (worktree agents) plus the coordinator. Wave A lanes after Phase 03: {02 crash safety, 04 reader engine} then {01 harness}.
- **2026-09-25** — Phase 03 COMPLETE (`5c3b9b2`; whitespace follow-up `31ab521`). The catalogue and first run are visible in the real window; the layout guard fails on the old nesting. Phase 02 is running in a worktree lane (agent). Next: Phase 04 lane.
- **2026-09-25** — Lanes in flight (worktree agents; branches are local to this machine until merged): **Phase 02** (crash safety, logging, threading, single instance) and **Phase 04** (reader engine: session CPU policy, async geometry, protocol request IDs, respawn, DPI-aware render). The coordinator merges each lane into `main` after re-running the gates. **If resuming after an interruption:** run `git worktree list` and `git branch -a`; for any lane branch with commits not on `main`, review its completion record, merge with `--no-ff`, re-run the gates, and push; relaunch unfinished lanes from their phase document. Phase 01 starts when a lane frees up.
- **2026-09-25** — Phase 04 merged. The reader no longer crashes: 470 turns of the 900-page book, worker-kill recovery under 2 s, UI-thread page-turn p95 6 ms (was a stall of 11.4 s), sharpness ×3. Merge fixes: 4 new broad catches were marked or logged to satisfy Phase 02's guard. Merged gates: 1,241 tests pass (48/1018/175). Merges now use `chore(merge):` subjects (the hook rejects `merge:`). Only Phase 01 (harness) remains in Wave A; its lane is running.
- **2026-09-25** — Phase 05 merged (IMPLEMENTED; the E2E journeys wait for Phase 01). Real window: 13/13 renderable books show real covers; empty, HTML and truncated files are in Needs attention; the password PDF is Locked; multi-root add/remove works; the watcher picks up a new file in 2.7 s; nothing is written into the library folder (assets in app-data per D-04, ADR-0018). Merged gates: 1,277 tests pass. Next lane: Phase 06 (pipeline and jobs).
- **2026-09-25** — A usage limit (reset 13:50 EAT) interrupted both lanes. Phase 01 had 13 commits including its completion record; Phase 06 had uncommitted work in progress. Both were resumed with their context after the reset.
- **2026-09-25** — Phase 01 merged: FlaUI UIA3 harness (ADR-0017), journeys G1–G8, deterministic corpus and oracle, AutomationIds, readable list-item names (K14 fixed), E2E-only folder-picker hook, informational `e2e-windows` CI job. Its baseline was measured on pre-04/05 code; the **next step is to re-run `Invoke-GoldenJourneys.ps1 -All` on merged `main`** once the Phase 06 lane's real-window run is finished (the two would fight for the foreground). New defects K93–K95. Merged gates: 1,281 tests pass.
- **2026-09-25** — The journey re-baseline on `main` (run 20260925-114848): G1 and G5 PASS, catalogue accessible names PASS; G2 and G6 fail on harness/product contract gaps (Needs-attention AutomationId, Locked counted as invalid in the oracle), which the coordinator aligns after Phase 07. Coordinator fix `936c587`: bounded shutdown (LaunchCycles hang), engine-recovery Warning log (G8), corrupt-settings quarantine and notice (K95). K96 registered (load-sensitive timing test).
- **2026-09-25** — Phase 06 merged. Real window vs baseline: failed jobs 17→0, attempts 269→65, all covers 84 s→27 s, processing done 163 s→64 s, worker launches 129→34, ISBNs 0→5, the duplicate grouped into 1 book with 2 locations, editions populated. Merged gates: 1,320 tests pass. Next: merge Phase 07, align the E2E contracts, re-run journeys, then Phases 08 and 09 in parallel.
- **2026-09-25** — Phase 07 merged: navigation rail with one destination at a time, filter chips, capability-gated entries, Advisor route to Settings (G7 now PASS), a 30-command palette. Owner-proxy decision **D-13**: the IA wireframes were approved under delegated authority; they are queued for the owner walkthrough. Merge conflicts in MainShellViewModel/ShellModule (Phase 06 processing progress vs Phase 07 capabilities) resolved by keeping both sides. Merged gates: 1,340 tests pass. Phase 17 (OCR) lane running. Next: align the E2E contracts for G2/G6, re-run the journeys, then launch Phase 08 (Settings) and Phase 09 (design system).
- **2026-09-25** — Phases 17 and 08 merged (`a22706e`, `acb9d36`); OCR policy wired into Settings. Merged gates: 1,404 tests pass. The owner allowed real-window runs while at the desk. The harness no longer pins windows topmost (`7e342a6`); needs-attention notice plus G2/G6 contract alignment (`49f1fe2`) made G2 and G6 PASS.
- **2026-09-25** — **Owner feedback on visuals:** "I find the UI a bit dull, too greyish and boring. Amaze me!" The Phase 09 lane was redirected to the **"Lamplit Library"** identity (owner-proxy decision **D-14**): a deep ink rail with per-destination jewel-tone colours; a warm parchment canvas with floating cards; colourful duotone icons (licensed, or drawn in-repo); cloth-bound generated covers with gilt Spectral titles; a welcoming continue-reading header; purposeful motion that respects reduced-motion; and a rich "Night Library" dark theme. Early concept screenshots will go to `evidence/sept-23-kaizen/phase-09/concept/` for owner reaction. Lanes running: 09 and 13.
