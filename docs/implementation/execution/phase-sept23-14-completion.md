# Phase 14 (Sept-23 Kaizen): Local semantic capability and embeddings: completion record

| Field | Value |
|---|---|
| Status | **IMPLEMENTED** 2026-11-20, not COMPLETE (see section 7) |
| Branch | `kaizen/phase-14` |
| Rollback point for the phase | `f29ece61` (head before slice 1) |
| Code head assessed | `f5fd5abc` (slice 7 changes documents and evidence only) |
| Phase document | [phase-14-local-semantic-capability.md](../../plans/sept-23-kaizen/phases/phase-14-local-semantic-capability.md) |
| Evidence | [`evidence/sept-23-kaizen/phase-14/`](evidence/sept-23-kaizen/phase-14/) |
| Defects | K25 (embedding part) resolved; K42 mostly resolved; K141 and K143 fixed; open K140, K142, K144 to K154 |

## 1. What changed

- **Provider abstraction (14.2).** `IEmbeddingProvider` (`ProviderKey`, `ModelId`, `ModelVersion`, `Dimensions`, `IsAvailableAsync`). The `EmbeddingProviderRegistry` is the single binding and chooses Built-in, Ollama or Off from Settings. Provenance is stamped by the provider, which also fixed Ollama vectors never being counted or searched.
- **Built-in ONNX provider (14.3).** `OnnxEmbeddingProvider` in `Infrastructure/AI/Onnx/` runs `intfloat/multilingual-e5-small` (MIT, 384 dimensions, qint8) with a SentencePiece tokenizer, the `query: ` and `passage: ` prefixes, mean pooling and L2 normalisation. A verified store checks a SHA-256 manifest. Inference makes no network call.
- **Model acquisition (14.4).** The model is downloaded on first use, and only after consent in Settings. The download is pinned by revision and SHA-256 and is resumable (`*.partial`). One Infrastructure adapter makes the request, with a host allowlist and its own redirect checks. Outcomes are `Installed`, `Offline`, `ChecksumMismatch`, `InsufficientSpace`, `HostNotAllowed` and `ServerError`. The model is never bundled in git or the installer.
- **Honest lifecycle (14.5, 14.6).** Embedding jobs park as `WaitingForCapability`, consume no retries, and resume when the capability changes. Availability is cached and event-driven, and the query path never probes.
- **Regeneration on model change (14.8).** `SemanticGenerationCoordinator` stages a new generation when the model changes and swaps atomically. Searches use the old generation until the swap, and no batch writes vectors from two models.
- **Settings > Search and AI (14.7).** Provider choice and model status: not installed, downloading %, verified, damaged, ready. Also disk use, *Build smart index now*, progress with an ETA, pause and *Erase smart index*. English and French strings throughout. The Activity Centre shows "Smart search is off: n books waiting" instead of failures.
- **Oracle and benchmark (14.9, 14.10).** The conceptual oracle has eight cases (en and fr), gated on the model. A throughput, memory and latency benchmark is tagged `Performance` and `Benchmark`. Journeys J-SEM-1, J-SEM-2, J-SEM-N1 and J-SEM-N2 were added.

## 2. Exit criteria

| # | Criterion | Result | Class |
|---|---|---|---|
| 1 | D-05 implemented: built-in ONNX provider, Ollama kept as advanced, zero egress at inference | Done (D-103 to D-105). The ONNX inference path has no HTTP (architecture tests, 54 of 54). Download only after a click. | CODE + MEASURED |
| 2 | Provider abstraction, registry picks from Settings | Done; `EmbeddingProviderRegistry` tests 12 of 12 (slice 3/4) | CODE |
| 3 | `WaitingForCapability`, no retries, auto-resume; Activity Centre wording | No model, 10 min real idle: 14 jobs parked, **0 failed, 0 attempts** (J-SEM-N1). Damaged model: 0 failed at both sizes (J-SEM-N2). Before: 16 of 16 failed. | MEASURED |
| 4 | Setup UI in Settings | Section present; headless tests pass. Real window: J-SEM-1 PASS at 1280x800 and FAIL at 1920x1080 (K152). J-SEM-2 PASS at both sizes. The first-run error toast is K147. | MEASURED |
| 5 | Staged regeneration on model change, atomic swap | Phase25 lifecycle tests 8 of 8: model switch, mid-batch switch, atomic swap (slice 4) | CODE (headless) |
| 6 | Semantic results in RRF fusion; cached, event-driven probe | Keyword latency, provider absent against semantic off: 6,480.2 against 6,484.6 ms (ratio 0.999, target ±10 %), 0 Ollama calls | MEASURED |
| 7 | Conceptual oracle top 3 | On the final head, all 8 conceptual cases (en and fr) are in the top 3 with the built-in model, and the oracle passes 38 of 38 in 6.0 min (`oracle-output.txt`). Ranks measured in slice 6: seven at 1, "royaumes africains précoloniaux" at 2. Real window: the expected book ranked 1 (J-SEM-1, 1280x800). No relevance floor (K146). | MEASURED (synthetic corpus) |
| 8 | Budgets: throughput and memory, UI responsive | Corpus 1,275 chunks at 3.29 chunks/s, peak 470 MB. 2,000 one-page books at 12.83 chunks/s, peak 493 MB. Budget not met for large libraries (K145). Below-normal priority applies only partly (K142). UI responsiveness not profiled. | MEASURED (throughput); HEURISTIC (responsiveness) |

