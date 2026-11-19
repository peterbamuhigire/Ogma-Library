# Sept-23 Phase 13 completion: unified, trustworthy search

Status: **IMPLEMENTED, not COMPLETE** (close-out 2026-11-19; G4 6/6 and A11yNames 6/6 at both
sizes, ScanOcrSearch passes since K99 was fixed; open in this phase: the K40 phrase miss's cause and
the J-SRCH-1 journey; see [Close-out 2026-11-19](#close-out-2026-11-19)). First recorded
2026-09-27. Unit, integration, headless UI, relevance oracle and performance checks pass.
Conceptual (semantic) queries are NOT ASSESSED (Phase 14).
Rollback point: `acb9d36` (the Phase 08 merge). No schema migration and no index rebuild
(ADR 0020, decision 4).
Plan: [phase-13](../../plans/sept-23-kaizen/phases/phase-13-unified-search.md).
Defects: K40 (search misses), K41 (misleading status and first-query race), K14 (search result
names). ADR: [0020 unified search contract](../../adrs/0020-unified-search-contract.md)
(renumbered from 0019 to avoid a collision with the Phase 09 ADR).

## Commits

| Commit | Slice |
|---|---|
| `bc431a1` | test: fail-first relevance oracle over the synthetic corpus (red: 15 of 16) |
| `7db6aaa` | feat: unified search pipeline with parser, normaliser, FTS match builder, metadata matcher and RRF fusion |
| `af6556a` | feat: route the Search destination through `IUnifiedSearchService` (view model, results UI, en/fr copy) |
| `755ec83` | perf: bound FTS joins and cache the catalogue snapshot |
| `330edd7` | fix: refresh the cached catalogue after metadata edits |
| `f10a05e` | docs: ADR for the unified search contract |
| `b8ab8a1` | fix: refresh an open query while books are still being prepared |
| `4ca23f2` | fix: keep an open query live after a scan; G4 waits for PDF metadata before probing |
| `5beced8` | docs: renumber the ADR to 0020 |
| `951090f` | fix(i18n): French accents in the search panel copy |
| `ab244c5` | feat: link index coverage gaps to Activity (13.8) |
| `8e81e0d` | test: oracle precision, recall and MRR |
| (this record) | layout fix (results fill the destination), baseline and register updates, completion record |

## Tasks

| Task | Result |
|---|---|
| 13.1 Oracle first | Done. `SearchRelevanceOracleTests` (audit battery plus the four `expected.json` G4 probes) was committed red: 15 of 16 cases failed on the old pipeline (CODE, commit message of `bc431a1`). Green now. |
| 13.2 Query parser | Done. `SearchQueryParser`: bare words, `"phrases"`, `title:`, `author:`, `isbn:`, `year:` (single, ranges, `>`/`<=`), `tag:`, `shelf:`, `text:`, `note:`, `toc:`, `-` exclusion, 500-character truncation with a notice. `SearchTextNormalizer` folds case and diacritics (NFKD, plus ß, Ł and similar) for matching and keeps the original for display. 30-row parse table plus fold, edit-distance, typo-budget, FTS and year tables. |
| 13.3 FTS match builder | Done. `FtsMatchQueryBuilder`: AND of quoted terms, prefix on the last term, phrases kept, exclusions as `NOT`; every token is quoted so user input cannot inject FTS5 syntax (`"`, `*`, `NEAR(`, `:`, `title:` never throw). The tokenizer stays `unicode61 remove_diacritics 1`, which already folds `ũ` and `É` for index and query alike, so no migration or rebuild was needed. `dhow Zanzibar` and `Chwezi dynasty` pass. |
| 13.4 Metadata search | Done. `MetadataMatcher` over a folded catalogue snapshot: titles resolved like the catalogue projection (title, best metadata title, file name), authors, ISBN, tags, shelves; every word must match, prefixes count; typo tolerance is a fallback with edit distance 0/1/2 by length (≤3 / 4–7 / ≥8). `algoritms`, `Wanjiru`, `Wanjirũ` pass. |
| 13.5 `IUnifiedSearchService` | Done. New versioned contract `unified-search-v1` in Application, `UnifiedSearchService` in Infrastructure, bound in the composition root; frozen v1 contracts untouched. Structured filters → metadata + FTS (+ semantic when the probe answers within 250 ms, cached 60 s) → RRF (k = 60) → one result per canonical book with hit locations. The response carries `SemanticSearchState`, typo-tolerance use and index coverage. |
| 13.6 View-model pipeline | Done. Versioned, cancellable request loop on the UI thread (debounce 150 ms). A newer request cancels the older; a stale response is never applied; failures are logged and shown as an error state with *Retry*. Mode chip and status come from the response. An open query is refreshed every 2 s while books are still being prepared (≤ 10 min) and for 90 s after it opens, so PDF metadata read after a scan appears without retyping. `SearchNowAsync`/`RetryAsync` complete when the first response is applied. |
| 13.7 Results UI | Done. Title, "by Author", snippet with highlighted terms (`SnippetHighlight`), match badges (Page N, Title, Author, close spelling, OCR text), *Open at page N*, keyboard navigation (Down from the box into results, Enter opens), accessible names "Title, by Author, matched on page N" on the `ListBoxItem` (K14). Empty state: "No books match “q”. Check the spelling, remove filters such as author: or try fewer words." Results now fill the destination (the old 220 px cap centred the list in the full-height page). |
| 13.8 Coverage indicator | Done. Header: "N of M books have searchable text; K need OCR; P still being prepared" from the catalogue snapshot (`Coverage_CountsSearchableAndScannedBooks` checks DB counts). New: when books have no searchable text, a *Show books without searchable text* button opens Activity (Index Manager and OCR queue). |
| 13.9 Performance | Done. `UnifiedSearchPerformanceTests` (`Category=Benchmark`), 2,000 books × 10 pages, 60 keyword queries: **p50 33.6 ms, p95 192.7 ms, max 276.4 ms** (budget p95 ≤ 300 ms). MEASURED, this machine, 2026-09-27. |
| 13.10 Global shortcut | Done with a deviation: Phase 07 objective 7 assigns Ctrl/Cmd+K and Ctrl/Cmd+Shift+P to the command palette, so global search is **Ctrl/Cmd+F** (and Ctrl/Cmd+2), which navigates to Search and focuses the box from anywhere (`SearchBar_CtrlF_OpensSearchAndCtrlK_OpensPalette`). J-SRCH-1 as a separate journey was not written; G4 covers query → results in the real window. |

Exit criteria (section 3): 1 pipeline — met (fusion is the contract's own RRF, k = 60, not the
frozen `HybridRankingService`, to keep `rrf-v1` unchanged; recorded in ADR 0020). 2 query
language — met. 3 oracle — met. 4 honest status — met; wording differs from the plan's
examples ("Titles, authors and full text · Semantic search off" and a tooltip pointing to
Settings) but is driven by the measured response. 5 race-free — met (50-repetition regression
test). 6 results — met. 7 p95 — met.

## Tests added or changed

- `SearchRelevanceOracleTests` (17 cases), `SearchRelevanceMetricsTests` (1).
- `SearchQueryParserTests` (30-row parse table and 7 more theories/facts).
- `UnifiedSearchServiceTests` (14): availability with provider absent / available / preparing / failing, fusion, filter-only queries, OCR labelling, duplicate copies, metadata-edit refresh, coverage counts, structured and prefix queries, exclusion, typo reporting, FTS syntax-breaking input.
- `UnifiedSearchPerformanceTests` (Benchmark).
- UI `SearchViewModelTests`: debounce and open-at-page, honest keyword mode, empty state, stale response, error and Retry, first query after open ×50, live refresh while preparing, keyboard into results, coverage review link (2 new).
- E2E `G04SearchTests`: waits for PDF metadata before probing; probes and assertions unchanged.

## Relevance oracle metrics (MEASURED, synthetic corpus, 14 judged queries + 1 negative)

| Pipeline | P@1 | P@3 (of shown) | R@3 | R@10 | MRR | Nonsense query empty |
|---|---|---|---|---|---|---|
| Before: `bc431a1` (old `SemanticSearchService` → combined fallback) | 0.000 | 0.000 | 0.000 | 0.000 | 0.000 | yes |
| After: unified pipeline (this branch) | **1.000** | **1.000** | **1.000** | **1.000** | **1.000** | yes |

Before, 7 of 14 queries returned nothing (`dhow Zanzibar`, `Chwezi dynasty`, `algoritms`,
`author:Okello`, `Zanzibar monsoon`, `Algoritms Explaned`, `amber library lantern`) and the other
7 returned a result titled *Untitled* (the old response carried `Books.Title`, which a scan never
fills). Measured by running the same metrics test in a temporary worktree at `bc431a1`.

## Gates (Windows 11, Release, 2026-09-27)

| Gate | Result |
|---|---|
| `Test-RequirementAccountability.ps1` | pass (101 FRs, 29 NFRs, 32 controls) |
| `dotnet restore --locked-mode` | pass |
| `dotnet format --verify-no-changes` | 0 changes |
| Release build | 0 warnings, 0 errors |
| Vulnerable packages | none |
| `dotnet format analyzers --severity warn` | 0 |
| `dotnet test -m:1 --filter "Category!=Performance"` | Architecture 52/52; core 1,257/1,258; UI 221/221. The one failure is `PdfWorkerSessionStabilityTests...WithinTwoSeconds` (recovery 2,080 ms vs 2,000 ms; reruns 1,930 / 1,856 / 2,076 ms): timing-flaky, not on the search path (K96, Phase 04). The E2E project was stopped: this filter also launches the real-window journeys outside the desktop lock (K98); they were run through the lock instead (below). |
| Search subset (`FullyQualifiedName~Search&Category!=Performance`) | 234/234; UI search 24/24 (before the 2 new UI tests) |
| Search benchmark | p95 192.7 ms (see 13.9) |

## Real-window journeys (desktop lock, isolated data, synthetic corpus)

| Run id | Journey | 1280×800 | 1920×1080 | Notes |
|---|---|---|---|---|
| `phase13-g4-final` | G4 | **PASS (baseline cleared)** | **PASS (baseline cleared)** | All five probes found in the top 3 in 0.5–0.9 s each (author probe 8.9 s at 1280 while metadata was still being read) |
| `phase13-g4-final` | A11yNames search results | FAIL (UIA `COMException`) | PASS (baseline cleared) | |
| `phase13-a11y-rerun` | A11yNames search results | PASS (baseline cleared) | — | Rerun of the COM failure |
| `phase13-g4-final` / `phase13-a11y-rerun` | A11yNames catalogue items | PASS / FAIL (empty names) | PASS | Intermittent, catalogue side of K14 (Phase 10/21), not search |
| `phase13-g4-final` | ScanOcrSearch | FAIL (window did not appear in 30 s) | FAIL (OCR text never indexed) | K99, Phase 17 |

The UI-thread violations counted in these runs (128–216) are all `ShelfSidebarViewModel` (K93,
Phase 06); none come from search.

Screenshots: [before 1280](evidence/sept-23-kaizen/phase-13/g4-before-search-1280x800.png)
(an intermediate run on 2026-09-25, author and Unicode probes failing),
[after 1280](evidence/sept-23-kaizen/phase-13/g4-after-search-1280x800.png),
[before 1920](evidence/sept-23-kaizen/phase-13/g4-before-search-1920x1080.png),
[after 1920](evidence/sept-23-kaizen/phase-13/g4-after-search-1920x1080.png);
[run summary](evidence/sept-23-kaizen/phase-13/results-phase13-g4-final.md).

`baseline.json`: the G4 and A11yNames search-result entries (K40, K41) are removed; both passed
at both sizes.

### Full suite regression run (`phase13-all`, `-All`, both sizes, desktop lock, `-NoBuild` on the `phase13-g4-final` app build)

| Journey | 1280×800 | 1920×1080 | Phase 13 related? |
|---|---|---|---|
| G1 | PASS | PASS | |
| G2 | FAIL (UIA `COMException`) | PASS | No: harness/UIA transient; passed at 1920 and in Phase 05 |
| G3 | BASELINE-FAIL (K32, K94) | FAIL (catalogue grid "covered" after return) | No: reader and harness paint check (Phase 04) |
| G4 | **PASS** | **PASS** | Yes, cleared |
| G5 | PASS | FAIL ("Could not leave page 2") | No: reader page turn (K32/K94, Phase 04) |
| G6 | PASS | PASS | |
| G7 | PASS | PASS | |
| G8 worker kill | PASS (baseline cleared, K30) | PASS (baseline cleared, K30) | No; Phase 04 should remove that entry |
| G8 corrupt settings | BASELINE-FAIL (K95, K28) | BASELINE-FAIL | No |
| G9–G12 | NOT ASSESSED | NOT ASSESSED | Owned by later phases |
| A11yNames (catalogue, search) | PASS, PASS | PASS, PASS | Search names cleared |
| Navigation (3 tests) | PASS, PASS, FAIL (`Shell.Nav.Library` "covered by Image") | PASS ×3 | No: paint-check hit on the rail icon |
| Settings (2 tests) | PASS, PASS | PASS, PASS | |
| ScanOcrSearch | FAIL (K99) | FAIL (K99) | No: OCR text never indexed (Phase 17) |
| Smoke | FAIL (exit > 10 s after WM_CLOSE) | — | Not shown to be: startup-close hang K71 (main recorded 3/20 LaunchCycles hangs) |
| LaunchCycles | FAIL (19 of 20 closes needed a kill) | — | Not shown to be: K71. Worse than main's 3/20; the machine was loaded by other lanes. No search code runs at startup without a query, and the Smoke log ends with `app.exit.completed`. A same-machine A/B against main was not run (NOT ASSESSED, coordinator) |
| Harness self-test | FAIL (paint detector "covered") | — | No: harness self-test |

## Typeface and design

No new fonts or icons. Text uses the theme's Public Sans body tokens (`Type.Size.Body`,
`Type.Size.Caption`); colours come from `Brush.Surface.*` and `Brush.Accent.*` tokens; the new
coverage button reuses the Retry button pattern. Snippet terms are highlighted with bold weight only (no hard-coded colour).

## Deviations

- Global search shortcut is Ctrl/Cmd+F, not Ctrl/Cmd+K (Phase 07 owns Ctrl+K for the palette).
- Fusion uses the unified contract's own RRF instead of the frozen `HybridRankingService`.
- No tokenizer migration: `remove_diacritics 1` already folds the corpus's precomposed letters.
- The open query keeps refreshing for 90 s after it opens (every 2 s, from the cached snapshot)
  so metadata that arrives after a scan appears. Cost: at most 45 extra in-memory queries.

## NOT ASSESSED

| Item | Owner | Why |
|---|---|---|
| Conceptual (semantic) queries, J-SRCH-3 with a provider | Phase 14 (D-05) | No embedding provider in this environment |
| 50k-book latency on reference hardware | Phase 24 | 2k measured locally |
| Human-labelled relevance set | Phase 16/28 | The synthetic oracle is necessary, not sufficient |
| Image-only text (`amber library lantern` in the scanned pamphlet), ScanOcrSearch | Phase 17 (K99) | OCR output does not reach the index in the real app |
| J-SRCH-1 (shortcut → query → open at page → back) as a dedicated journey | Phase 13 follow-up / Phase 01 harness | Not written; covered by headless tests and G4 |
| French culture real-window run of the search destination | Phase 21 | Only en was run in the real window; fr strings are unit-checked |
| Screen-reader (assistive technology) check of result names | Phase 21 | UIA names checked; no AT run |
| macOS Cmd+F | Phase 27 | Windows only |
| Same-machine A/B of LaunchCycles/Smoke against `main` (were close hangs worse on this branch?) | Coordinator (K71, Phase 24) | 19/20 here vs 3/20 recorded on main, under load from other lanes |
| Real-window screenshot after the results-list height fix | Coordinator re-run of G4 | The fix landed after the journey runs; headless UI tests cover it |

## Phase CO slice 5 (2026-11-19): journeys on the close-out head

- G4 passed at 1280x800 and 1920x1080 in Light and Dark in `co-nav-1` and `co-nav-2`. Together with slice 2's `co-all-1` to `co-all-3`, that is 7 consecutive passes per size.
- The "Grace Namutebi" author-probe flake did not reproduce. Every probe was found in 0.5 to 4.4 s. The `SearchViewModel` request loop was reviewed and needs no change: stale responses are dropped by version, and the live refresh re-runs while books are pending. No fix is claimed.
- The `ScanOcrSearch` failure in the status line above is superseded. It now passes, and its baseline entry was removed (`912977c6`; K97 and K99 resolved; see Phase 17 evidence).
- The search-result badge label now uses `Brush.Accent.Text` (K113, `1fc796ea`), so it is readable in Dark.

## Close-out 2026-11-19

Full record: [phase-sept23-CO-completion.md](phase-sept23-CO-completion.md#slice-6-record-and-evidence-final-head).
Gates on `ebb7b718`, all PASS (MEASURED): accountability (162 IDs), locked restore, format, Release
build (0 warnings), vulnerable-package scan (11 of 11 clean), analyzers, and `scripts/Test-Fast.ps1`
1,677 of 1,677. Journey runs `co-final` (`-All`, both sizes, Light: 44 PASS, 2 BASELINE-FAIL for
G3, 0 FAIL, 8 NOT ASSESSED placeholders) and `co-final-g2`. Stability rule D-17: 3 consecutive passes
at both sizes. Typeface unchanged: Spectral, Public Sans and JetBrains Mono.
| Journey | co-final 1280x800 / 1920x1080 | D-17 streak | Result |
|---|---|---|---|
| G4 search (title, author, body phrase, typo, Unicode) | PASS / PASS | 6 / 6 | met |
| A11yNames (catalogue, search results) | PASS / PASS | 6 / 6 | met |
| ScanOcrSearch (OCR hits labelled, K99 fixed) | PASS / PASS | 5 / 6 | met |

The NOT ASSESSED rows above for ScanOcrSearch (K99) and the post-fix G4 screenshot are now done.
Evidence: [`evidence/sept-23-kaizen/phase-13/closeout/`](evidence/sept-23-kaizen/phase-13/closeout/).

**Status: IMPLEMENTED, not COMPLETE.** Open in this phase: (1) K40, the cause of the one
multi-word phrase miss in `co-all-1` is NOT_ASSESSED; it has not recurred in 15 later ScanOcrSearch
rows; (2) the J-SRCH-1 dedicated journey is not written. Owner: Phase 13. Rollback point: `ebc9bf6e`.