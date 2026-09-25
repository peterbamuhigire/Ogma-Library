# Phase 13: Unified, trustworthy search

## 1. Header

| Field | Value |
|---|---|
| Wave | D, Search and AI |
| Size | 6–8 engineering days |
| Depends on | Phase 06 (text extraction and index state reliable), Phase 07 (search is a navigation destination and global shortcut) |
| Owner decisions | None (semantic availability depends on D-05, handled in Phase 14) |
| Primary defects | K40, K41, K14 (search result names) |
| Requirements | SEARCH-001 (W), SEARCH-002 (W), SEARCH-003 (W), SEARCH-004 (P), SEARCH-005 (P), SEARCH-006 (W), CLIENT-007 (P, host-index search) |

## 2. Why this phase exists

The audit ran a query battery against the 17-file synthetic library in the real window
(K40/K41). Results after the panel had settled:

| Query | Expected (content of the corpus) | Measured |
|---|---|---|
| `Lantern` | The Lantern Keeper ×2 (original + duplicate) | 2 ✔ |
| `Namutebi` | Algorithms Explained (author) | 1 ✔ |
| `caravan` | Trade Routes of East Africa (body text) | 1 ✔ |
| `dhow Zanzibar` | Trade Routes of East Africa (both words in body) | **0** |
| `Chwezi dynasty` | A Short History of the Great Lakes Kingdoms (body) | **0** |
| `algoritms` (typo) | Algorithms Explained | **0** |
| `Wanjirũ` (Unicode author) | Hadithi za Jioni: Évening Tales | **0** |
| `author:Okello` | The Physics of Everyday Light | **0** |
| `amber library lantern` | The Lantern Keeper (body words) + scanned pamphlet (image only, OCR-dependent) | **0** |
| `books about light and colour` | The Physics of Everyday Light (conceptual) | **0** (semantic unavailable) |
| `zzqxnotaword` | none, with a helpful empty state | 0 ✔ (no guidance) |

A first run of the same battery, immediately after opening the panel, returned **0 for every
query including `Lantern`** (K41), which indicates a race. The status read "Semantic search active"
while no embedding provider existed; each result exposed `SearchResultItem { BookId = … }` as its
accessible name (K14).

**Root causes found in code:**

1. The search panel is bound to `ISemanticSearchService` only (`SearchViewModel.cs:17,35`). When
   the provider is unavailable it falls back to `ExactFallbackAsync` → `ICombinedSearchService`
   (`SemanticSearchService.cs:89-92, 229-240`). The richer `CatalogueSearchService`
   (structured `field:` parsing and fuzzy fallback, `CatalogueSearchService.cs:55-112`) is never used
   by the panel.
2. `FtsIndexService.BuildMatchQuery` turns every multi-token query into an **exact phrase**
   (`FtsIndexService.cs:204-215`: `"\"" + string.Join(' ', tokens) + "\""`), so non-adjacent words
   never match.
3. Search runs inside a fire-and-forget `Task.Run` whose non-cancellation exceptions are lost, and
   the result version check compares against a `Query` that may have changed
   (`SearchViewModel.cs:196-269`).
4. The mode label is computed before availability is known.

## 3. Objectives and exit criteria

1. One search pipeline for the panel: **query parser → structured field filters → metadata
   (exact + fuzzy) → FTS5 (all-terms AND with prefix on the last token; quoted phrases stay
   phrases) → semantic (when available) → reciprocal rank fusion** (the existing
   `HybridRankingService`), deduplicated per book with per-hit locations.
2. **Query language:** bare words (all terms, any order), `"quoted phrase"`, `author:`, `title:`,
   `isbn:`, `year:`, `shelf:`, `tag:`, and a minus sign for exclusion. Diacritic-insensitive and
   case-insensitive for metadata and text (`Wanjirũ` ≡ `Wanjiru`). Typo tolerance (edit distance
   1–2 by term length) for metadata fields.
3. **Relevance oracle:** the battery above becomes an automated acceptance test over the synthetic
   corpus with these expected top-1 results: `dhow Zanzibar` → Trade Routes; `Chwezi dynasty` →
   Great Lakes Kingdoms; `algoritms` → Algorithms Explained; `Wanjirũ` and `Wanjiru` → Hadithi za
   Jioni; `author:Okello` → Physics of Everyday Light; `amber library lantern` → The Lantern Keeper
   in the top 2; `zzqxnotaword` → empty state with suggestions. Conceptual queries are asserted only
   when semantic search is available (Phase 14).
