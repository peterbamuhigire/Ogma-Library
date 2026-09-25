# Phase 14: Local semantic capability and embeddings

## 1. Header

| Field | Value |
|---|---|
| Wave | D, Search and AI |
| Size | 5–8 engineering days (depends on D-05) |
| Depends on | Phase 13 (unified search consumes semantic results through fusion), **D-05** |
| Owner decisions | **D-05** local semantic engine (recommended: bundle a small local ONNX embedding model; alternatives: guided Ollama setup, or semantic off by default) |
| Primary defects | K42, K25 (embedding retry storm) |
| Requirements | AI-006 (P), SEARCH-004 (P), SEARCH-005 (P), NFR local-first/offline, CTRL zero-egress for embeddings |

## 2. Why this phase exists

Semantic ("smart") search is a headline feature, but on a normal machine it cannot work:

- The only provider is Ollama at `localhost:11434` with the model `nomic-embed-text`
  (`EmbeddingGenerationService.cs:18-20`, `OllamaEmbeddingAdapter`). Ollama is not shipped, not
  detected in the UI and not explained (K42). On the test machine the port was closed.
- The embedding worker still queued and **failed 16 of 16 jobs** with
  `embedding_provider_unavailable` (retryable), contributing to the Activity Centre's
  "26 failed · 300 attempts" for 17 books (K25). `EmbeddingGenerationWorker` polls every 10 s idle
  and 15 s after errors (`EmbeddingGenerationWorker.cs:14-15,60-69,87-99`), so an absent provider
  becomes a permanent failure stream.
- Each search first probes the provider (`SemanticSearchService.cs:89`), adding latency when it is
  absent.
- Conceptual queries such as "books about light and colour" return nothing (Phase 13 battery).

The backend foundations are strong: versioned embeddings with provenance, dimension consistency,
stale/tombstone lifecycle, a bounded-memory 50k vector scan, staged side-by-side generation and
swap (Aug-39 phases 25–26). What is missing is a provider that exists on the user's machine and
an honest lifecycle when it does not.

## 3. Objectives and exit criteria

1. **D-05 implemented.** Recommended path: an `OnnxEmbeddingProvider` running a small, permissively
   licensed sentence-embedding model (selected in 14.1 by the currentness and licence review) with
   the ONNX Runtime CPU package, shipped in the installer or downloaded on first use with checksum
   verification. Ollama stays as an optional *advanced* provider. Zero network egress in both cases.
2. **Provider abstraction:** rename or generalise `IOllamaEmbeddingProvider` into
   `IEmbeddingProvider` with `ProviderKey`, `ModelId`, `ModelVersion`, `Dimensions` and
   `IsAvailableAsync`. The registry picks the provider from Settings.
3. **Honest job lifecycle:** when no provider is available, embedding jobs enter a
   **`WaitingForCapability`** state (not Failed, no retry count consumed). They resume automatically
   when the provider becomes available. The Activity Centre shows "Smart search is off: 17 books
   waiting" instead of failures.
4. **Setup UI** in Settings → Search and AI: provider choice (Built-in / Ollama / Off), model
   status (installed, downloading %, verified), disk use, *Build smart index now*, progress with an
   ETA, pause, and *Erase embeddings*.
5. **Model or version change** triggers a staged regeneration into a new generation with atomic
   swap (existing staged generation service); searches keep using the old generation until the swap.
6. **Search integration:** semantic results flow into the Phase 13 RRF fusion. The provider probe is
   cached (availability is event-driven, not probed on every query).
7. Conceptual oracle queries pass when the provider is on: "books about light and colour" → Physics
   of Everyday Light in the top 3; "rainforest animals" → Tropical Ecology in the top 3;
   "precolonial kingdoms" → Great Lakes Kingdoms in the top 3.
