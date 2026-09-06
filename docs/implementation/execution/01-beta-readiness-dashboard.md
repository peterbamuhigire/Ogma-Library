# Ogma Library Beta-Readiness Dashboard

Date: 2026-09-06
Authority: [39-phase execution ledger](00-execution-status.md) and the
[approved Aug-39 roadmap](../../plans/aug-39/README.md)

## Decision summary

The repository is implementation-ready for continued validation, but it is
not beta-release-ready. Local implementation and automated gates are evidenced;
physical platform, reference-machine, legal/privacy, signing, and owner gates
remain open. `COMPLETE` means the phase's local implementation gates are
closed; it does not imply later physical release acceptance.

## Current validation

The latest complete protected-`main` regression at `8321ea6` passed 1,136 tests
per platform with 0 failures and 0 skips: 936 core, 41 architecture, and 159 UI
on both Windows and macOS. The authoritative record is
`evidence/ci-cross-platform-regression-2026-09-06.md`.

```text
dotnet test OgmaLibrary.sln --configuration Release --no-restore --logger "console;verbosity=minimal"
```

## Phase disposition

| Phase | Local implementation delivered | Remaining gate / disposition | Authority |
| ---: | --- | --- | --- |
| 7 | Discovery, incremental scan, recovery | Cross-platform permissions and assistive-technology walkthrough: `NOT ASSESSED` | [phase-07](phase-07-progress.md) |
| 8 | Reconciliation, recovery, safe author binding, operator relocation review | Disconnected volume/ACL and cross-OS walkthrough: `NOT ASSESSED` | [phase-08](phase-08-progress.md) |
| 9 | Duplicate resolution, aliases, grouping, projections | Physical operator walkthrough and cross-platform UI: `NOT ASSESSED` | [phase-09](phase-09-progress.md) |
| 10 | PDF broker, password/resource validation, Windows Job Object startup | OS sandbox/escape proof and independent security approval open | [phase-10](phase-10-progress.md) |
| 11 | Versioned extraction, TOC/ISBN evidence, page-on-demand adapter, real corpus, 500-book mixed benchmark | Representative real target-scale acceptance, repeated production resource ceiling, and cross-platform evidence open | [phase-11](phase-11-progress.md) |
| 12 | Metadata scope, precedence, enrichment proposals, provenance | Physical UI walkthrough: `NOT ASSESSED` | [phase-12](phase-12-progress.md) |
| 13 | Provider cache/gateway wired to runtime enrichment, persisted/projected stale labeling, quota/circuit/retry/privacy controls, fixed-host attribution links | Legal owner review, archive, live provider/network and physical evidence open | [phase-13](phase-13-progress.md) |
| 14 | Review proposals, concurrency, bulk/tag mutation, review UI | Physical accessibility evidence open | [phase-14](phase-14-progress.md) |
| 15 | Hash guard, streaming backup preparation, verified same-directory source/undo/recovery promotion, consented writeback | Physical process-kill interruption and cross-platform permission evidence open | [phase-15](phase-15-progress.md) |
| 16 | Assets, manifests, embedded-source acquisition, lazy variants, provider boundary wired to enrichment, ingest/update spine scheduling, LAN authorization, and real-worker disk budget | GPU/reference-hardware budget and physical accessibility/cross-platform journeys open | [phase-16](phase-16-progress.md) |
| 17 | Leases, retries, distinct pause/dead-letter states, resource groups, safe queued cancellation, cooperative OCR controls, fail-safe unsupported-active pause semantics, metrics/diagnostics, and Windows/macOS hosted process recovery | Cooperative control for other active handlers, full-app crash/activity-centre, physical reference-machine process, and soak evidence open | [phase-17](phase-17-progress.md) |
| 18 | Design tokens, controls, localization increments including AI accessibility, 3D/directory fallback copy, theme/density, command palette, static route inventory, automated and rendered contrast evidence | Physical Windows/macOS screenshot review and Narrator/VoiceOver accessibility open | [phase-18](phase-18-progress.md) |
| 19 | 2D catalogue, cover fallback, paging, badges, authenticated assets | Keyboard/screen-reader and reference hardware open | [phase-19](phase-19-progress.md) |
| 20 | Detail curation, collections, smart shelves, history/TOC/provenance, relink wiring | Physical picker/relink, accessibility, E2E open | [phase-20](phase-20-progress.md) |
| 21 | Reader portability, import/export, split view, cache/session evidence | Platform viewer, crash, accessibility, reference performance open | [phase-21](phase-21-progress.md) |
| 22 | Structured/fuzzy search, facets, paging, highlighting, keyboard UI, localized fallbacks | Reference hardware and assistive technology open | [phase-22](phase-22-progress.md) |
| 23 | FTS filters, snippets, page jumps, rebuild/swap, 50k local latency | Reference hardware and assistive technology open | [phase-23](phase-23-progress.md) |
| 24 | Selective OCR policy, checksums, stable failures, cooperative page-boundary controls, 500-book benchmark, packaged-fixture resource telemetry | Representative real accuracy/resource corpus, cross-platform packaged assets, physical accessibility open | [phase-24](phase-24-progress.md) |
| 25 | Versioned vectors, stale/tombstone lifecycle, bounded memory/cache, swap/resume, explicit local token/zero-egress/zero-external-cost accounting | ANN/relevance, target-scale UI, reference corpus/machine open | [phase-25](phase-25-progress.md) |
| 26 | Hybrid ranking, filters, RRF, integrity, synthetic quality metrics, executable v1 search-contract freeze | Representative corpus, ANN quality, independent memory, reference machine open | [phase-26](phase-26-progress.md) |
| 27 | AI gateway, privacy tiers, cost/quotas, egress, credentials, retention/erasure UI | Provider terms/conformance and physical evidence open | [phase-27](phase-27-progress.md) |
| 28 | Intent parsing, candidate/reranking, comparison references, diagnostics | Human-labelled benchmark, reference machine, final UI/performance open | [phase-28](phase-28-progress.md) |
| 29 | Grounded local evidence, citations, consent, traces, abstention benchmark | Physical UI evidence open | [phase-29](phase-29-progress.md) |
| 30 | Advisor routes, feedback consent, evaluation runs, history erase, thresholds, frozen v1 retrieval dependency | Live evaluation, accessibility, physical picker open | [phase-30](phase-30-progress.md) |
| 31 | Typed 3D bridge, projection, accessible fallback, native WebView binding and secure loopback asset adapter | Physical WebView2/WKWebView, WebGL2 and integration evidence open | [phase-31](phase-31-progress.md) |
| 32 | Meshes, sharded asset URIs, interaction, atlas/LOD/motion/focus wiring | Reference confirmation and physical Windows/macOS evidence open | [phase-32](phase-32-progress.md) |
| 33 | Virtualization, texture residency, metrics, eviction, fallback | GPU/WebView/context-loss/cross-platform accessibility open | [phase-33](phase-33-progress.md) |
| 34 | Published scope, redaction, authenticated concurrency, host boundaries | Two-machine networking, firewall/mDNS/TOFU, hostile soak open | [phase-34](phase-34-progress.md) |
| 35 | Tamper-evident cache, bounded sync, deterministic reconnect renewal, concurrency-safe per-profile private-key isolation | Physical credential/pairing/network interruption/offline UX/two-user/load evidence open | [phase-35](phase-35-progress.md) |
| 36 | Key custody, scopes, quotas, DPIA minimization, managed-AI controls, transactional erasure audit | Physical E2E, backup/platform-key/erasure/accessibility/soak/formal DPIA open | [phase-36](phase-36-progress.md) |
| 37 | Code safety, headers, throttling, integrity, audit minimization, synthetic hostile-PDF redaction/recovery corpus | Physical/third-party hostile corpus, secret store, penetration, backup, cross-platform soak open | [phase-37](phase-37-progress.md) |
| 38 | Release descriptors, detached-signature verification, candidate package, migration checks, executable beta-v1 schema freeze | Signed artifacts, clean install, performance, recovery, rollback, owner approval open | [phase-38](phase-38-progress.md) |
| 39 | Fail-closed acceptance contract, exact beta schema-freeze binding, contract fixtures, 162-ID accountability | Reference machines, signing, install, performance, rollback, backup, owner acceptance open | [phase-39](phase-39-progress.md) |

## Governing residual gates

These are intentionally not closed by local tests or source inspection:

- Windows/macOS native UI, WebView, GPU, filesystem, credential-store, and
  assistive-technology behavior: `NOT ASSESSED` until physically run.
- Provider legal terms, privacy/DPIA approval, data-controller decisions, and
  owner acceptance: awaiting accountable review.
- Reference-machine performance, clean install/upgrade/rollback, backup/restore,
  signing/notarization, and hostile/soak drills: awaiting release evidence.

This dashboard is a navigation and decision aid. The per-phase progress record
and its linked evidence remain authoritative for each individual gate.