4. **Honest status:** the mode chip reads "Keyword and full-text", "Smart (semantic) search", or
   "Semantic unavailable: showing keyword results (set up in Settings)" from the actual response;
   index coverage is shown ("15 of 17 books searchable; 2 need OCR").
5. **Race-free:** a monotonically versioned request pipeline with cancellation on the UI thread;
   exceptions surface as an error state with *Retry*. The first-query-after-open defect cannot
   recur (regression test that opens the panel and queries immediately).
6. Results show title, author, match location (metadata field or page N with a snippet
   highlighting the terms), and *Open at page*. Accessible name: "Title, by Author, matched on
   page N" (K14).
7. p95 latency ≤ 300 ms for keyword queries on the 2,000-book synthetic corpus locally (the 50k
   budget is verified in Phase 24).

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-rag-patterns\SKILL.md` (hybrid retrieval, fusion, evaluation oracle)
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-evaluation\SKILL.md` (golden set and release gates)
- `C:\wamp64\www\chwezi-dev-engine\skills\backend-databases\database-design-engineering\SKILL.md` (FTS5 and indexes)
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md` (UI-thread rules)
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\error-empty-and-system-messaging\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\accessibility-wcag-2-2-compliance\SKILL.md`

## 5. Scope

**In scope:** a new `IUnifiedSearchService` in Application with its Infrastructure implementation
composing the existing services; query parser; FTS match builder; Unicode normalisation; the
`SearchViewModel` request pipeline; the results UI; the oracle test; and index-coverage status.

