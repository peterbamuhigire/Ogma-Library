# Architecture Decisions Required

> Part of the canonical [August 39-phase desktop roadmap](../README.md).

Existing accepted v2.1 ADR decisions remain authoritative unless this audit identifies an implementation-level choice or a conflict. Decisions already settled—.NET 10, Avalonia, SQLite/local sidecars, local Tesseract, provider-neutral AI, opt-in classroom host, and Windows/macOS-only desktop scope—must not be reopened without evidence.

## Decision 1 — File / asset / edition / work identity

### Why It Matters
Every metadata, duplicate, search, AI and reconciliation behavior depends on it.

### Existing Implementation
Hashes on `BookRow`, incomplete `BookFileRow`, schema-only work/edition and path placeholder hashes.

### SDLC Position
Requires physical vs bibliographic identity and work/edition operations.

### Options
A) Preserve book-centric rows; B) file occurrence→content asset→edition→work; C) fully generic entity graph.

### Recommendation
Option B: explicit, constrained and understandable without over-generalization.

### Consequences
Early migration and repository rewrite; major downstream simplification. Decide/freeze Phase 3–4.

## Decision 2 — Root locator and platform semantics

### Why It Matters
Windows/macOS paths, external disks, permissions and bookmarks differ.

### Existing Implementation
Single path setting and string-prefix containment.

### SDLC Position
Local-first, multi-root, portable and safe.

### Options
A) raw absolute path; B) root row + platform locator/bookmark + relative paths; C) copy all PDFs into managed storage.

### Recommendation
Option B. Never take ownership/copy without an explicit import mode.

### Consequences
Platform adapters and physical tests; stable relink behavior. Phase 5.

## Decision 3 — Worker/queue topology

### Why It Matters
Heavy work needs durability without unnecessary deployment complexity.

### Existing Implementation
In-process polling over a generic Jobs table.

### SDLC Position
Background, recoverable, observable local desktop work.

### Options
A) durable SQLite leased queue in process; B) external broker/service; C) OS scheduler.

### Recommendation
Option A initially, with handler contracts that permit later process isolation; PDF remains separately sandboxed.

### Consequences
Atomic lease implementation and SQLite contention benchmarks. Phases 6/17.

## Decision 4 — PDF containment mechanism per OS

### Why It Matters
Environment flags and child process boundaries do not prevent data access.

### Existing Implementation
Subprocess plus Windows kill-on-close/process-count Job Object.

### SDLC Position
Untrusted input, no network/child process, resource limits.

### Options
Windows AppContainer/restricted token/low-integrity broker; macOS sandbox profile/app sandbox/XPC helper; lighter process-only isolation.

### Recommendation
Prototype and select a brokered OS-enforced adapter per platform in Phase 10; reject process-only as non-compliant.

### Consequences
Packaging entitlements/native work; higher security assurance.

## Decision 5 — Full-text and vector storage

### Why It Matters
50k catalogues and content chunks cannot be brute-force loaded indefinitely.

### Existing Implementation
SQLite FTS5 and SQLite-stored vectors scored in memory.

### SDLC Position
Local-first hybrid search with rebuildable indexes.

### Options
A) FTS5 + bounded two-stage SQLite vectors; B) SQLite vector extension; C) embedded external vector engine.

### Recommendation
Keep FTS5. Benchmark A/B/C using packaged Windows/macOS support, migration, memory and 50k relevance before freezing Phase 25; do not choose solely from novelty.

### Consequences
Potential native dependency; complete compatibility/version contract remains invariant.

## Decision 6 — Metadata match thresholds and precedence

### Why It Matters
Bad automation corrupts trust.

### Existing Implementation
0.70 auto-apply and automatic writeback.

### SDLC Position
User override wins; low confidence requires review; writeback confirmed/reversible.

### Options
A) universal threshold; B) rule/evidence-specific calibrated policy; C) manual all matches.

### Recommendation
Option B with mandatory review for conflicts/low certainty and no automatic file writeback.

### Consequences
Evaluation corpus and explainable proposals. Phases 12–15.

## Decision 7 — AI provider/payload policy

### Why It Matters
Titles, interests, notes and passages are private and provider capabilities differ.

### Existing Implementation
Good tier concepts, incomplete runtime gateway.

### SDLC Position
Provider-neutral, local option, preview/consent, disabled default.

### Options
A) one cloud provider; B) enforced capability gateway with local/cloud adapters; C) local only.

### Recommendation
Option B, while preserving a fully useful provider-off core and local-first defaults; personal notes excluded by default.

### Consequences
Provider governance/conformance overhead. Phase 27.

## Decision 8 — 3D host and renderer boundary

### Why It Matters
Signature UI requires two native WebViews and must not duplicate catalogue logic.

### Existing Implementation
Three.js bundle and empty bridge facades.

### SDLC Position
Three.js embedded via WebView with accessible 2D alternative.

### Options
A) finish WebView2/WKWebView adapters; B) native 3D engine; C) remove 3D.

### Recommendation
Option A after an early physical host proof; retain option C as release fallback if Phase 31 cannot meet security/platform gates, rather than ship a broken gimmick.

### Consequences
Secure message/asset contract and JS build remains internal. Phases 31–33.

## Decision 9 — Distribution channels and update trust

### Why It Matters
No current executable release path exists.

### Existing Implementation
CI build/test only; ADR-0009 selects Velopack + MSIX + notarized macOS direct distribution.

### SDLC Position
Signed artifacts, independent feed verification and rollback.

### Options
Already decided at product level; implementation choices concern signing service, key custody and exact store timing.

### Recommendation
Implement direct signed Windows/macOS channel first, plus required MSIX; use protected signing service/HSM-grade custody; promote immutable artifacts.

### Consequences
Certificates/accounts and physical clean-machine labs are release dependencies. Phases 38–39.

## Decision 10 — At-rest database encryption

### Why It Matters
Catalogue, notes, AI history and classroom identities may be sensitive.

### Existing Implementation
Unencrypted SQLite; OS account permissions only.

### SDLC Position
Control set calls for an explicit protection/backup decision rather than magical security.

### Options
A) OS permissions + encrypted device/backups; B) SQLCipher/embedded encryption with OS-store key; C) encrypt selected sensitive columns.

### Recommendation
Threat-model and benchmark B/C in Phase 37. Do not ship application-level encryption without tested key recovery/backup/rotation; document A clearly if accepted for standalone.

### Consequences
Native packaging, migration and recovery complexity.

## Decision 11 — FR-EXT-002/003 release tier

### Why It Matters
Local API/imports/themes are normative but less central than integrity/advisor/3D.

### Existing Implementation
Extension contracts only; full features absent.

### SDLC Position
FR-EXT-001..003 included.

### Options
A) deliver before Phase 39; B) formal SRS deferral; C) silently omit.

### Recommendation
Choose A or B in Phase 1; C is prohibited. If A, stage validated imports via Phase 14 and keep any API loopback/read-only/authorized.

### Consequences
Scope/load transparency without distorting the critical path.