8. **Budgets (local, this machine):** embedding throughput and memory recorded for the 17-book
   corpus and the 2,000-book synthetic corpus; the UI stays responsive (worker at below-normal priority).

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-rag-patterns\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-evaluation\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-model-gateway\SKILL.md` (provider abstraction and routing)
- (`chwezi-dev-engine/skills/backend-databases/vector-databases` is an inactive alias of `ai-rag-patterns`, already listed above; read its `references/` only for embedding-cost and freshness notes.)
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`
- `C:\wamp64\www\digital-research-engine\skills\source-evaluation\SKILL.md` and `...\source-verification\SKILL.md` (model and package currentness)
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\form-ux-design\SKILL.md`

## 5. Scope

**In scope:** the provider abstraction; the ONNX provider (or the D-05 alternative); job state
`WaitingForCapability`; setup UI; model download and verification; regeneration on model change;
the search integration flag; licence register entries for model weights.

**Out of scope:** chat and generation providers (Phase 15); Advisor ranking (Phase 16); and ANN
indexing beyond the existing bounded scan (Phase 24 decides whether ANN is needed at 50k).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 14.1 | Currentness and licence review: shortlist 2–3 small embedding models (≤ 150 MB, permissive licence, multilingual including French), and the current ONNX Runtime package. Record them in `00-currentness-register.md` with sources, dates and freshness class; present the choice to the owner (D-05). | `docs/plans/sept-23-kaizen/00-currentness-register.md` | Register entries VERIFIED; owner decision recorded. |
| 14.2 | Introduce `IEmbeddingProvider` and adapt the Ollama adapter; keep `IOllamaEmbeddingProvider` as an adapter alias until callers migrate. | `Application/Search/IOllamaEmbeddingProvider.cs`, `Infrastructure/AI/Ollama/OllamaEmbeddingAdapter.cs`, `Infrastructure/Search/EmbeddingGenerationService.cs:18-20` | Architecture tests pass (no HTTP in Application). |
| 14.3 | `OnnxEmbeddingProvider` in Infrastructure: tokenizer, mean pooling, normalisation, batch size bound, cancellation; the model stored under the app-data folder with a SHA-256 manifest. | New `Infrastructure/AI/Onnx/` | Unit: deterministic vectors for fixed inputs; dimension matches the manifest. |
| 14.4 | Model acquisition: bundled or first-use download from a pinned URL with checksum; resumable; clear failure. | Installer (Phase 26) or downloader service | Tampered file → rejected; offline → "download later". |
| 14.5 | `WaitingForCapability` job state: a migration adding the status; the worker parks jobs when the provider is unavailable and does not consume retries; resume on the capability-changed event. | `Workers/EmbeddingGenerationWorker.cs:60-120`, job runtime, migrations | With no provider: 0 failed embedding jobs, 0 retries after 10 minutes. |
| 14.6 | Cached availability with event-driven refresh; remove the per-query probe. | `Infrastructure/Search/SemanticSearchService.cs:89` | Query latency with the provider absent is equal to keyword latency (±10 %). |
| 14.7 | Settings → Search and AI section: provider selector, model status, build or pause index, erase embeddings, disk usage. | Phase 08 Settings view | Real-window journey J-SEM-1. |
| 14.8 | Regeneration on model change using the staged generation and swap; old generation serves until the swap. | `IStagedEmbeddingGenerationService` | Switch model mid-way → searches keep working; the swap is atomic. |
| 14.9 | Extend the Phase 13 oracle with conceptual queries, gated on provider availability; add a small multilingual (en/fr) set. | `SearchRelevanceOracleTests` | Conceptual top-3 assertions pass with the built-in provider. |
| 14.10 | Throughput and memory benchmark (tagged `Performance`). | Tests | Numbers recorded in the evidence folder. |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Semantic search unusable (K42) | Only an external, uninstalled provider | Bundled local provider (D-05) | Smart search works offline out of the box | Machines with working semantic search after install: ~0 % → 100 % (Windows x64 and macOS arm64 evidence) | Harness plus oracle | Package size and CPU cost | Provider "Off" option; Ollama remains |
| Retry storm (K25) | Absent capability treated as a retryable failure | `WaitingForCapability` state | Clean Activity Centre | Failed embedding jobs with no provider: 16 → 0 | DB query | State migration errors | Migration rollback script |
| Per-query probe latency | Availability probed each search | Cached, event-driven availability | Faster keyword search | Added latency: unmeasured → ≈ 0 | Benchmark | Stale availability | Refresh on settings change and hourly |

## 8. Test plan

- **Unit:** provider contract tests run against a fake, ONNX and (optionally) Ollama; vector normalisation; the manifest checksum; job state transitions.
- **Integration:** embed the 17-book corpus with the built-in provider; semantic plus RRF results through the unified search.
- **Headless UI:** Settings section states (not installed, downloading, ready, error, off).
- **Real-window:** J-SEM-1 enable smart search → progress → conceptual query succeeds; J-SEM-2 disable → keyword-only label, no failures appear.
- **Negative:** a corrupt model file; a disk-full during download; a provider switched mid-generation; an app closed mid-batch (no retry consumed).

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test OgmaLibrary.sln --configuration Release --no-build --filter "Category!=Performance" -m:1
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~SearchRelevanceOracleTests"
dotnet list OgmaLibrary.sln package --vulnerable --include-transitive
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag SemanticSearch
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| D-05 decision and model licence | Owner | Blocks 14.3–14.4 |
| macOS arm64 ONNX Runtime behaviour | Phase 27 | Windows evidence only |
| 50k-book embedding time on reference hardware | Phase 24 | Local 2k only |
| Human-judged semantic relevance | Phase 16/28 | Synthetic oracle only |

## 11. Risks and mitigations

- **Installer size.** Mitigation: first-use download with checksum, opt-in, and a clear size shown before download.
- **CPU contention** with PDF processing. Mitigation: a shared resource group and below-normal priority; pause while the reader is active.
- **Model licence changes.** Mitigation: the currentness register entry with a review date; weights pinned by hash.

## 12. Execution prompt

```text
You are implementing Phase 14 (Local semantic capability and embeddings) of the Sept-23 Kaizen
plan in C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md and AGENT_BRIEF.md
3. docs/plans/sept-23-kaizen/phases/phase-14-local-semantic-capability.md
4. docs/plans/sept-23-kaizen/03-defect-register.md rows K25, K42 and 08-master-plan.md D-05
Confirm Phase 13 is COMPLETE and D-05 is decided; if D-05 is not decided, do only 14.1 and 14.5–14.6
(they are needed whatever the choice) and stop. Load the skills in section 4, and run the digital
research currentness gate for model and package choices (record claims in 00-currentness-register.md).
Keep Domain and Application free of HTTP and native runtime references (architecture tests are
release gates). After each slice run the section 9 commands; store the oracle output, benchmark
numbers and screenshots under docs/implementation/execution/evidence/sept-23-kaizen/phase-14/.
Record results and NOT ASSESSED items in docs/implementation/execution/phase-sept23-14-completion.md.
```
