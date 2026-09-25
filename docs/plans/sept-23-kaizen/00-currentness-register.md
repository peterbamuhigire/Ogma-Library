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
| CUR-14 | A small local ONNX embedding model with acceptable licence, size and quality exists for bundling (decision D-05) | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phase 14 |
| CUR-15 | The provider keys the code allows are `disabled`, `openai`, `deepseek`, `anthropic` and `ollama`, with fixed-host egress checks; the view models hard-code `"openai"` / `"gpt-test"` | `AiProviderProfileService.cs:169-211`, `RecommendationPanelViewModel.cs:376`, `ReadingPlanViewModel.cs:155` | T0 | Repo @ `0ad3c0c` | — | 2026-09-25 | context-bound | verified (local measurement) | Phase 15 |
| CUR-16 | The exact provider model IDs, their availability, pricing and data-use terms for the Advisor | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** (see model-currentness review) | Phase 15 |
| CUR-17 | Metadata provider (Google Books, Open Library) terms, quotas and attribution rules | Prior records cite official terms, but not re-verified this cycle | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phase 08 |
| CUR-18 | WCAG 2.2 Level AA is the accessibility target, and its success criteria apply to desktop UI through WCAG2ICT | Not checked this cycle | — | — | — | — | stable text, context-bound application | **NOT_ASSESSED** | Phase 21 |
| CUR-19 | Windows code signing requirements (certificate type, key storage, timestamping, SmartScreen reputation) and MSIX packaging rules | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phase 26 |
| CUR-20 | macOS Developer ID signing, hardened runtime and notarisation requirements (tooling and entitlements for WKWebView) | Not checked | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phases 26, 27 |
| CUR-21 | Data-protection obligations for processing minors' data in schools (DPIA, controller approval) in the owner's jurisdictions | Prior records mark these open | — | — | — | — | time-sensitive | **NOT_ASSESSED** | Phase 23 |
| CUR-22 | Startup and runtime measurements: 3.7–4.1 s warm launch to window; 23.9 s under concurrent test load; working set 182–252 MB; scan of 17 files in about 15 s; close in 3.75 s | Audit runs (see [02-test-report.md](02-test-report.md)) | T0 | Audit machine, synthetic corpus | 2026-09-25 | 2026-09-25 | context-bound | verified (local measurement) | Phase 24 |

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
