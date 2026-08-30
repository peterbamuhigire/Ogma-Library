# Testing Gap Analysis

## Current quality baseline

At commit `5514276...`, locked restore, Release build, formatting, warning-level analyzers and all 800 automated tests pass on the audit Windows workstation. The suite contains 637 core tests, 126 Avalonia headless/UI tests and 37 architecture tests. CI is configured for `windows-latest` and `macos-latest`. This is a strong engineering asset.

The suite also creates a false sense of completeness when tests assert that placeholders render or mock providers return parseable data. Automated source-level parity is not physical-platform, security, GPU, packaging, usability or relevance acceptance.

## Coverage assessment

| Test area | Existing evidence | Gap | Required evidence |
| --- | --- | --- | --- |
| Unit/domain | Broad | Some identity rules encode unsafe semantics | Rewrite identity/property tests around file/edition/work invariants |
| Database/migrations | EF and repository tests | Rollback, corruption, large migration and legacy-data proof weak | Up/down/backup/restore, constraints, interrupted migration |
| Scanner/filesystem | Discovery/identity tests | Multi-root, root disconnect, permission loss, symlink/mount, network/external volume absent | Hermetic filesystem matrix on Windows and macOS |
| PDF processing | Parser/worker tests and fixtures | Hostile/malformed corpus, OS isolation, huge files, real password flows limited | Quarantined corpus, resource ceilings, subprocess kill/retry |
| Metadata | Detectors/providers/confidence tests | Live contracts, cache/quota, wrong-provider result and writeback consent absent | Recorded-contract tests, proposal review and no-write-without-confirmation |
| Covers | Generation tests | Read-model/UI connection, variants, invalidation and corrupt assets | Snapshot/manifest and end-to-end cover tests |
| Search | FTS/vector synthetic tests | Fuzzy, realistic relevance, scale, stale index and scores | Labeled corpus, Recall/MRR/nDCG and 50k performance |
| AI/RAG | Mock structural tests | Live runtime DI, semantic candidate recall, grounding, false claims, provider failure | Offline eval + quarantined live-provider suite |
| 2D UI | 126 headless tests/screenshots | Visual polish, keyboard journeys, AT, DPI/theme/platform behavior | Screenshot regression plus Narrator/VoiceOver/manual acceptance |
| 3D | Layout/VM/bridge contract tests | No native WebView, GPU FPS, texture memory, input/accessibility | Windows/macOS WebView E2E and reference-hardware benchmarks |
| Classroom | Service/integration tests | Multi-machine LAN, hostile access, disconnect/reconnect, load | Physical network lab and adversarial authorization suite |
| Security | Path/secret/process unit tests | PDF sandbox not real, no penetration test, signing/update trust absent | OS containment and hostile file/API/update tests |
| Privacy | Tier/audit tests | Reachability, exact payload, retention/erasure, provider evidence | User-journey, data-flow capture and deletion verification |
| Reliability | Job tests | Leasing, duplicate workers, crash/kill/resume and swallowed save error | Fault injection and deterministic recovery suite |
| Performance | Small synthetic tests; 3D arithmetic script | No reference library/hardware/provider/GPU measurements | 2k/5k/50k library and 50/250/1k/5k/10k 3D tiers |
| Packaging | None executable | Installer/signing/notarization/update/rollback absent | Clean VM/Mac install, tamper, upgrade/downgrade and rollback |

## Required PDF and filesystem fixture matrix

- Valid text PDF with correct metadata.
- No metadata and misleading embedded metadata.
- ISBN in filename, copyright page, multiple ISBNs and no ISBN.
- Malformed xref, truncated file, encrypted/password-protected PDF.
- Image-only PDF, mixed text/image PDF, unusual Unicode and embedded fonts.
- Very large byte size and page count with enforced memory/time ceilings.
- Exact byte copy, different rendering of same edition, different edition of same work and similar title.
- Rename, move within root, move across roots, replace-in-place, delete, root move, permission loss and temporary external-drive disconnect.
- Symlink/reparse point, traversal-like name, case-variant paths and network-volume interruption.

Use generated or licensed fixtures; record provenance and expected behavior. One bad file must not block a scan batch.

## AI/search evaluation suite

Create a stable catalogue snapshot and human judgments for topic, mood, difficulty, length, comparison, combined, negative and surprise queries. Measure candidate retrieval before generation. Every run records code commit, database snapshot, extractor/chunker/embedding/model/prompt versions and provider. Required metrics include Recall@K, Precision@K, MRR/nDCG, unavailable-book rate, unsupported-claim rate, attribution coverage, diversity, latency and cost.

## Quality-gate policy

1. Unit and integration tests run on every change.
2. Windows/macOS CI builds remain mandatory.
3. Platform/WebView/PDF-security tests run on signed release candidates in controlled environments.
4. Provider-contract suites use recordings by default and quarantined live calls on a schedule.
5. Performance results come from named reference hardware and representative datasets.
6. Placeholder tests must be labeled as scaffold tests and cannot satisfy a functional requirement.
7. Flaky tests are quarantined with owner and expiry, never silently retried into green.
8. Phase 39 requires dated evidence packs, not prose assertions.

