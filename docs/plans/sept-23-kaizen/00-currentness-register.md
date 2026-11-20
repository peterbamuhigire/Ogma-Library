# Currentness register (Digital Research gate)

Cycle: Sept-23 Kaizen. Preflight date: **2026-09-25**. Gate followed:
`C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md`,
with the engine skills `source-evaluation` and `source-verification` read first.

## Preflight result

This plan contains time-sensitive claims, so `NO_TIME_SENSITIVE_CLAIMS` does not apply. Each claim
below has a disposition.

- **Local measurements** were made on the audit machine (Windows 11 Pro 10.0.26200, 2560×1440)
  from lock files, the registry, command output or the running app. They are recorded as
  `verified (local measurement)`, scoped to this machine and commit `0ad3c0c`.
- **External rules and standards** (signing, notarisation, WCAG text, provider model IDs) were
  **not** checked online in this cycle. They are `NOT_ASSESSED`, with the consuming phase as owner.
  Nobody may turn them into an acceptance criterion until that phase records a primary-source check.

Remembered defaults are not admitted as current facts.

## Source tiers used

| Tier | Meaning |
|---|---|
| T0 | Direct local measurement (command output, lock file, registry, running app) |
| T1 | Primary vendor or standards-body documentation (not consulted this cycle) |
| T2 | Reputable secondary source (not used) |

## Claim register

| claim_id | Claim | Source | Tier | Scope | Version / as-of | Access / verified | Freshness | Disposition | Owner |
|---|---|---|---|---|---|---|---|---|---|
| CUR-01 | The build uses .NET SDK **10.0.401** (MSBuild 18.9.11) with runtime **10.0.12**; SDK 10.0.101 is also installed, and there is no `global.json` pin | `dotnet --info`, `dotnet --list-sdks`, crash event "CoreCLR 10.0.1226.42308 / .NET 10.0.12" | T0 | Audit machine | 2026-09-25 (10.0.401 folder written 06:41–06:42 the same morning) | 2026-09-25 | time-sensitive | verified (local measurement) | Phase 00 (pin in `global.json`) |
| CUR-02 | All projects target `net10.0` | `src/*/*.csproj` | T0 | Repo @ `0ad3c0c` | — | 2026-09-25 | context-bound | verified (local measurement) | Phase 00 |
| CUR-03 | .NET 10 is an LTS release and supported through the planned beta | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phase 26 |
| CUR-04 | The UI framework is Avalonia **11.3.17**, with `Avalonia.Controls.WebView` **11.4.0** | `src/*/packages.lock.json` | T0 | Repo @ `0ad3c0c` | lock file | 2026-09-25 | context-bound | verified (local measurement) | Phases 07, 09, 18 |
| CUR-05 | Avalonia 11.3.x is the current supported line, and Avalonia 12 changes nothing this plan depends on | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phase 07 |
| CUR-06 | PDF stack: `PDFtoImage` **5.2.1** (PDFium natives in `runtimes/*/native`), `PdfPig` **0.1.9**, `PDFsharp` **6.1.1** | lock files; `bin/Release/net10.0/runtimes/win-x64/native/pdfium.dll` | T0 | Repo @ `0ad3c0c` | lock file | 2026-09-25 | context-bound | verified (local measurement) | Phases 04, 06 |
| CUR-07 | OCR: `Tesseract` **5.2.0** with `Tesseract.Data.English` **4.0.0**; `tessdata/eng.traineddata` ships; no other language data ships | lock files; build output | T0 | Windows build | lock file | 2026-09-25 | context-bound | verified (local measurement) | Phase 17 |
| CUR-08 | Tesseract native packaging works on macOS | Prior CI records say NOT ASSESSED | — | macOS | — | — | time-sensitive | **NOT_ASSESSED** | Phase 27 |
| CUR-09 | Persistence: EF Core SQLite **10.0.9**, `Microsoft.Data.Sqlite.Core` **10.0.9**, `SQLitePCLRaw.bundle_e_sqlite3` **3.0.3**; the catalogue has 68 tables and 41 migrations | lock files; audit DB query | T0 | Repo @ `0ad3c0c` | lock file | 2026-09-25 | context-bound | verified (local measurement) | Phases 05, 06 |
| CUR-10 | Edge WebView2 Runtime **153.0.4234.48** is installed on the audit machine, yet the app reported "3D view is not available on this device" | Registry `EdgeUpdate\Clients\{F3017226-…}` `pv`; screenshot `sheet1.png` | T0 | Audit machine | 2026-09-25 | 2026-09-25 | time-sensitive | verified (local measurement) | Phase 18 |
| CUR-11 | 3D bundle dependencies: `three` ^0.181.2, `esbuild` ^0.28.2, `typescript` ^5.9.3 | `src/shelf3d/package.json` | T0 | Repo | package.json | 2026-09-25 | context-bound | verified (local measurement) | Phase 18 |
| CUR-12 | WebGL2 is available in WebView2 and WKWebView on the reference machines | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phases 18, 27 |
| CUR-13 | Semantic search expects Ollama at `http://localhost:11434` with the model `nomic-embed-text`; port 11434 was **closed** on the audit machine | `OllamaEmbeddingAdapter.cs:15`, `AiServiceExtensions.cs:54`; TCP probe | T0 | Audit machine | 2026-09-25 | 2026-09-25 | time-sensitive | verified (local measurement) | Phase 14 |
| CUR-14 | A small local ONNX embedding model with acceptable licence, size and quality exists for bundling (decision D-05) | Superseded by CUR-23 to CUR-27 (Phase 14 slice 1, 2026-11-20) | T1 + T0 | Windows x64, this machine | see CUR-25 | 2026-11-20 | time-sensitive | **verified for licence, size, tokenizer parity and CPU execution**; retrieval quality NOT_ASSESSED until the Phase 14 conceptual oracle runs | Phase 14 |
| CUR-15 | The provider keys the code allows are `disabled`, `openai`, `deepseek`, `anthropic` and `ollama`, with fixed-host egress checks; the view models hard-code `"openai"` / `"gpt-test"` | `AiProviderProfileService.cs:169-211`, `RecommendationPanelViewModel.cs:376`, `ReadingPlanViewModel.cs:155` | T0 | Repo @ `0ad3c0c` | — | 2026-09-25 | context-bound | verified (local measurement) | Phase 15 |
| CUR-16 | The exact provider model IDs, their availability, pricing and data-use terms for the Advisor | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** (see model-currentness review) | Phase 15 |
| CUR-17 | Metadata provider (Google Books, Open Library) terms, quotas and attribution rules | Prior records cite official terms, but not re-verified this cycle | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phase 08 |
| CUR-18 | WCAG 2.2 Level AA is the accessibility target, and its success criteria apply to desktop UI through WCAG2ICT | Not checked this cycle | — | — | — | — | stable text, context-bound application | **NOT_ASSESSED** | Phase 21 |
| CUR-19 | Windows code signing requirements (certificate type, key storage, timestamping, SmartScreen reputation) and MSIX packaging rules | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phase 26 |
| CUR-20 | macOS Developer ID signing, hardened runtime and notarisation requirements (tooling and entitlements for WKWebView) | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phases 26, 27 |
| CUR-21 | Data-protection obligations for processing minors' data in schools (DPIA, controller approval) in the owner's jurisdictions | Prior records mark these open | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phase 23 |
| CUR-22 | Startup and runtime measurements: 3.7–4.1 s warm launch to window; 23.9 s under concurrent test load; working set 182–252 MB; scan of 17 files in about 15 s; close in 3.75 s | Audit runs (see [02-test-report.md](02-test-report.md)) | T0 | Audit machine, synthetic corpus | 2026-09-25 | 2026-09-25 | context-bound | verified (local measurement) | Phase 24 |

