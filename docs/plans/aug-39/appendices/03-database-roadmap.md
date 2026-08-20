# Database Roadmap

> Part of the canonical [August 39-phase desktop roadmap](../README.md).

SQLite remains the local system of record. Derived FTS/vector/assets can be rebuilt, but curation, annotations, identity and audit cannot. Every migration is transactional where SQLite permits, preceded by verified backup and followed by integrity checks. Forward-recovery is mandatory where a true down migration would lose newly captured user data.

| Phase | Schema work | Indexes/constraints | Data migration | Rollback / recovery |
| ---: | --- | --- | --- | --- |
| 1 | Record current schema/migration hashes | None | None | Evidence only |
| 2 | Startup migration state/health if needed | migration lock | Preserve settings | Restore verified pre-start backup |
| 4 | Roots, file occurrences, content assets, identity decisions, robust edition/work links | unique content hash+version; root-relative uniqueness; FKs/checks | Each legacy book→provisional edition/work/file; preserve curation/provenance | Dry run, backup, alias map, forward repair |
| 5 | Root canonical/bookmark/volume/health fields | canonical locator and active-root indexes | Existing configured folder→root | Relink/restore root row |
| 6 | ScanSession, StageExecution, leases, retry/error taxonomy | due-work/lease/idempotency indexes | Map generic jobs; unknown→dead letter | Keep old job archive until acceptance |
| 7–8 | Observations/checkpoints/presence/relocation audit | root/session/path/hash lookup | Revalidate old unavailable flags | No absence change on incomplete migration scan |
| 9 | External identifiers, match candidates, merge/split aliases | normalized source+ID uniqueness; candidate blocks | Legacy duplicates become proposals only | Reversible decisions via aliases/audit |
| 10–11 | Validation/extraction runs, page quality, TOC, identifier evidence, manifests | asset+extractor version uniqueness | Mark legacy outputs unverified | Side-by-side artifacts; source untouched |
| 12 | Canonical sourced fields, contributor roles, override locks, confidence versions | scope/source/field constraints; normalized identifier indexes | Preserve legacy value as explicit provenance | Restore proposal/canonical snapshot |
| 13 | Provider request/cache/quota/retention | provider+query+version unique; expiry indexes | Classify legacy raw responses | Purge cache safely; proposals persist |
| 14–15 | Review batches/undo and writeback plan/backup audit | optimistic concurrency and immutable audit constraints | Flag ambiguous auto-applied fields | Undo commands; file restore verification |
| 16 | Visual asset manifests/variants/custom locks | asset+variant+generator uniqueness | Import/regenerate legacy JPEGs | Retain old asset until verified replacement |
| 17 | Dead-letter/diagnostic retention | queue/lease/retention indexes | Convert remaining handlers | Repair command and archive |
| 18–20 | Preferences, saved views/smart shelves; validate organisation/reading constraints | covering catalogue/facet/sort indexes | Normalize legacy preferences/tags/status | Version preferences; preserve notes |
| 21 | Coordinate/export versions and reading durability fields | book/user/layer/page indexes | Convert annotation coordinates | Fallback reader for legacy coordinates |
| 22 | Normalized fuzzy-search projection | term/prefix/filter indexes | Background backfill | Old exact search remains until swap |
| 23–24 | FTS version/checkpoints; OCR page sources | FTS integrity and artifact-version indexes | Side-by-side rebuild | Drop/rebuild derived projection only |
| 25 | Complete vector compatibility tuple/tombstones/index metadata | source/chunker/model/dimension uniqueness | Legacy vectors isolated | Verify new index then purge old |
| 26 | Relevance judgments/evaluation runs/fusion parameters | run/query/result indexes | None | Versioned parameters allow revert |
| 27 | Provider profiles, consents, budgets, retention/deletion | consent/provider/version and audit indexes | Import config, never plaintext secrets | Disable provider; retain consent audit by policy |
| 28–30 | Intent/candidate/evidence/history/evaluation versions | cache compatibility and retention indexes | Label old explanations ungrounded | Delete derived artifacts; preserve user choices |
| 31–33 | Optional 3D preferences only | user/view indexes | Reset incompatible camera state | Fall back to 2D |
| 34 | Host identity, publication, roles, sessions, revocation | published-scope/user/session indexes | Disable/revalidate legacy host config | Host stays off on failure |
| 35 | Host-scoped client cache/private sync state | host+user+record uniqueness | Securely re-pair or invalidate legacy tokens | Clear remote projection; local library unaffected |
| 36 | Classes/policies/quotas/minors/audit/backup metadata | role/policy/quota/retention indexes | Default deny and review | Restore policy backup; rotate keys |
| 37 | Retention/erasure manifests; optional encryption only after ADR | erasure due/audit integrity indexes | Dry-run classification | Verified backup and restore before encryption |
| 38 | Release/schema compatibility and update state | migration version constraints | Rehearse last-supported upgrade | Signed rollback/forward-recovery drill |
| 39 | No new schema except release-blocker | freeze | Final backup/export/reimport proof | Any change restarts release gates |

## Invariants that must never be contradicted

- A file occurrence belongs to one root and may reference one content asset; file facts are not stored only on a bibliographic book.
- One content asset may have multiple physical occurrences.
- An edition may have multiple assets; a work may have multiple editions.
- Automated proposals cannot overwrite protected user fields.
- Derived artifacts have complete compatibility keys and can be invalidated independently.
- A failed/unavailable root cannot cause destructive catalogue deletion.
- Secrets are references to OS stores, never plaintext database fields.
- Classroom/private state is host- and user-scoped.