## 3. Gates on the final head (MEASURED, this machine, 2026-11-20)

| Command | Result |
|---|---|
| `./scripts/Test-RequirementAccountability.ps1` | Pass: 101 FRs, 29 NFRs, 32 controls; 162 IDs assigned |
| `dotnet restore OgmaLibrary.sln --locked-mode` | Pass |
| `dotnet format --verify-no-changes --no-restore` | Pass |
| `dotnet build --configuration Release --no-restore` | Pass, 0 warnings, 0 errors |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages (11 projects) |
| `dotnet format analyzers --severity warn` | Pass |
| `dotnet test … --filter "Category!=Performance&Category!=Benchmark&Category!=E2E" -m:1`, with `OGMA_TEST_EMBEDDING_MODEL_DIR` set | Architecture 54 of 54; Tests.Ui 533 of 533; E2E harness 5 of 5. Unit tests **1,588 of 1,589, 0 skipped**, in 1 h 16 min (K154). The one failure is `PdfWorkerSessionStabilityTests…WorkerKilledMidRead` (2,160 against 2,000 ms); its class passed 12 of 12 when run alone (K153, a load-sensitive flake, not Phase 14 code). |
| `SearchRelevanceOracleTests` with the model | 38 of 38, 0 skipped (`oracle-output.txt`) |

The `src/shelf3d` npm gates were not run: no change in `src/shelf3d`.

## 4. Real-window journeys (desktop lock, fresh data folder, synthetic corpus, Light)

Full detail: [`real-window-journeys-final.json`](evidence/sept-23-kaizen/phase-14/real-window-journeys-final.json). Screenshots: [`slice7-screenshots/`](evidence/sept-23-kaizen/phase-14/slice7-screenshots/), with before and after at both sizes.