### Phase 14 slice 1 additions (2026-11-20)

Skills read: development engine `research-currentness` and `source-verification` (router route
`root`). The phase document's `ai-rag-patterns`, `ai-evaluation` and `ai-model-gateway` do not
exist in the consolidated engine: NOT_ASSESSED. Sources were read directly from the registries
on 2026-11-20 (access date = verification date). Review: re-check at the start of Phase 28, or
sooner on a security advisory, a new major package version or a change to the model repository.
"Latest" below means the newest non-preview version listed by the registry on the access date.

| claim_id | Claim | Source | Tier | Scope | Version / as-of | Access / verified | Freshness | Disposition | Owner |
|---|---|---|---|---|---|---|---|---|---|
| CUR-23 | `Microsoft.ML.OnnxRuntime` **1.32.0** is the latest stable version (previous 1.31.1), published 2026-11-13; licence MIT; the `.nupkg` is 184,391,411 B because it carries every platform's native runtime (packaging must trim to the target RID) | <https://api.nuget.org/v3-flatcontainer/microsoft.ml.onnxruntime/index.json>; registration leaf `published`; package `LICENSE` file | T1 | NuGet.org | 1.32.0, published 2026-11-13 | 2026-11-20 | time-sensitive | verified | Phase 14 (slice 3), Phase 26 (package size) |
| CUR-24 | `Microsoft.ML.Tokenizers` **3.0.0** is the latest stable version (earlier entries are 3.0.0 previews), published 2026-11-11; licence MIT (nuspec expression); its `SentencePieceTokenizer` loads the XLM-R `sentencepiece.bpe.model` (a unigram model) | <https://api.nuget.org/v3-flatcontainer/microsoft.ml.tokenizers/index.json>; nuspec; spike CUR-27 | T1 + T0 | NuGet.org | 3.0.0, published 2026-11-11 | 2026-11-20 | time-sensitive | verified | Phase 14 (slice 3) |
| CUR-25 | `intfloat/multilingual-e5-small` at revision `614241f622f53c4eeff9890bdc4f31cfecc418b3` (repository last modified 2026-04-02): licence **MIT**; `config.json` hidden size **384**, 512 positions, vocabulary 250,037, `bert` architecture; the card requires the prefixes `query: ` and `passage: `. Files: `onnx/model_qint8_avx512_vnni.onnx` 118,346,824 B SHA-256 `dd476dd0c2514e9b9be83aeb3853fac0763e0bdf4a71645407587d77c48a2d88`; `sentencepiece.bpe.model` 5,069,051 B SHA-256 `cfc8146abe2a0488e9e2a0c56de7952f7c11ab059eca145a0a727afce0db2865`; `tokenizer.json` (reference only, not shipped) 17,082,730 B SHA-256 `0b44a9d7b51c3c62626640cda0e2c2f70fdacdc25bbbd68038369d14ebdf4c39`. Downloaded hashes matched the registry's LFS hashes | <https://huggingface.co/api/models/intfloat/multilingual-e5-small/revision/main?blobs=true>; `config.json` and `README.md` at the pinned revision | T1 + T0 | Hugging Face | revision `614241f6…` | 2026-11-20 | time-sensitive (pinned by revision and hash) | verified; **chosen (D-105)** | Phase 14 |
| CUR-26 | `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` at revision `e8f8c211226b894fcb81acc59f3b34ba3efd5f42` (last modified 2026-01-28): licence **Apache-2.0**; `onnx/model_quint8_avx2.onnx` 118,453,870 B SHA-256 `98a01d88b7de996cdea58c32ca71208c09968d143798814b2ea09d3439dc334f`; uses the same `sentencepiece.bpe.model` hash as CUR-25. Output dimension not read from its config: NOT_ASSESSED | <https://huggingface.co/api/models/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/revision/main?blobs=true> | T1 | Hugging Face | revision `e8f8c211…` | 2026-11-20 | time-sensitive | verified (licence, size, hash); fallback only, not spiked | Phase 14 |
| CUR-27 | Spike outside `src` (scratch folders deleted afterwards): `SentencePieceTokenizer` with the fairseq id mapping (`<s>`=0, `</s>`=2, `<unk>`=3, other ids +1) matched the reference `tokenizer.json` ids (Python `tokenizers` 0.22.2) for **7 of 8** strings, including French and `Wanjirũ wa Gĩkũyũ`; the miss was a string with **trailing spaces**, where the reference emits one extra `▁` (id 6). With whitespace trimmed and runs collapsed, 8 of 8 match, so the provider must normalise whitespace before tokenising. The qint8 "VNNI" file **runs on this CPU (Intel Xeon E5-1620 v4, AVX2, no AVX-512 or VNNI)** with ONNX Runtime 1.32.0: load 482–614 ms, 11–73 ms per short sentence on 2 threads, output `last_hidden_state` [1, n, 384]; mean-pooled, L2-normalised vectors are unit length and deterministic across runs; cos(query "books about light and colour", light passage) 0.816 vs 0.765 for an unrelated French passage | Local run, 2026-11-20 | T0 | This machine, Windows 11 x64 | ORT 1.32.0, Tokenizers 3.0.0 | 2026-11-20 | context-bound | verified (local measurement); throughput on the corpus is Phase 14 slice 7 | Phase 14 |

