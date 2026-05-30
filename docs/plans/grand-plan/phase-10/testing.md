# Phase 10 — Test Plan

All nine test layers for Search & Indexing.

---

## 1. Test layers in scope

| Layer | In scope | Notes |
| --- | --- | --- |
| 1 — Domain unit | Yes — relevance score formula; chunker logic; `ExtractionQuality` enum transitions | Pure logic |
| 2 — Infrastructure | Yes — repositories; FTS5 trigger correctness; extraction pipeline against in-memory SQLite | Requires SQLite FTS5 support |
| 3 — PDF fixture | Yes — extraction golden-corpus; `ExtractionQuality` per fixture | Oracle: known text content |
| 4 — Search unit | Yes — `MetadataSearchService`; `FtsIndexService`; phrase search; diacritics | Core of this phase |
| 5 — AI unit | No | N/A |
| 6 — UI / component | Yes — search bar debounce; result list navigation; Index Manager dashboard | Avalonia headless |
| 7 — 3D | No | N/A |
| 8 — Performance | Yes — metadata P95 ≤ 150 ms; FTS5 P95 ≤ 500 ms warm; UI stall ≤ 100 ms | BenchmarkDotNet |
| 9 — Packaging | No | N/A |
| Manual | Yes — keyboard search flow; screen-reader result announcement | Documented below |

---

## 2. Golden-corpus extraction fixtures

| Fixture | Expected `ExtractionQuality` | Text oracle |
| --- | --- | --- |
| `simple-text` | `Full` | Known phrase "introductory chapter" found in `SearchChunks` |
| `two-column` | `Full` or `Partial` | Left-column text precedes right-column text in chunks |
| `non-english` | `Full` | French accented words extracted without corruption (é, à, ô) |
| `embedded-toc` | `Full` + TOC entries in `SearchChunks` with `Source = "toc"` | TOC item "Chapter 3" present in results |
| `scanned-image-only` | `Scanned` | `ExtractedPage.ExtractionQuality = Scanned`; `Text` is empty or null |
| `bad-metadata` | `Full` (text extractable even with bad metadata) | Text extracted; `IndexStatus = Indexed` |
| `very-large-1000pp` | `Full` (at least first 100 pages) | `SearchChunks.Count ≥ 100`; no OOM |

---

## 3. Unit tests

### 3.1 Relevance score

| Test | Oracle |
| --- | --- |
| `Score_ExactTitleMatch_Scores100` | Score = 100 |
| `Score_TitlePrefixMatch_Scores80` | Score = 80 |
| `Score_AuthorOnlyMatch_Scores60` | Score = 60 |
| `Score_MultipleMatchFields_Additive` | Score = 160 if both title prefix and author match |
| `Score_DescriptionOnlyMatch_Scores20` | Score = 20 |

### 3.2 Chunker

| Test | Oracle |
| --- | --- |
| `Chunker_512Token_NoOverlap_LastChunkTruncated` | Last chunk ≤ 512 tokens |
| `Chunker_64TokenOverlap_SecondChunkStartsAt448` | Token position 448 in text appears in chunk 1 and chunk 2 |
| `Chunker_EmptyText_ProducesZeroChunks` | `chunks.Count = 0` |
| `Chunker_ShortText_ProducesSingleChunk` | Text < 512 tokens → 1 chunk |

### 3.3 FTS5 trigger correctness

| Test | Oracle |
| --- | --- |
| `FtsTrigger_Insert_SearchChunk_AddedToFts5` | After `INSERT INTO SearchChunks`, `SELECT * FROM SearchFts5 WHERE chunk_text MATCH 'keyword'` returns one row |
| `FtsTrigger_Delete_SearchChunk_RemovedFromFts5` | After `DELETE FROM SearchChunks`, FTS5 returns zero rows |
| `FtsTrigger_Update_SearchChunk_ReflectedInFts5` | After UPDATE, FTS5 returns new text |

---

## 4. Integration tests

| Test | ID | Oracle |
| --- | --- | --- |
| `MetadataSearch_ExactTitle_ReturnsBook` | FR-SEARCH-001 | Query = exact title → book appears at position 0 |
| `MetadataSearch_PartialAuthor_ReturnsBook` | FR-SEARCH-001 | Partial author name → book in top 3 results |
| `MetadataSearch_EmptyQuery_ReturnsEmpty` | FR-SEARCH-001 | Zero results |
| `FtsSearch_PhraseInSimpleText_Finds` | FR-SEARCH-002 | Phrase known to be in `simple-text` fixture found |
| `FtsSearch_DiacriticInNonEnglish_Finds` | FR-SEARCH-002 | "éducation" found despite diacritic normalization |
| `FtsSearch_NoteText_Source_note_Finds` | FR-SEARCH-002 | Annotation note text indexed; found via FTS5 with `source = note` |
| `FtsSearch_TagText_Source_tag_Finds` | FR-SEARCH-002 | Tag "philosophy" indexed; found in FTS5 |
| `IndexManager_ShowsCorrectCounts` | FR-SEARCH-006 | After 50-book extraction: `IndexedCount = 50`, `FailedExtractionCount = 0` |
| `ExtractionPipeline_Resume_NoDuplicates` | NFR-OGMA-009 | Kill at book 5 of 10; restart; `SearchChunks.Count` = clean full run |
| `IndexRebuild_CompletesWithoutCorruption` | G7 | `SearchChunks.Count` same before/after rebuild; `integrity_check` passes |

