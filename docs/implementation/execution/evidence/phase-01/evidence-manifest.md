# Phase 1 evidence manifest

Evidence ID: `OGMA-EV-P01-20260820`

Captured: 2026-08-20, Africa/Kampala

Execution baseline commit: `de8a42429ee353db800bba5b1439d902b4543733`

Execution baseline tree: `74aa3937fcd8049fd1dca49dcdb681c382bbe6de`

Audit provenance commit: `5514276fba5755335f754ad8db4c824783e9d6a4`

## Environment

| Item | Evidence |
| --- | --- |
| Host OS | Windows 10.0.26200, x64 |
| .NET SDK | 10.0.100 |
| Target framework | `net10.0` |
| Repository branch | `main`; two approved local commits ahead of `origin/main` before Phase 1 |
| Physical platform assessed | Current Windows development host |
| Time zone | Africa/Kampala |
| Source of private content | None; tests use repository fixtures and generated data |

## Baseline hashes

| Artifact | SHA-256 or Git tree |
| --- | --- |
| `OgmaLibrary.sln` | `d3328a00bc8115518dab47f8e9cabfb3a76563244c9e0923b1a74f1bffb431ea` |
| `Directory.Build.props` | `425939b449cb23b398431a076dd7acde2ed2aef7bc157bfee3b2d195d5aea627` |
| Canonical SRS DOCX | `0289d07d183b112e89f867bbcac1d410d59d64e34913de32238c9db3b93c6162` |
| 39-phase roadmap index | `11f0f94054e006111fdee6cb6b72248f0536ebe9deb89f7908a835f9ba8f54fa` |
| Requirement-phase matrix | `80e88eea9a92151da19958df853e1f7b5b3d4681b90efebc3e1248a401c98fb2` |
| EF migration directory at baseline | Git tree `9f8a12e0e4dba07ca41e33ec5e90c3f4214a9c57` |
| PDF adapter directory at baseline | Git tree `5072de094bc898725c93ca789d28d270e28ebe46` |
| Search implementation at baseline | Git tree `ae62a6afd108a0ed015ba3807661455054e041d9` |
| Golden-corpus tests at baseline | Git tree `07bf66f6d4524c0ef55096444567d15f168d6119` |
| PDF tests at baseline | Git tree `949b447904c9a75e17b9d9b1a9d4774b126fbb07` |

## Version and assumption inventory

- Database migrations run through the Infrastructure EF migration set ending in
  the historical `Phase18SchoolAdminTables` migration. Phase numbers in migration
  names are historical and do not assert completion of the new roadmap.
- Search currently uses 512-token chunks with 64-token overlap.
- Embeddings currently identify `nomic-embed-text` with version
  `nomic-embed-text:latest`; explicit content/extraction/chunking/model lifecycle
  is not frozen until Phases 25–26.
- Existing PDF adapters and isolation tests are baseline evidence only. The
  approved PDF processing contract is not frozen until Phase 10 and the
  extraction/OCR contracts are not complete until Phases 11 and 24.
- Existing advisor and 3D code are scaffolds under the scope-freeze rules.

## External integration inventory

| Integration | Endpoint class | Credential treatment | Evidence owner |
| --- | --- | --- | --- |
| Google Books | `https://www.googleapis.com/books/v1/` | No key value in evidence; configuration/status only | Phase 13 |
| Open Library | `https://openlibrary.org/` and cover service | No credential expected; cache/terms evidence required | Phase 13 |
| OpenAI-compatible | `https://api.openai.com/v1/` | OS-backed secret; never logged or committed | Phase 27 |
| DeepSeek-compatible | `https://api.deepseek.com/v1/` | OS-backed secret; never logged or committed | Phase 27 |
| Anthropic | `https://api.anthropic.com/v1/` | OS-backed secret; never logged or committed | Phase 27 |
| Ollama | loopback `http://localhost:11434/` | Local-only endpoint; no cloud credential | Phases 25 and 27 |

Live endpoint behavior, provider terms and credentials were not exercised in
Phase 1. They are `NOT ASSESSED`, not passed.

## Gate results

| Gate | Result | Evidence |
| --- | --- | --- |
| Requirement accountability | PASS | 101 FR, 29 NFR, 32 controls; 162/162 mapped |
| Locked restore | PASS | `dotnet restore ... --locked-mode` |
| Format | PASS | No changes required |
| Release build | PASS | 0 warnings, 0 errors; 43.81 s initial run |
| NuGet vulnerability scan | PASS | No vulnerable direct or transitive package reported |
| .NET analyzer scan | PASS | No analyzer formatting violation at warning severity |
| High-confidence secret scan | PASS | No matching credential/private-key pattern in scoped source or workflow files |
| npm vulnerability scan | PASS after remediation | esbuild upgraded from 0.28.0 to 0.28.2; 0 known vulnerabilities |
| 3D typecheck and deterministic bundle | PASS | TypeScript check and esbuild bundle completed |
| 3D performance budget | PASS | shelf p95 0.143 ms; grid3d p95 0.103 ms on this host |
| Architecture tests | PASS | 37/37 |
| UI render tests | PASS | 126/126 |
| Core/integration tests | PASS after triage rerun | 637/637; complete solution 800/800 |
| LAN Host cohort | PASS | 59/59 |
| LAN concurrent catalogue focused stability | PASS | 12 consecutive runs, 20 clients per run |
| LAN concurrent page render | PASS | 10 clients; p95 threshold under 2,000 ms |
| macOS CI and physical behavior | NOT ASSESSED | No matching current commit run available locally |
| Windows/macOS signing and installer verification | NOT ASSESSED | Scheduled for Phases 38–39 |
| Live bibliographic providers | NOT ASSESSED | Scheduled for Phase 13 quarantine evidence |
| Live AI providers and model quality | NOT ASSESSED | Scheduled for Phases 27–30 |
| Physical assistive technology | NOT ASSESSED | Scheduled for Phases 18, 21, 33 and 39 |

The first complete baseline run produced one HTTP 500 in the 20-client LAN
catalogue smoke test. The endpoint passed a focused rerun, 12 consecutive focused
runs, all 59 LAN tests and the next complete 800-test run. No deterministic code
fault was established. The test now includes response status and body in its
failure message so a recurrence yields actionable evidence. This event remains a
reliability watch item for Phase 17 rather than being hidden or declared fixed.

The first npm audit reported GHSA-g7r4-m6w7-qqqr against esbuild 0.28.0. The
direct development dependency and lockfile were upgraded to 0.28.2. Audit then
reported zero vulnerabilities and all 3D gates were rerun successfully.

## Exclusions and redactions

No private PDF, extracted passage, user note, prompt, API key, access token,
credential value or personal filesystem root is included. Machine-specific temp
paths and generated certificates are intentionally omitted. Unavailable gates
are labeled `NOT ASSESSED`.