## Model-currentness review (AI Reading Advisor)

| Field | Value |
|---|---|
| Runner for this audit | Claude Code, model `claude-opus-5-5` (the audit session only; this is **not** a product runtime choice) |
| Product Advisor runtime | No live provider: only `AiDisabledProvider` is registered (`AiServiceExtensions.cs:66`). The code references provider key `openai` with model `gpt-test`, a placeholder rather than a real model ID. |
| Latest official releases checked | **NOT_ASSESSED.** No provider catalogues were queried in this cycle. |
| Account and runtime availability | NOT_ASSESSED. No provider keys are configured on the audit machine. |
| Quality, cost and latency evidence | NOT_ASSESSED. There is no evaluation set yet (Phase 16 builds one). |
| Retain or change decision | **Change required.** Remove the `gpt-test` placeholder. Phase 15 must select exact model IDs from primary provider documentation under owner decision D-06, record access and review dates, and re-run this review before release (Phase 28). |
| Open action owner | Phase 15, re-checked in Phase 28 |

## Rules for executors

1. Before a phase uses an external rule as an acceptance criterion, it must add a T1 record here
   with URL, publication or version date, access date, verification date and review date.
2. Re-check `time-sensitive` claims at the start of the consuming phase and again in Phase 28.
3. A `NOT_ASSESSED` claim that blocks release keeps the release gate open. It is never replaced by
   a remembered default.
