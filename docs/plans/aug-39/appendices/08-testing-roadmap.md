# Testing Roadmap

> Part of the canonical [August 39-phase desktop roadmap](../README.md).

## Test architecture

| Layer | Purpose | Runs |
| --- | --- | --- |
| Unit/property | Domain invariants, parsers, ranking, state transitions, redaction | Every change |
| Database | Schema, constraints, migrations, FTS/vector lifecycle, concurrency | Every change |
| Component/integration | Providers, workers, PDF broker, repositories, host/client | Every change with recordings/fakes |
| Avalonia headless | View models, navigation, states, snapshots, keyboard basics | Every change |
| Physical platform | WebView, filesystem, secrets, accessibility, PDF containment, packaging | Nightly/RC on Windows and macOS |
| E2E | User workflows from root selection to read/search/advice/3D/classroom | Nightly/RC |
| Security/privacy | Hostile corpus/paths/messages/APIs, egress, erasure, signing | Scheduled and RC |
| Performance/reliability | reference datasets/hardware, soak, crash/fault injection | Scheduled and RC |
| AI/relevance | deterministic offline benchmark + quarantined live providers | Every retrieval change / scheduled live |

## Phase gates

| Phases | Mandatory emphasis |
| --- | --- |
| 1–4 | requirements/evidence, identity properties, migration up/down/forward recovery and data preservation |
| 5–9 | multi-root, path/symlink, rename/move/replace/delete/disconnect, duplicates and ambiguity |
| 10–11 | hostile/malformed/password/image/large/Unicode PDFs, real sandbox escapes/resource limits |
| 12–16 | metadata provenance/conflict/overrides, provider recordings/outages, writeback consent/restore, image assets |
| 17 | leases, duplicate workers, crash/kill/resume, poison jobs, log redaction and load |
| 18–21 | visual regression, localisation, keyboard/AT, catalogue scale, reader durability/export |
| 22–26 | typo, FTS page anchors, OCR quality, vector invalidation and IR relevance/scale |
| 27–30 | gateway bypass, secrets/payload/consent/cost/deletion, intent, grounding, prompt injection and relevance |
| 31–33 | real WebView2/WKWebView, secure bridge, screenshots/interactions, GPU/memory and 2D fallback |
| 34–36 | physical multi-machine TLS/TOFU/RBAC/range/offline/sync/quota/minors/managed-AI |
| 37 | independent hostile/security/privacy/control evidence |
| 38–39 | full regression, clean install, signing/notarization/update/tamper/rollback/UAT |

## Fixture catalogues

1. **Filesystem/PDF corpus:** valid, metadata-poor, misleading, ISBN/no-ISBN, malformed, encrypted, image-only, mixed, Unicode/font, huge, exact copy, duplicate edition, different edition, similar title.
2. **Catalogue scale:** deterministic 50, 250, 1k, 2k, 5k, 10k and 50k records with realistic authors/tags/descriptions/files/assets.
3. **Search/AI benchmark:** human judgments for topic, mood, difficulty, length, comparison, combination, negative and surprise requests.
4. **Security corpus:** traversal, symlink/reparse, hostile image/PDF, malformed provider/model responses, prompt injection, WebView message/navigation and LAN authorization cases.

Record generator/license/provenance, expected behavior and immutable fixture hash.

## Metrics

- Search: p50/p95/p99, Precision@K, Recall@K, MRR/nDCG and fallback correctness.
- Advisor: candidate Recall@20/50, Precision@3, diversity, constraint satisfaction, unavailable/duplicate rate, attribution coverage, unsupported-claim rate, latency/tokens/cost.
- 3D: TTI, FPS/p95 frame time, draw calls, CPU/GPU/texture memory and input latency.
- Reliability: duplicate execution, recovery time, queue lag, crash-free sessions and data-loss count (must be zero).
- Accessibility: automated rules plus documented Narrator/VoiceOver task completion.

## Evidence rules

Every report records commit, OS/hardware, dataset hash, dependency/model/prompt versions and date. Mock tests are labeled as such. A retry cannot turn a flaky failure into release evidence. Missing physical or live evidence is `NOT ASSESSED`. Phase 39 publishes a traceable evidence index, not raw sensitive logs.


