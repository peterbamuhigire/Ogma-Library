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
- **2026-09-25** — A usage limit (reset 18:50 EAT) interrupted the Phase 09 and 13 lanes; both were resumed. The real-window run `20260925-145558` on `main`: Settings PASS, G8 worker-kill PASS (baseline cleared), and G1, G2, G5, G7 and Navigation PASS. Failures went to a new **stabilisation lane**: K93 off-thread sidebar, pre-migration DB queries, LaunchCycles close hang, the G8 corrupt-settings notice, OCR NoText on one fixture, the garbled locked-PDF message, and a G6 flake. MILESTONES.md updated.
- **2026-09-27** — Session resumed. Three interrupted lanes were found with commits and uncommitted work: **Phase 09** (Lamplit; 10 commits, completion record draft), **Phase 13** (unified search; 7 commits), and the **stabilisation lane** (5 commits; K93, migration gate, K71 close, locked-PDF message, OCR fixture). At the owner's request, three subagents were relaunched, one per existing worktree, with explicit finish-and-verify instructions. Phase 13 renumbers its ADR to 0020 so that it does not collide with Lamplit's ADR-0019. Phase 09 merges `main` into its lane before verifying. The status register was corrected: Phases 08 and 17 are IMPLEMENTED (merged 2026-09-25).
- **2026-09-27** — Owner request: an About dialog crediting **Peter Bamuhigire, Lead Developer** (www.techguypeter.com, peter@techguypeter.com, +256 784 464178) and linking the Ogma website https://ogma.techguypeter.com/, made beautiful in the Lamplit direction. Built in its own lane and merged (`4c94be38`). It is reachable from Settings ("About Ogma Library") and the command palette. Links are allow-listed to those four URIs only. Real-window screenshots (Light and Dark) are in `docs/implementation/execution/evidence/sept-23-kaizen/about/`. Gates on merged `main`: build 0 warnings; Architecture 52/52; UI 243/243; core 1,151/1,152. The single failure is K96 (worker recovery 2,850 ms against a 2 s budget while three lanes loaded the 4-core machine; unrelated to About). A network outage interrupted all four lanes; each was resumed with its context intact.
- **2026-11-19** — Phase 09 slice 3 (executor session). Owner-proxy decision **D-15** (the work order's recommended option): the TabStrip **overflow menu is built now** as a style-level control — every `TabControl` gets a trailing, keyboard-reachable "More tabs" button (`Controls/TabOverflowButton`, en+fr name `Controls.TabStrip.More` published as an app resource by `Themes/StyleStrings`) that lists the tabs not fully in view and selects one on click; **adoption and verification in the detail inspector and reader side panel stay with Phases 11/12**. No Lucide "ellipsis" glyph is vendored in `scripts/icons/lucide-subset.json` and fetching one was out of bounds, so the button draws three token-filled dots (no new asset). Evidence: headless tests `Sept23Phase09TabOverflowTests` (8 pass); real-window check NOT_ASSESSED in this slice.
- **2026-11-19** — Phase 09 slice 5 (executor session): **IMPLEMENTED**, not COMPLETE. The full gate set on the lane head passes except `Test-RequirementAccountability.ps1`, which throws because `main` archived the `aug-39` matrix (K100). The first locked journey run found a **Phase 09 regression (K106)**: the 2 px focus ring was a border on the focused button, so a focused *Filter* pushed *Folders* into overflow at 1280 and the Navigation drawers journey failed in both themes. Fixed in `5520c82c` (the ring is now a layout-neutral `FocusAdorner`; red-then-green headless test). The re-run `phase09-final2` on the fix passes every assessed journey in Light and Dark at both sizes (78 PASS, 0 FAIL, 16 NOT ASSESSED placeholders), including G3 and G4, which failed on the previous build under load; one green run does not close K32 or K40. 22 Light/Dark screenshots at both sizes, plus the gallery, replace the stale evidence. The TabStrip decision is D-15 (above). K105 (fast suite back to 18 min) was logged for Phase 24. Open: K100, the owner walkthrough, hosted CI, and the accessibility runs (Phase 21). [Record](../../implementation/execution/phase-sept23-09-completion.md).
- **2026-11-19** — Phase 09 review rework (executor session). Decision (the plan's own text, task 9.4 "debug builds only"): the component gallery gesture Ctrl+Shift+Alt+G is now compiled into **Debug builds only** (`#if DEBUG` in `DesktopShellWindow.axaml.cs`); the lane had opened it in Release. The gallery types stay compiled so the Release headless gallery tests still run; future gallery screenshots need a Debug build. Guard test `ComponentGalleryGesture_IsCompiledIntoDebugBuildsOnly`. Also added the missing home-strip, card-sync and QR tests (`Sept23Phase09HomeStripTests.cs`), marked K100 resolved by `82f88e23`, and recorded that `82d25bf1` made the lost PDF worker's dispose fire-and-forget with no test for a throwing or exit-interrupted dispose (Phase 24). Gates and journeys on the final head: NOT ASSESSED (driver).
- **2026-11-19** — Phase CO slice 1 (executor session). Owner-proxy decisions, the work order's recommended options: **D-16** the canonical close-out record is `phase-sept23-CO-completion.md` (runner contract); `phase-sept23-closeout-completion.md` is a pointer to it, to satisfy the phase note. **D-17** (the plan is silent) a `baseline.json` entry is removed only after 3 consecutive passes at both sizes. Slice 1 resolved K102 (`Uia.Retry` on list-item lookups and the UIA tree walks), K103 (journey script survives redirected output) and K98 (AGENT_BRIEF gate filter). Run `co-s1`: Harness, Smoke, G2 9 PASS, 0 FAIL at both sizes; the earlier `co-s1-run1` failed G2 1280x800 on an unretried snapshot walk, now fixed.
- **2026-11-19** — **Close-out 2026-11-19** (Phase CO slice 6, executor session, head `ebb7b718`, rollback `ebc9bf6e`). Decisions applied: **D-16** (canonical record `phase-sept23-CO-completion.md`; `phase-sept23-closeout-completion.md` is a pointer) and **D-17** (a `baseline.json` entry is removed, and a journey counts as stable, only after 3 consecutive passes at both sizes). The full gate set passes on `ebb7b718`, including `scripts/Test-Fast.ps1` 1,677 of 1,677. Final locked runs: `co-final` (`-All`, both sizes) 44 PASS, 2 BASELINE-FAIL (G3), 0 FAIL, 8 NOT ASSESSED placeholders; `co-final-g2` 2 PASS. Outcome: Phases **05, 08 and 17 COMPLETE**; **02** IMPLEMENTED (G3: K32, K94, K109); **06** IMPLEMENTED (K109); **07** IMPLEMENTED (owner walkthrough, D-13); **13** IMPLEMENTED (K40 miss cause, J-SRCH-1). Screenshots are in each phase's `closeout/` folder. NOT ASSESSED with owners: owner walkthrough (Owner), macOS (27), screen reader and accessibility variants (21), signing (26), hosted CI (driver), Roots and loose-book journeys (10), Watcher, SingleInstance and cancel journeys (24), J-SET-2 (11). [Record](../../implementation/execution/phase-sept23-CO-completion.md).
- **2026-11-19** — Phase 10 slice 1 (executor session): composed accessible names, virtualised grid and cover-unavailable badge. Owner-proxy decisions (the work order's recommended options): **D-18** proceed with Phase 10 although Phases 06 and 09 are IMPLEMENTED, not COMPLETE; **D-19** virtualise with an in-repo `VirtualizingUniformGridPanel` inside the existing ListBox (no new package); `OGMA_CATALOGUE_VIRTUALIZE=0` restores `WrapPanel`; **D-20** one criteria model with read-model and view-model parity; server-side paging moves to Phase 24; **D-21** exact-hash copies show "2 copies" and Compare; merge and split act on work groups; **D-22** moving IngestionFailure rows to FileIssues moves to Phase 11. Results: K14 names composed in the grid, list, directory and home strip; Show all on by default and persisted; 42 containers realised at 2,000 books; corpus `--count N` (default output still matches the manifest); `Uia.FindListItem` scrolls to items. Real-window G2 1280x800 failed 3 times on a recycling defect (refreshes moved books between containers that UIA held) and, after the fix, PASSED twice. Gates: build, format, design tokens and the fast suite (1,684 of 1,684) pass. Real-window frame times NOT_ASSESSED; headless median scroll step 3-5 ms. K114 logged (cover-failure reason not persisted). [Record](../../implementation/execution/phase-sept23-10-completion.md).
- **2026-11-19** — Phase 10 slice 2 (executor session): shared filter criteria, read-model predicates, new sorts and view state restored once. Applies D-18 to D-22 (already recorded in slice 1). The slice assumed a date-added value already existed, but none was stored. New owner-proxy decision **D-23**: add a nullable `Books.AddedUtc` column, set by registration. Existing books stay unknown and sort last; no date is invented. The release schema freeze advances deliberately from 44 to 45 migrations, with a backup-and-restore rehearsal (`BookAddedUtcMigrationRehearsalTests`). Results:
  - `CatalogueFilter` carries every criterion, and `CatalogueCriteriaEvaluator` is the in-memory twin. A parity theory binds it to `CatalogueReadModel`.
  - `CountAsync` was added.
  - Smart shelves round-trip every criterion, and a damaged query fails closed.
  - Sort labels and the sort direction are localised (English and French).
  - The fast suite passes: 1,763 of 1,763.
  - Real-window journey: NOT_ASSESSED.

  [Record](../../implementation/execution/phase-sept23-10-completion.md).
- **2026-11-19** — Phase 10 slice 3 (executor session): full filter bar, chips for every criterion, explained no-results state and smart shelves from filters. New owner-proxy decisions (the plan's recommended or least-surprise options):
  - **D-24**: the plan's "status (reading state)" is a premise error. The criteria model's status is the lifecycle status (Active, Unavailable, Excluded), and the bar exposes that.
  - **D-25**: "Save as smart shelf" is disabled, with a hint, when no filter is active or when a collection filter applies, because smart-shelf conditions cannot express shelf membership.
  - **D-26**: `ShelfProjection` carries a smart shelf's saved `Query`, so the in-memory grid evaluates a selected smart shelf exactly as the read model does.

  Results:
  - The tests in `Sept23Phase10FilterBarTests` pass, 8 of 8.
  - The fast suite passes, 1,771 of 1,771.
  - Build, format and design tokens pass.
  - Navigation journey at 1280x800: run `p10-s3-nav` had 1 failure, a pre-existing foreign-toolbar occlusion also seen in `co-all-1`. Re-run `p10-s3-nav2` passed 3 of 3.
  - K115 was logged: the sidebar holds an orphan filter instance.

  [Record](../../implementation/execution/phase-sept23-10-completion.md).
- **2026-11-19** — Phase 10 slice 4 (executor session): multi-select, selection toolbar and shelf membership. New owner-proxy decisions (the plan's recommended or least-surprise options):
  - **D-27**: the selection toolbar shows at two or more selected books, or at one when the Library is filtered to a manual collection (so Remove from collection applies).
  - **D-28**: drag targets are the Collections rows. Collections is not on screen with the Library, so the Collections rail item opens Collections while a drag is over it.
  - **D-29**: Mark as read uses `IBookCurationService.UpdateReadingStateAsync(Finished)`, not `BulkEditCommand.NewStatus`, which writes the lifecycle status (K116).
  - **D-30**: in multi-select the primary book (detail pane) is the first pick; the grid and list bind only `SelectedItems`, and shelves sync in place on refresh.
  - **D-31**: K115 is deferred to slice 5. Sharing the Library filter with the sidebar now would let a background reload, which replaces shelf rows, clear the Library's collection filter; it needs id-stable shelf selection first.

  Results:
  - Real-window J-CAT-1 and J-CAT-2 PASS at 1280x800 under the desktop lock (run `20261119-132548`), after four refresh-related defects were fixed (selection, focus, shelf flicker, empty menu) and the overlay inspector no longer hides the toolbar.
  - J-CAT-2's drag step and G10 are NOT_ASSESSED (injected input cannot start an OLE drag; G10 is still a placeholder).
  - `Sept23Phase10Selection` 12 of 12; the fast suite 1,783 of 1,783; build, format and design tokens pass.
  - K116 and K117 were logged.

  [Record](../../implementation/execution/phase-sept23-10-completion.md).
- **2026-11-19** — Phase 10 slice 5 (executor session): bulk edit with preview and undo (CAT-005). New owner-proxy decisions (the plan's recommended or least-surprise options):
  - **D-32**: the preview and the write share `BulkEditFieldRules`. This corrects the plan's premise that `UpdateMetadataFieldAsync` already validated values; it did not.
  - **D-33**: the bulk-edit form offers author, year and rating only. Lifecycle status stays internal (`NewLifecycleStatus`, K116 resolved) and reading state stays with curation (D-29).
  - **D-34**: undo restores a snapshot of every touched field, captured before the write, rather than replaying an inverse edit.
  - **D-35**: each applied edit has its own Undo on the shared LIFO history, exposed only for the newest edit. A stale toast Undo does nothing, and a failed undo marks the Activity entry unavailable.
  - **D-36**: notification toasts sit 112 px above the bottom edge so they clear the selection toolbar (K117).

  Results:
  - The first real-window J-CAT-3 run (`20261119-140258`) failed: the preview row ids were on a `Border`, which has no automation peer. The ids moved to the row title, and a headless test now asserts the peer.
  - J-CAT-3 and the other catalogue journeys PASS at 1280x800 under the desktop lock (runs `20261119-141118` and `-141641`). The journey harness can now capture a dialog window.
  - `Sept23Phase10BulkEdit` 9 of 9; the fast suite (Tests 1,327, Ui 413, Architecture 52); build, format and design tokens pass.
  - G10, K115 and the other widths, themes and accessibility checks are NOT_ASSESSED or open.

  [Record](../../implementation/execution/phase-sept23-10-completion.md).
- **2026-11-19** — Phase 10 slice 6 (executor session): copies and editions badge, Compare, Merge and Split with Undo (CAT-007). New owner-proxy decisions:
  - **D-37**: a reviewed work group shows as one card (the lowest book id), collapsed after the query and paging, as edition groups already are.
  - **D-38**: Merge creates a work from ungrouped books, adds books to the one work already selected, and refuses books from two different works with a message. Split dissolves the whole work.
  - **D-39**: Undo checks that the group version still matches what this operation produced; a change made elsewhere is shown as a conflict, not overwritten. The check is optimistic, because the service has no expected-version parameter.
  - **D-40**: "Possible duplicates" and Library Health share one rule set (`DuplicateCandidates`); the catalogue also flags a book held as two or more files.
  - **D-41**: Keep separate is omitted until it can be persisted (K118, Phase 11).

  Results:
  - J-CAT-4 PASS at 1280x800 under the desktop lock (runs `20261119-150819` with every catalogue journey, and `20261119-151646` after the Compare title fix).
  - `Sept23Phase10Identity` 7 + 3; the fast suite (Tests 1,333, Ui 416, Architecture 52); build, format and design tokens pass.
  - The stale card after a merge (`IsSameCard` ignored the identity fields) and the journey runner's rejection of hyphenated journey names were fixed in this slice.

  [Record](../../implementation/execution/phase-sept23-10-completion.md).
- **2026-11-19** — Phase 10 slice 7 (executor session): library health dashboard, localisation key parity and a real G10 (META-008). New owner-proxy decisions:
  - **D-42**: the dashboard is a route owned by the Library destination (`RouteKind.LibraryHealth`), reached from Settings → Library, the status bar and the command palette (`library.health`).
  - **D-43**: each book tile counts with `CountAsync` over exactly the criteria it applies, and activating it replaces the whole filter (`ClearAll`) before showing the catalogue. Back returns without changing the filter.
  - **D-44**: Failed jobs opens an in-dashboard list with Retry; Needs attention opens the existing needs-attention list; batch Pause and Resume show only when metadata providers are enabled. Low quality means a quality score below 0.5.
  - **D-45**: no English fallbacks in view models. Without a localisation service the key is shown; `BookSummaryProjection.PrimaryAuthor` is null when no author is stored, and the view supplies "Unknown author".
  - **D-46**: every status badge explains itself through a `Catalogue.Badge.{X}.Help` tooltip, in grid and list.
  - **D-47**: G10 lives with the catalogue journeys in `CatalogueOrganiseTests` (shared helpers); the placeholder is removed from `LaterPhaseJourneyTests`. Dashboard tiles use a new theme variant `Button.tile`.
  - K119 was logged and fixed: five French values were mis-encoded (mojibake, for example "IndexÃ©"); `LocalizationKeyParityTests.NoValueIsMisEncoded` guards it.

  Results:
  - Build (0 warnings), format, design tokens and requirement accountability pass. The fast suite passes (Tests 1,350, Ui 417, Architecture 52); the slice filter passes 17 of 17.
  - G10 and J-CAT-5 at 1280x800 are **NOT_ASSESSED**. Run `20261119-155855` failed at launch ("main window did not appear"), while the owner's interactive desktop was running a full-screen game. A diagnostic launch showed the visible "Ogma Library" window, but `Process.MainWindowHandle` stayed 0. Real input was not injected again over a desktop in use.

  [Record](../../implementation/execution/phase-sept23-10-completion.md).
- **2026-11-19**: Phase 10 slice 8 (executor session), record and evidence. Phase 10 is **IMPLEMENTED, not COMPLETE**. New owner-proxy decision:
  - **D-48**: a real-window run does not start while the owner's interactive session is in use (input within the last 5 minutes). The journeys are recorded as NOT_ASSESSED rather than injecting input over the owner's desktop. `baseline.json` is unchanged: D-17 requires 3 consecutive passes at both sizes.
  - Preamble decisions D-18 to D-22 were recorded at the start of the phase and are restated in the record.

  Results on `aee9e2dd`:
  - The static gates pass: accountability (162 IDs), locked restore, format, Release build (0 warnings), the vulnerable-package scan (0 in 11 projects), analyzers and design tokens. The fast suite passes (Tests 1,350, Ui 417, Architecture 52).
  - `-Tag Catalogue -Sizes 1280x800,1920x1080` is **NOT_ASSESSED** under D-48: the owner was using the desktop, with the last input 2 s earlier and game tooling in the foreground.
  - The 2,000-book scroll in the real window is NOT_ASSESSED, because the harness has no frame timer. The headless proxy measured p95 212-250 ms and a worst step of 346-439 ms: HEURISTIC, over the target, and registered as K120 (Phase 24).
  - K119 was added to the register as resolved. K121 (failed-job rows not localised, Phase 11) is new. K14 is resolved for the catalogue, and K23 is partly resolved.
  - Open items to reach COMPLETE: the journeys at both sizes, G10 and J-CAT-5, screenshots, K120 and K121.

  [Record](../../implementation/execution/phase-sept23-10-completion.md).