---

## 5. Performance benchmarks

All benchmarks use the synthetic 2,000-book perf corpus (seeded by deterministic
random; titles/authors/text derived from a fixed seed). Benchmarks run on both
Windows (x64) and macOS (arm64) CI runners.

| Benchmark | Gate | Method |
| --- | --- | --- |
| `PerfBenchmark_MetadataSearch_P95` | ≤ 150 ms P95 (NFR-OGMA-003) | 50 random queries against 2,000-book corpus; BenchmarkDotNet percentile |
| `PerfBenchmark_FtsSearch_P95_Warm` | ≤ 500 ms P95 (NFR-OGMA-004) | 50 phrase queries; FTS5 warm (index pre-built) |
| `PerfBenchmark_UIStall_SearchDebounce` | No UI stall > 100 ms (NFR-PROD-005) | Trigger 20 rapid keystrokes; measure dispatcher queue depth |
| `PerfBenchmark_ExtractionThroughput` | Trend data (no hard gate yet) | Extract 100 books; measure wall time; stored as trend |

---

## 6. Reliability tests (G7 gate)

| Test | Steps | Oracle |
| --- | --- | --- |
| `IndexRebuild_CompletesWithoutDuplicatesOrCorruption` (G7) | (1) Full extraction on 100-book corpus; snapshot `SearchChunks.Count = N`. (2) Trigger rebuild. (3) Wait for completion. (4) Assert `SearchChunks.Count = N`. (5) Call `IntegrityCheck()`. | Count matches; `integrity_check` returns no errors |
| `IndexRebuild_InterruptedMidRebuild_Recovers` | (1) Start rebuild. (2) Kill DI scope at midpoint (after delete, before full re-extraction). (3) Restart. (4) Allow pipeline to complete. (5) Assert final state consistent. | `SearchChunks.Count = N`; `integrity_check` passes |

---

## 7. UI / accessibility tests

| Test | Tooling | Oracle |
| --- | --- | --- |
| `SearchBar_CtrlK_Opens` | Avalonia headless | `Ctrl+K` focuses search bar |
| `SearchBar_Debounce_DoesNotFireBelow150ms` | Avalonia headless | Rapid keystrokes do not fire search; fires 150 ms after last key |
| `ResultList_ArrowKeys_Navigate` | Avalonia headless | Arrow keys move selection; Enter opens book |
| `ResultList_Escape_ClearsAndCloses` | Avalonia headless | Escape clears query and dismisses results |
| `IndexManager_RebuildButton_ShowsProgress` | Avalonia headless | After click, progress bar visible; cancel button appears |
| Screen-reader pass (manual) | VoiceOver / Narrator | "3 results found" announced after query; "Index rebuild complete" announced |

---

## 8. Architecture tests

| Test | Oracle |
| --- | --- |
| `Architecture_Search_DoesNotDependOnReader` | No type in `OgmaLibrary.Application.Search` references `OgmaLibrary.Reader.*` |
| `Architecture_Search_DoesNotDependOnAI` | No type references `OgmaLibrary.AI.*` |
| `Architecture_Search_AccessesCatalogueOnlyViaContracts` | No `DbContext` in `OgmaLibrary.Application.Search`; only `ISearchChunkRepository`, `IExtractedTextStore` |

---

## 9. Manual test checklist

- [ ] Type "rousseau" in search bar on French locale; confirm results appear
      within 150 ms; result list shows author/title match.
- [ ] Open Index Manager; trigger rebuild; observe progress bar advancing;
      click Cancel; confirm index left in consistent state (not empty).
- [ ] On `non-english` fixture: search for "éducation" with diacritics;
      confirm results found.
- [ ] On `scanned-image-only` fixture: confirm Index Manager shows in
      "Pending OCR" count; FTS5 search returns no result for this book.
- [ ] Under Narrator (Windows): focus search bar; type query; confirm
      result count announced.
- [ ] Change UI language to French; confirm all Index Manager labels and
      search tooltips are in French.