| Journey | 1280x800 | 1920x1080 |
|---|---|---|
| J-SEM-1 enable → index → conceptual query | PASS, contaminated (index 649.5 s; expected book 1st; foreign keystrokes in the screenshot) | **FAIL** (K152: stalled at "7 of 12", then a blank window) |
| J-SEM-2 off → keyword-only, no failures | PASS | PASS (rerun; the first run's hit test was covered) |
| J-SEM-N1 no model, 10 min idle | PASS (0 failed, 0 attempts) | not run, by design (runs at the first size) |
| J-SEM-N2 damaged model | PASS | PASS |
| G4 search | PASS | PASS |
| Settings (4 journeys) | PASS (1 on the rerun) | 3 PASS; `EverySection` NOT_ASSESSED (foreign windows in both runs) |
| G8 corrupt settings (Settings tag) | PASS | NOT_ASSESSED (foreign windows in both runs) |

During the Settings runs and the J-SEM-2 rerun, another party was using the desktop. Hit tests landed on a "Windows PowerShell" window, a messaging notification and a "Conversation" group, and one run found the window minimised. These failures are recorded as NOT_ASSESSED, not as passes or app failures.

## 5. Rollback SHAs per slice

| Slice | Content | Rollback point (head before) | Commits |
|---|---|---|---|
| 1 | Currentness review, D-103 to D-105, `IEmbeddingProvider` | `f29ece61` | `06d47582`, `1c0d8fbd` |
| 2 | ONNX provider and verified store | `1c0d8fbd` | `808016fb`, `36c258dd` |
| 3 | Verified download and provider registry | `36c258dd` | `42097c46`, `cd762712`, `1a5c9828` |
| 4 | Event-driven availability, resume, staged regeneration | `1a5c9828` | `d6a07379`, `7d515966`, `851e3c0b` |
| 5 | Settings > Search and AI | `851e3c0b` | `02a22bc8`, `465c2a4c` |
| 6 | Oracle, journeys, benchmark | `465c2a4c` | `c2b7ab4e`, `f5fd5abc` |
| 7 | Record and evidence (this record) | `f5fd5abc` | `docs(kaizen): record phase 14 completion evidence and status` |

## 6. Decisions

- **D-103** D-05 re-evaluated; built-in ONNX provider, consented first-use download, pinned revision and SHA-256, never bundled; Ollama advanced; Off.
- **D-104** No model installed → Built-in "not installed", jobs parked, no network without a click.
- **D-105** `multilingual-e5-small` qint8 with `query: `/`passage: ` prefixes; fallback `paraphrase-multilingual-MiniLM-L12-v2`.
- **D-106 to D-127**: see the execution log (slices 2 to 6).
- **D-128** (slice 7) Phase 14 is IMPLEMENTED, not COMPLETE: J-SEM-1 fails at 1920x1080 (K152), and K147 and K148 (P2, phase 14) are open. The work order said both should be fixed before close; slice 7's scope is the record, so they go to a Phase 14 follow-up slice.
- **D-129** (slice 7) Real-window failures whose hit tests land on foreign windows are NOT_ASSESSED, and each test keeps its pass from either run. No journey was retried a third time while the desktop was in use.
- **D-130** (slice 7) The benchmark JSON from slice 6 stands as the measurement for this head. The code has not changed since `c2b7ab4e`, and the benchmark is excluded from the gate filter.

## 7. Definition of Done assessment (08-master-plan section 5)

| Item | Status |
|---|---|
| 1 Every task done or moved | Tasks 14.1 to 14.10 done in code. Open P2 defects in this phase's scope: K147, K148, K152. |
| 2 Full gate set | Pass except one load-sensitive unit failure (K153). Journey suite: J-SEM-1 FAIL at 1920x1080. |
| 3 Tests at the right layers | Yes: unit, contract, headless UI, real window |
| 4 Before and after screenshots at both sizes | Yes, for J-SEM-1, J-SEM-2, J-SEM-N2, G4 and Settings. J-SEM-1 at 1920x1080 has no valid "after" (blank). |
| 5 Localised, tokens, accessible names | en and fr, tokens only. `Search.Mode` is missing from the UIA tree (K149), and the badge label does not match its accessible name (K150). |
| 6 Completion record | This file |
| 7 Status register and execution status | Updated |

**Result: IMPLEMENTED.**

## 8. Typeface statement

No typeface change in Phase 14. The Search and AI section uses Spectral for the section title, Public Sans for labels and controls, and JetBrains Mono for the environment-override line, all from the existing design tokens (`src/OgmaLibrary.App/Themes/`). No banned font was introduced.

## 9. NOT_ASSESSED, with owners

| Item | Owner |
|---|---|
| macOS arm64 ONNX Runtime behaviour | Phase 27 |
| 50k-book embedding time on reference hardware | Phase 24 |
| Human-judged semantic relevance (synthetic oracle only) | Phases 16 and 28 |
| The real first-use download click in the window (journeys copy the verified model from `OGMA_TEST_EMBEDDING_MODEL_DIR`; the download protocol is covered by stub-handler tests only) | Phase 14 follow-up |
| Ollama live path (no Ollama server on this machine) | Phase 15 |
| Screen-reader walkthrough of Settings > Search and AI | Phase 21 |
| Installer payload: ONNX Runtime natives trimmed to the target runtime | Phase 26 |
| Skills `ai-rag-patterns`, `ai-evaluation`, `ai-model-gateway` (absent from the consolidated development engine) | Engine maintainer |
| Settings `EverySection` and G8 at 1920x1080 (foreign windows) | Phase 14 follow-up (rerun on an idle desktop) |
| Dark theme, 200 % text, French, RTL runs of the J-SEM journeys | Phases 21 and 22 |
| CDN redirect hosts outside this region (K140) | Phase 26 |
| UI responsiveness during indexing (K142) | Phase 24 |

## 10. Evidence files

- `real-window-journeys.json` (slice 6) and `real-window-journeys-final.json` (slice 7)
- `slice7-screenshots/` (29 images; synthetic corpus only)
- `embedding-throughput.json`, `keyword-latency-provider-absent.json` (slice 6 benchmark, same code)
- `embedding-jobs-query.sql` (the failed-embedding-jobs query and its measured results)
- `oracle-output.txt` (conceptual and keyword oracle on the final head)

No model files and no book content are stored. The model sits outside git in a temporary folder pointed to by `OGMA_TEST_EMBEDDING_MODEL_DIR`.