**Out of scope:** embedding generation and provider setup (Phase 14); AI answers (Phase 16); host
index search for classroom clients (Phase 20 reuses the same contract); and the catalogue quick filter
(Phase 10).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 13.1 | Commit the oracle first: an xUnit test that builds the synthetic corpus DB (reuse `New-SyntheticCorpus.py` content as C# fixture generation, or check in a small generated fixture set) and asserts the section 3 expectations. It must fail today. | `tests/OgmaLibrary.Tests/Search/SearchRelevanceOracleTests.cs` | Red on HEAD with the measured failures. |
| 13.2 | Query parser: tokens, quoted phrases, fields, exclusions; NFKD normalisation with diacritic stripping for matching; keep the original for display. | `src/OgmaLibrary.Application/Search/` (new `SearchQueryParser`) | Unit table of 30 parse cases. |
| 13.3 | FTS match builder: AND of terms with prefix on the last term, phrases preserved, exclusions as `NOT`; verify `unicode61 remove_diacritics` behaviour for precomposed characters and switch to `remove_diacritics 2` via migration if needed (rebuild the index through the staged generation already built). | `Infrastructure/Search/FtsIndexService.cs:204-215`, a migration if the tokenizer changes | `dhow Zanzibar`, `Chwezi dynasty` pass the oracle. |
| 13.4 | Metadata search: diacritic-folded comparisons and fuzzy matching on normalised title and author. | `Infrastructure/Search/MetadataSearchService.cs`, `CatalogueSearchService.cs:98-112` | `algoritms`, `Wanjiru`, `Wanjirũ` pass. |
| 13.5 | `IUnifiedSearchService`: structured filters → metadata + FTS (+ semantic when available) → `HybridRankingService` RRF → book-level results with hit locations; returns an explicit availability model. | New contract in Application; implementation in Infrastructure; register in the composition root | Unit: fusion ordering; availability flags correct with the provider absent. |
| 13.6 | `SearchViewModel` pipeline: replace fire-and-forget `Task.Run` with a versioned request loop on the dispatcher (debounce 150 ms), cancellation, and an error state; mode label from the response. | `ViewModels/Search/SearchViewModel.cs:160-269` | Regression test: query immediately after open returns results. |
| 13.7 | Results UI: snippet with highlighted terms, page label, *Open at page*, keyboard navigation, accessible names; empty state with suggestions (check spelling, remove filters, index coverage). | `Views/Search/SearchPanelView.axaml` | Headless UI names; screenshots. |
| 13.8 | Index coverage indicator from the index manager (`15 of 17 searchable`), linking to books that need OCR (Phase 17) or failed extraction. | Search panel header | Matches DB counts. |
| 13.9 | Performance benchmark (tagged `Performance`, excluded from the fast suite) on the 2,000-book corpus. | `tests/OgmaLibrary.Tests/Search/` | p95 ≤ 300 ms locally, recorded. |
| 13.10 | Global search shortcut (Ctrl+K, and Cmd+K on macOS) from anywhere, coordinated with the Phase 07 navigation. | Shell | Real-window journey J-SRCH-1. |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Multi-word misses (K40) | Multi-token → exact phrase | AND-of-terms builder | Natural queries work | Oracle pass: 3/10 → 10/10 (conceptual only with semantic) | Oracle test | Broader recall adds noise | RRF ranking; phrases still available via quotes |
| Field, typo, Unicode misses | Panel bypasses `CatalogueSearchService` | Unified pipeline plus parser | Parity with backend capability | As above | Oracle | Parser edge cases | 30-case parse table |
| Flaky empty results (K41) | Fire-and-forget plus version race | Dispatcher request loop | Deterministic results | First-query failures: observed → 0 in 50 repetitions | Regression test | Debounce feel | Tune the delay |
| Misleading mode label | Label before availability | Response-driven label | Users trust the status | Wrong labels: 1 → 0 | Screenshot | None | Revert |
| Record-dump names (K14) | No container names | Named result rows | Screen-reader usable | Bad names → 0 | UIA dump | None | Revert |

## 8. Test plan

- **Unit:** parser; normaliser; FTS builder; fusion; availability model.
- **Integration:** the relevance oracle over a real SQLite FTS5 index (fast enough for the default suite: 17 books).
- **Headless UI:** debounce and versioning; error state; empty state; accessible names; keyboard navigation of results.
- **Real-window:** J-SRCH-1 global shortcut → query → open at page → back to results; J-SRCH-2 the full battery typed through the UI (Phase 01 harness); J-SRCH-3 provider absent shows the honest label.
- **Negative:** an FTS syntax-breaking input (`"`, `*`, `NEAR(`, `:`) never throws; very long queries are truncated with a notice; the index rebuilding shows a partial-coverage notice.
- **Performance:** 2,000-book benchmark (tagged).

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Search&Category!=Performance"
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SearchRelevanceOracleTests"
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Search -Sizes 1280x800
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Conceptual (semantic) queries | Phase 14 (D-05) | Asserted only when a provider is available |
| 50k-book latency on reference hardware | Phase 24 | 2k locally |
| Human-labelled relevance set beyond the synthetic oracle | Phase 16/28 | The synthetic oracle is necessary, not sufficient |
| Image-only text (`amber library lantern` in the scanned pamphlet) | Phase 17 OCR | Counted only after OCR |

## 11. Risks and mitigations

- **Tokenizer change requires an index rebuild.** Mitigation: use the staged side-by-side generation and promotion built in Aug-39 Phase 23; show progress.
- **Fuzzy matching over-matches short terms.** Mitigation: edit distance by term length (0 for ≤ 3 characters, 1 for 4–7, 2 for ≥ 8).
- **Breaking search contract v1** frozen in Aug-39 Phase 26. Mitigation: a new versioned contract; keep v1 until the callers migrate; record in an ADR.

## 12. Execution prompt

```text
You are implementing Phase 13 (Unified, trustworthy search) of the Sept-23 Kaizen plan in
C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md and AGENT_BRIEF.md
3. docs/plans/sept-23-kaizen/phases/phase-13-unified-search.md (the query battery table is the acceptance oracle)
4. docs/plans/sept-23-kaizen/03-defect-register.md rows K40, K41, K14
Load the skills in section 4. Begin with 13.1: commit the failing oracle test and show it fails on
the current code. Then 13.2–13.4 (parser, FTS builder, metadata folding), 13.5 (unified service
+ ADR for the versioned contract), 13.6–13.8 (view model and UI), 13.9–13.10. Run the section 9
commands after each slice. Capture the real-window battery results and screenshots under
docs/implementation/execution/evidence/sept-23-kaizen/phase-13/. Do not weaken the oracle to make
it pass. Record results and NOT ASSESSED items in docs/implementation/execution/phase-sept23-13-completion.md.
```
