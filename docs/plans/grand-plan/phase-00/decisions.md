# Phase 00 — Decision Closure Log

**Version:** 1.0  
**Baseline date:** 2026-05-30  
**Author:** Phase 00 execution (WP1 + WP2 + WP6)  
**Owner:** Peter Bamuhigire / Chwezi Core Systems  

This document closes every open question (OQ-01..OQ-08) and every SRS context
gap (CON-1..CON-9) identified in PRD §10 and SRS §8.5. For each item the
structure is: the question, the Decision, the Rationale, the Impact on
downstream phases, the owning ADR or phase, and a Sign-off line. Additional
cross-cutting decisions confirmed here (License, CLA, i18n, icons, LAN scope)
are recorded in the supplementary section. A Consolidated Owner Sign-off table
closes the document.

All decisions in DECISIONS.md (D-001..D-006) are incorporated here and do not
need re-confirmation; they are referenced inline. Where the PRD recommended
default conflicts with a DECISIONS.md entry, DECISIONS.md governs.

---

## Open Questions (OQ-01..OQ-08)

---

### OQ-01 — .NET Runtime Version

**Question:** Should Ogma Library target .NET 10 LTS or retain .NET 8 as named
in the original SRS baseline?

**Decision:** Target **.NET 10 LTS** (supported through 2028-11-14). .NET 8 is
permitted only as a temporary, ADR-recorded fallback behind a named, evidenced
blocker with a dated migration plan back to .NET 10 attached at the time the
blocker is filed. No blanket .NET 8 usage is permitted.

**Rationale:** .NET 8 reaches end of support on 2026-11-10, roughly six months
after first commit. Shipping on an out-of-support runtime is a security and
supportability defect. .NET 10 LTS provides a 2.5-year-plus support runway past
first launch; Avalonia, PDFium wrappers, and all other key dependencies support
it. The cost of a Phase 0 dependency-validation spike is lower than a forced
mid-build migration.

**Impact:** Every build artifact, the CI matrix, and the packaging pipeline
(ADR-0009) targets .NET 10. Any library that has not been validated on .NET 10
is a Phase 01 spike item. Phase 02 scaffolds the solution on .NET 10; no other
runtime is introduced.

**Owning ADR/Phase:** ADR-0001 (Accepted).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### OQ-02 — PDFium Wrapper Choice

**Question:** Which .NET wrapper for the PDFium engine should Ogma Library
adopt, and is the choice binding before Phase 01?

**Decision:** Adopt **PDFium via a .NET wrapper, isolated behind an internal
PDF adapter interface**. The specific wrapper is **not yet fixed**; it is
determined by a Phase 01 spike that benchmarks at least two wrapper options
(e.g. `PDFiumCore` and `PdfiumViewer` or equivalent) against render fidelity,
text-extraction quality, password-protected-document handling, and search-
feeding accuracy on the golden-corpus fixture set. The winning wrapper is
recorded as an amendment to ADR-0004 at the close of Phase 01.

**Rationale:** Wrapper quality varies across the ecosystem. Committing to one
wrapper before a benchmark is premature; the adapter boundary makes the choice
replaceable so the spike evidence, not a preference, drives the selection.
ADR-0004 is Accepted with the spike gate explicitly part of its outcome.

**Impact:** Phase 01 must include a timed PDFium-wrapper benchmark spike as a
required work item. No rendering, thumbnail, or extraction code is written
before the spike result is recorded. All reader, thumbnail, and indexer code
in Phase 05+ depends only on the adapter interface, never on the wrapper
directly.

**Owning ADR/Phase:** ADR-0004 (Accepted; wrapper identity to be amended after
Phase 01 spike).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied (spike-
gated), pending explicit sign-off in WP6 session.

---

### OQ-03 — Annotation Write-Back Strategy

**Question:** Should Ogma Library write annotations, highlights, and metadata
changes back into source PDF files, and if so, when and how?

**Decision:** **Database-first** (MVP and V1). Annotations, bookmarks, reading
state, and all metadata changes are stored exclusively in the SQLite catalogue
(ADR-0005) in the MVP. Source PDFs remain untouched. PDF write-back is a
**later, opt-in V1 capability**, off by default, enabled only after backup +
diff + verify + restore safety machinery is in place and the PDF engine write
path (ADR-0004) is proven through Phase 01 spike results. The schema is
designed from Phase 04 to accommodate write-back fields (last-written
timestamp, backup path, diff hash) so no schema migration is required when
write-back ships.

**Rationale:** Writing to untrusted or malformed PDFs before the safety net is
in place risks irreversible corruption of user files. The principle "metadata
is reversible" is not compatible with premature PDF mutation. DB-first delivers
all annotation and metadata UX on day one with zero file risk.

**Impact:** Phase 04 (Catalogue) must include write-back-ready columns in the
schema. Phase 07 (PDF Write-Back) implements the opt-in capability, gated by
ADR-0004 spike maturity. A write-back decision review occurs before Phase 03
scope is locked (per SRS §8.5 deadline).

**Owning ADR/Phase:** ADR-0008 (Accepted).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### OQ-04 — Thumbnail and Spine Storage

**Question:** Where should generated thumbnail, spine, and cover images be
stored — inline in the SQLite catalogue as BLOBs, or as files in a dedicated
sidecar folder?

**Decision:** **Sidecar folder on disk**. All regenerable derived assets
(thumbnails, cover images, spine images, extracted-text caches, embeddings)
live in a structured sidecar asset folder alongside the SQLite catalogue file.
The catalogue holds only the relative path reference to each sidecar file, not
the bytes. The sidecar uses a SHA-256-sharded naming convention per Phase 04
decision (see `docs/plans/grand-plan/phase-04/README.md` §Sidecar and
CON-6 below) to avoid filesystem namespace exhaustion on large corpora.

**Rationale:** BLOBs in the catalogue bloat the database file, slow backups,
and mix regenerable data with the catalogue of record. The sidecar folder keeps
the catalogue compact and fast; derived assets are regenerable and are cleanly
excluded from structured backups while remaining part of the portable export
bundle. SQLite WAL-mode performance is measurably better when the database does
not carry large binary columns.

**Impact:** Phase 04 defines the canonical sidecar path layout and
`ISidecarService`. All phases that produce thumbnails, spines, or embeddings
write through `ISidecarService`. The export bundle (NFR-PROD-009) zips the
catalogue plus the full sidecar subtree.

**Owning ADR/Phase:** ADR-0005 (Accepted); CON-6 (see below).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### OQ-05 — Full-Text Search in MVP Scope

**Question:** Should a full-text search (FTS5) index over extracted document
text ship in the MVP, or be deferred?

**Decision:** **Metadata search is MVP; FTS5 full-text search is deferred to
V1.** MVP delivers metadata search (title, author, tag, year, status, rating,
shelf) within the NFR-OGMA-003 budget (≤ 150 ms P95 at 2,000 books) using
structured SQLite queries. FTS5 over extracted document text, the associated
text-extraction pipeline, and the index manager ship in V1 (FR-SEARCH-002,
FR-SEARCH-006) as part of Phase 10.

**Rationale:** Metadata search satisfies the core "find a book" promise for
the MVP target personas (serious reader, collector, power librarian). FTS5
requires reliable text extraction (itself dependent on the Phase 01 PDFium
spike) and significant indexing pipeline work that is not on the MVP critical
path. Deferring reduces MVP scope without removing a user-visible capability
that is promised at launch.

**Impact:** Phase 04 schema includes the `ExtractedPages`, `SearchChunks`, and
FTS5-external-content tables (schema-ready, unpopulated in MVP). Phase 10
populates the index. ADR-0006 remains Accepted for the full hybrid search
architecture; only the FTS5 activation milestone moves to V1.

**Owning ADR/Phase:** ADR-0006 note (FTS5 V1 milestone); Phase 10.

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### OQ-06 — OCR in MVP Scope

**Question:** Should optical character recognition (OCR) of scanned PDFs ship
in the MVP?

**Decision:** **OCR is deferred to V1** (FR-READ-010). MVP does not index
scanned image-only PDFs. Scanned documents are displayed in the reader but are
flagged as "no text layer" in the catalogue and are excluded from search results
with a user-visible indicator. The golden-corpus fixture `gc-scanned-image`
is included in Phase 01 test setup to ensure the flagging path works correctly.
OCR via Tesseract ships as a background job in V1 (Phase 15).

**Rationale:** OCR integration (Tesseract, language packs, background job
scheduling, extraction quality validation) is material scope. The MVP reading
and catalogue experience is complete without it; deferring OCR to V1 keeps MVP
focused on the core promise for the majority of well-formed PDFs.

**Impact:** Phase 05 (Ingestion) must correctly flag scanned PDFs and report
them in the health dashboard stub. Phase 15 implements OCR. The golden-corpus
`gc-scanned-image` fixture must be licensed and present by Phase 02.

**Owning ADR/Phase:** Decision log; Phase 15.

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### OQ-07 — EPUB and CBZ Format Support

**Question:** Should EPUB and/or CBZ (comic book archive) support ship in the
MVP or V1?

**Decision:** **EPUB and CBZ are post-V1** (post-V2 gate). Neither format is
in MVP or V1 scope. The schema is designed to be format-extensible from Phase
04 (the `BookFiles` table carries a `FormatType` discriminator column so new
formats can be added without a structural migration). A format-extension point
is registered in the plugin SDK (Phase 23) so the community can deliver EPUB/
CBZ support independently of the core release train.

**Rationale:** Ogma Library's core promise is PDF mastery. Adding EPUB and CBZ
in MVP/V1 would dilute quality and extend the critical path with format-specific
reader, metadata, and extraction work. The schema-extensible design ensures the
decision is reversible and does not lock out community contribution.

**Impact:** Phase 04 schema must include the `FormatType` discriminator on
`BookFiles`. Phase 23 (Extension SDK) registers a `IBookFormatProvider`
extension point. No EPUB or CBZ rendering, extraction, or metadata work is
planned before Phase 23.

**Owning ADR/Phase:** Decision log; Phase 04 (schema); Phase 23 (SDK).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### OQ-08 — Cloud Synchronisation

**Question:** Should cloud sync of the library catalogue and/or PDF files be
included in MVP or V1?

**Decision:** **Cloud sync is excluded from MVP and V1.** The schema is
designed to be cloud-sync-ready from Phase 04 (all catalogue rows carry
`CreatedUtc`, `UpdatedUtc`, `SyncVersion`, and `DeviceId` columns sufficient
for a last-write-wins or CRDT-based sync strategy). Any cloud-sync feature is
**DPIA-gated**: a per-feature DPIA must be completed and owner-approved before
any cloud-sync capability begins Phase implementation. This gate is formally
tracked in Phase 19.

**Rationale:** Cloud sync is a significant compliance surface (GDPR, Uganda
DPPA, US COPPA/FERPA for the school track) and a meaningful engineering effort.
Including it in MVP/V1 would expand the compliance surface before the product
is stable. Local-first is a core product principle; cloud sync is an
enhancement, not a foundation.

**Impact:** Phase 04 schema must include the sync-readiness columns above.
Phase 19 (DPIA) must include a cloud-sync DPIA work item as a tracked open
item. No cloud-sync code is written before the DPIA is signed.

**Owning ADR/Phase:** Decision log; Phase 04 (schema); Phase 19 (DPIA gate).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

## Context Gaps (CON-1..CON-9)

---

### CON-1 — Reference Hardware Specification

**Question:** What are the two reference machines (Windows + macOS) that anchor
all NFR-OGMA-001..007 performance budgets?

**Decision:** The two reference machines are:

**Windows reference machine (W-REF-01):**
- CPU: Intel Core i5-10210U (Comet Lake, 4-core/8-thread, 1.6/4.2 GHz)
- RAM: 8 GB DDR4-2666 dual-channel
- Storage: 256 GB SATA SSD (sequential read ≈ 550 MB/s, random 4K ≈ 40 MB/s)
- GPU: Intel UHD Graphics 620 (WebGL2 supported, no discrete GPU)
- Display: 1920 × 1080, 96 DPI (non-HiDPI reference)
- OS: Windows 10 22H2 (build 19045) with latest cumulative updates

**macOS reference machine (M-REF-01):**
- CPU: Apple M1 (8-core: 4 performance + 4 efficiency, 3.2 GHz performance
  cluster)
- RAM: 8 GB unified LPDDR4X
- Storage: 256 GB Apple SSD (NVMe, sequential read ≈ 3.4 GB/s)
- GPU: Apple M1 7-core GPU (WebGL2 supported via WKWebView)
- Display: 2560 × 1664 Retina (@2x, 224 DPI)
- OS: macOS 13.6 Ventura (latest patch)

Full machine profiles, NFR mapping, and the "trend-only" caveat for phases
before Phase 20 are in `docs/governance/REFERENCE-HARDWARE.md`.

**Rationale:** Anchoring budgets to specific market-class machines (W-REF-01
is a representative mid-range 2020 business laptop; M-REF-01 is the entry-level
2022 MacBook Air M1) prevents NFR targets from being aspirational rather than
testable. The Windows machine is intentionally the weaker reference: most
NFR-OGMA budgets must be met on W-REF-01 to be meaningful.

**Impact:** All NFR-OGMA budget numbers in the SRS (cold start ≤ 3 s P95, page
turn ≤ 100 ms P95, 3D ≥ 60 FPS at 500 books, etc.) are interpreted relative to
W-REF-01 (Windows) and M-REF-01 (macOS). Phase 20 (Performance & Benchmarks)
is the first phase that requires physical access to these machines. All earlier
performance results are "trend-only" on developer hardware. Full NFR-to-machine
mapping: see `docs/governance/REFERENCE-HARDWARE.md`.

**Owning ADR/Phase:** `docs/governance/REFERENCE-HARDWARE.md`; Phase 20.

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session. **Owner ask: confirm or substitute
alternative machine models before Phase 20 benchmarks begin.**

---

### CON-2 — Installation Minimums

**Question:** What are the minimum and recommended system requirements that
will be documented in the product installer and store listings?

**Decision:**

| Attribute | Minimum | Recommended |
|---|---|---|
| RAM | 4 GB | 8 GB |
| Free disk (app only) | 500 MB | 1 GB |
| Free disk (corpus) | Grows with PDF collection | — |
| OS (Windows) | Windows 10 1903 (build 18362, WebView2 available) | Windows 11 22H2+ |
| OS (macOS) | macOS 13.0 Ventura (WKWebView with WebGL2) | macOS 14.0 Sonoma+ |
| CPU | Dual-core 2.0 GHz x64 or Apple Silicon | Quad-core 2.5 GHz+ or M1+ |
| GPU/WebGL2 | Required for 3D shelf; grid/list available without GPU | Discrete or Apple Silicon GPU |
| Network | Offline-capable; internet required for metadata lookup and AI features only | Broadband |

**Rationale:** 4 GB RAM is the minimum that allows the Avalonia shell, SQLite
catalogue, and a PDF reader window to coexist without swapping. 500 MB covers
the application binary, WebView2 runtime stub (Windows), and initial catalogue.
Windows 10 1903 is the first version where the WebView2 fixed-version runtime
can be distributed without system-level dependency. macOS 13 is the WKWebView
baseline confirmed in ADR-0003.

**Impact:** Installer copy, store listing, and README must reflect these values.
Phase 02 CI matrix must include a Windows 10 1903 runner. Phase 09 (Packaging)
must test install on the minimum OS version.

**Owning ADR/Phase:** Decision log; Phase 09 (Packaging & Updates).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### CON-3 — Linux MVP Scope

**Question:** Is Linux a required platform for the MVP, V1, or V2 release, or
is it a bonus?

**Decision:** **Linux is a bonus, not an MVP/V1/V2 release gate.** Linux
support is:
- Not a blocker for MVP, V1, or V2 releases.
- Not a requirement in any phase's Definition of Done.
- A best-effort target: Avalonia supports Linux natively, and CI may include
  a Linux runner, but any Linux-specific failure does not block a release.
- Planned as a community-contribution pathway: the open-source readiness work
  in Phase 23 includes a documented path for Linux maintainers.

This confirms CON-6 (original SRS context gap label for Linux scope) as
formally resolved.

**Rationale:** The product promise targets Windows and macOS as first-class
desktop platforms. Linux support requires additional testing on multiple
distributions and desktop environments; the team is too small to gate releases
on it. Avalonia's Linux story is strong enough that community contributions are
feasible once the core is stable.

**Impact:** Every phase README specifies "Windows 10+ and macOS 13+" as the
platform gate. Linux CI is optional and informational. Phase 23 includes a
"Linux community track" section in the Extension SDK documentation.

**Owning ADR/Phase:** Decision log; SOURCE-SUMMARY §A.

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### CON-4 — Command-Palette Command Set

**Question:** What is the first-pass list of command-palette commands to be
available at MVP? Phase 03 will implement them; Phase 00 needs the list so
Phase 03 scope is bounded.

**Decision:** The following **~30 command-palette commands** constitute the MVP
first-pass list. Each command is identified by a stable key, a display label
(en), and the bounded context it invokes. Phase 03 implements the command
palette UI; this list is the backlog input.

| # | Key | Display label (en) | Context |
|---|---|---|---|
| 1 | `library.open` | Open library folder… | Library Catalogue |
| 2 | `library.scan` | Scan library for new files | Library Catalogue |
| 3 | `library.rescan` | Re-scan entire library | Library Catalogue |
| 4 | `library.settings` | Library settings | Settings & Security |
| 5 | `view.grid` | Switch to grid view | Bookshelf Presentation |
| 6 | `view.list` | Switch to list view | Bookshelf Presentation |
| 7 | `view.directory` | Switch to directory view | Bookshelf Presentation |
| 8 | `view.3d` | Switch to 3D shelf view | Bookshelf Presentation |
| 9 | `search.focus` | Open search | Search Index |
| 10 | `search.advanced` | Advanced search… | Search Index |
| 11 | `shelf.create` | Create new shelf… | Library Catalogue |
| 12 | `shelf.manage` | Manage shelves… | Library Catalogue |
| 13 | `shelf.goto` | Go to shelf… | Library Catalogue |
| 14 | `book.open` | Open selected book | Reader |
| 15 | `book.detail` | Show book detail | Library Catalogue |
| 16 | `book.editMetadata` | Edit metadata… | Metadata Enrichment |
| 17 | `book.addToShelf` | Add to shelf… | Library Catalogue |
| 18 | `book.markRead` | Mark as read | Library Catalogue |
| 19 | `book.markReading` | Mark as currently reading | Library Catalogue |
| 20 | `reader.toggleFullscreen` | Toggle full-screen reader | Reader |
| 21 | `reader.bookmark` | Add bookmark | Reader |
| 22 | `reader.nextPage` | Next page | Reader |
| 23 | `reader.prevPage` | Previous page | Reader |
| 24 | `ai.toggle` | Toggle AI advisor on/off | AI Advisor |
| 25 | `ai.recommend` | Ask AI for recommendations | AI Advisor |
| 26 | `ai.privacy` | Open AI privacy centre | Settings & Security |
| 27 | `settings.open` | Open settings | Settings & Security |
| 28 | `settings.privacy` | Privacy settings | Settings & Security |
| 29 | `app.checkForUpdates` | Check for updates | Packaging & Updates |
| 30 | `app.about` | About Ogma Library | App (composition root) |

**Rationale:** The list covers the five personas' primary workflows: scanning
and browsing (serious reader, power librarian), reading (serious reader,
researcher), metadata (collector), AI (researcher, privacy-sensitive user), and
settings. It is a first-pass; Phase 03 may add, remove, or rename based on
design review. The stable keys are used by keyboard shortcut bindings and by
the keyboard-shortcut customisation system.

**Impact:** Phase 03 implements the command palette with this backlog as its
scope contract. Any command not in this list that is proposed in Phase 03
requires a scope note in the Phase 03 PR.

**Owning ADR/Phase:** Decision log; Phase 03 (command palette backlog).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session. **Owner ask: confirm or amend the command
list before Phase 03 begins.**

---

### CON-5 — Work/Edition Cardinality and Merge/Split Rules

**Question:** Does the data model support a "Work" (abstract bibliographic
identity) that has one or more "Editions" (physical manifestations), each
Edition mapping to one or more `BookFiles`? What are the merge and split rules?

**Decision:**

**Cardinality:** A Work has 1..n Editions. An Edition maps to 1..n BookFiles
(e.g. the same edition as a PDF and as a scanned backup). This is an
**optional layer** on top of the existing `Books` table — MVP does not require
the UI to populate or display Works/Editions; the table structure exists from
Phase 04 but is nullable.

**Data model:**
- `Works` table: `WorkId`, `CanonicalTitle`, `CanonicalAuthorIds[]`,
  `CreatedUtc`, `UpdatedUtc`.
- `Editions` table: `EditionId`, `WorkId` (FK), `EditionLabel`,
  `PublicationYear`, `Publisher`, `Isbn13`, `Language`, `CreatedUtc`.
- `Books.EditionId` is a nullable FK to `Editions`. A `Book` record with a
  null `EditionId` is a standalone entry not yet linked to the Work/Edition
  hierarchy.

**Merge rule:** Two `Book` records that represent the same Edition of the same
Work are merged by: (a) creating or identifying the target `Work` and
`Edition`, (b) updating both `Books.EditionId` to point to the same
`EditionId`, (c) retaining all annotations, reading progress, and shelves from
both records, (d) marking the less-canonical `Book` as the secondary. No data
is deleted; merge is reversible.

**Split rule:** Detaching an Edition from a Work sets `Books.EditionId` to a
new `EditionId` with a new `WorkId`, or nulls it out (standalone). All
annotations and reading progress follow the `Book` record; nothing is lost.

**MVP exposure:** Not surfaced in any MVP UI. The schema is present, nullable,
and tested at the data layer in Phase 04. UI for merge/split is Phase 06/07
scope.

**Rationale:** Representing Works and Editions is standard in library science
(FRBR-inspired) and is needed for the Collector and Power Librarian personas to
deduplicate their collections. Making it optional and nullable means MVP is not
blocked on the UX complexity.

**Impact:** Phase 04 schema must include `Works`, `Editions`, and
`Books.EditionId` (nullable). Phase 06/07 implements merge/split UI. Metadata
enrichment (Phase 06) uses Edition-level ISBN as the primary lookup key.

**Owning ADR/Phase:** Decision log; Phase 04 (schema).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session. **Owner ask: confirm the merge/split rules
and the nullable MVP stance before Phase 04 schema is locked.**

---

### CON-6 — Sidecar Naming Convention per Asset Class

**Question:** What is the canonical naming and folder structure convention for
the sidecar asset folder, per class of asset (thumbnails, covers, spines, OCR,
embeddings, extracted text, backups)?

**Decision:** The sidecar folder uses a **SHA-256-sharded** naming convention.
The sidecar root is `{LibraryRoot}/.ogma/` (hidden folder). Within it, each
asset class occupies a subdirectory, and each file is named using the first two
hex characters of the book's content-hash SHA-256 as a sharding prefix to
avoid filesystem namespace exhaustion on large corpora:

```
.ogma/
  thumbnails/<sha256[0:2]>/<sha256>.webp         # 400×600 cover thumbnail
  covers/<sha256[0:2]>/<sha256>.jpg              # full-resolution cover image
  spines/<sha256[0:2]>/<sha256>.webp             # 32×160 spine image
  text/<sha256[0:2]>/<sha256>.txt.zst            # extracted text (zstd-compressed)
  ocr/<sha256[0:2]>/<sha256>.ocr.json.zst        # OCR result (zstd JSON)
  embeddings/<sha256[0:2]>/<sha256>.emb.bin      # embedding vector blob
  backups/<sha256[0:2]>/<sha256>.<iso8601>.bak   # PDF write-back backup
  meta/catalogue.db                             # SQLite catalogue (not sharded)
  meta/catalogue.db-wal                         # SQLite WAL file
  meta/catalogue.db-shm                         # SQLite SHM file
```

The `ISidecarService` (Phase 04) resolves these paths deterministically from a
`Sha256Hash` value object and an `AssetClass` enum; no caller constructs a path
by string concatenation.

**Rationale:** SHA-256 sharding at the first two hex characters distributes
assets across up to 256 subdirectories, keeping directory entry counts below
file system limits (e.g. ext4 256k, NTFS ~4M but performance degrades at 100k)
for corpora up to ~25,000 books per asset class before the second shard level
is needed. This matches the Phase 04 decision (referenced in OQ-04 above) and
is consistent with the reference adopted in large-scale content-addressable
stores.

**Impact:** Phase 04 implements `ISidecarService` using this convention.
All subsequent phases that write sidecar assets (Phase 05 thumbnails, Phase 10
text, Phase 11 embeddings, Phase 15 OCR, Phase 07 backups) must call
`ISidecarService`, not construct paths directly.

**Owning ADR/Phase:** ADR-0005 (Accepted; naming detail recorded here); Phase
04 (implementation).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### CON-7 — Target Jurisdictions and Data Protection Acts

**Question:** Which jurisdictions does Ogma Library target, and which Data
Protection Acts must it comply with?

**Decision (overrides recommended default; governed by DECISIONS.md D-003):**
**Global / multi-region.** Ogma Library is designed to comply with the
**strictest common denominator** of:

1. **Uganda Data Protection and Privacy Act 2019 (DPPA)** — primary
   jurisdiction (Chwezi Core Systems is Uganda-based).
2. **EU General Data Protection Regulation (GDPR) + UK Data Protection Act
   2018** — applies to any EU/UK data subject using the product.
3. **US COPPA** (Children's Online Privacy Protection Act, 1998 / amended
   2013) — applies to the school track (Phases 16-18) where users may be
   under 13.
4. **US FERPA** (Family Educational Rights and Privacy Act, 1974) —
   applies to the school track where student educational records are processed.

**Compliance design posture:**
- **Default posture is Tier-0 Offline / metadata-only** (ADR-0007). This
  keeps most users outside any controller/processor relationship entirely.
  The compliance surface engages only at opt-in off-device transmission.
- **Design to strictest common denominator** means: GDPR's explicit consent
  requirements apply to all regions; Uganda DPPA's data-subject rights (access,
  rectification, erasure, portability) are met; COPPA's verifiable parental
  consent requirement is met for any feature available to under-13 users.
- **DPIA required before each off-device feature ships** (CTRL-OGMA-024).
  Phase 19 is the DPIA phase; each off-device feature (AI metadata cloud,
  AI content-aware cloud, cloud sync, LAN classroom) has its own DPIA entry
  covering: data categories, lawful basis per region, provider processing
  location, cross-border transfer mechanism, retention period, erasure path,
  and residual risk. School-track features (Phases 16-18) carry additional
  COPPA/FERPA and GDPR-K (children's consent) analysis.
- **No personal data leaves the device without an explicit, previewed,
  per-transmission user action** (CTRL-OGMA-017). The gateway enforces this
  regardless of jurisdiction.

**Impact:** Phase 00 closes the jurisdiction context gap. Phase 18 (School AI)
and Phase 19 (DPIA) use this answer as their compliance baseline. The
`security:uganda-dppa-compliance` and `security:dpia-generator` skills are
used in Phase 19. Privacy centre UI (Phase 14) must present rights under all
three regimes. Phase 03 includes a Privacy Centre command in the command
palette (`ai.privacy`, `settings.privacy`).

**Owning ADR/Phase:** DECISIONS.md D-003; ADR-0007 (Accepted); Phase 19 (DPIA).

**Sign-off:** Owner: Peter Bamuhigire — **owner decision D-003 applied
(explicit owner sign-off on file in DECISIONS.md 2026-05-30)**.

---

### CON-8 — Provider Trust Weights and Field-Match Scoring

**Question:** What trust weights and field-match scoring formula does the
metadata confidence model use for Google Books and Open Library?

**Decision:** The following formula and weights are the **Phase 07 baseline**
(to be validated against real lookup results in Phase 07 spike):

**Provider trust weights:**
- Google Books: **0.85**
- Open Library: **0.80**
- Manual user edit: **1.00** (always wins, no formula applied)
- Existing PDF metadata (XMP/DocInfo): **0.60**

**Field confidence formula:**
```
FieldConfidence = ProviderWeight × MatchScore × RecencyBonus
```

Where:
- `MatchScore` ∈ [0.0, 1.0]: a per-field similarity score (exact ISBN match =
  1.0; fuzzy-title match scaled by Jaro-Winkler; missing field = 0.0).
- `RecencyBonus` ∈ [1.0, 1.1]: 1.0 for a lookup older than 30 days; 1.05 for
  a lookup within the last 30 days; 1.1 for a lookup within 7 days. Capped at
  1.0 after multiplication so the result never exceeds `ProviderWeight`.
- Final `FieldConfidence` ∈ [0.0, 1.0] (clamped).

**Merge rule:** When multiple providers return a field, the value with the
highest `FieldConfidence` wins; ties resolved by `RecencyBonus`, then
`ProviderWeight` order.

**Rationale:** Google Books has broader coverage and more complete metadata
for commercial titles; the weight advantage reflects this. Open Library is
stronger for public-domain and academic titles; the weight reflects solid but
narrower coverage. The formula is simple enough to reason about and audit.

**Impact:** Phase 07 (Metadata Enrichment) implements `IMetadataConfidenceModel`
using this formula. Phase 00 records the initial weights as defaults; Phase 07
may amend them based on lookup-quality spike results. Provider weights are Owner
asks (see Consolidated Sign-off table).

**Owning ADR/Phase:** Decision log; Phase 07 (Metadata Enrichment).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session. **Owner ask: confirm provider trust weights
(0.85 / 0.80) before Phase 07 implementation begins.**

---

### CON-9 — Corpus Licensing Provenance

**Question:** Are the PDF files in the golden-corpus test harness cleared for
inclusion in the test suite (public domain, CC-licensed, or owner-supplied
synthetic)?

**Decision:** The golden-corpus fixture manifest (specified in
`docs/plans/grand-plan/phase-00/testing.md`) mandates that **every fixture
must be in one of three categories**:

1. **Public domain** (e.g. Project Gutenberg, Internet Archive, US government
   publications): license confirmed, permalink documented, SHA-256 hash pinned.
2. **Creative Commons (CC-BY, CC-BY-SA, CC0)**: license confirmed, source URL
   documented, attribution included in `MANIFEST.sha256`.
3. **Owner-supplied synthetic**: generated or authored by Peter Bamuhigire /
   Chwezi Core Systems, no third-party rights. Synthetic fixtures are
   reproducibly generated by a seeded tool.

Any fixture without a confirmed license in one of these three categories **must
be replaced before Phase 02 begins**. The license review is a Phase 00 DoD
gate (P00-TEST-05 variant for the corpus).

**Current status:** No fixture files are committed to the repository yet. The
corpus manifest in `testing.md` lists the required fixture types. License
sourcing is a Phase 01/02 preparatory task gated on this decision.

**Rationale:** Including unlicensed or ambiguous-license PDFs in a public
open-source test suite creates IP liability for contributors and downstream
users. Public-domain and synthetic-only corpora eliminate this risk entirely.

**Impact:** Phase 02 creates the `tests/golden-corpus/fixtures/` directory and
a `MANIFEST.sha256` file with confirmed sources. Any fixture that cannot be
cleared is replaced by a synthetic alternative before Phase 02 closes.

**Owning ADR/Phase:** Decision log; Phase 02 (test harness setup).

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session. **Owner ask: confirm which specific public-
domain or CC documents to use (or approve synthetic-only corpus) before Phase
02 fixture sourcing begins.**

---

## Supplementary Cross-Cutting Decisions

The following decisions are confirmed or incorporated from DECISIONS.md and the
owner's expansion deltas (SOURCE-SUMMARY §L). They are recorded here for
traceability and form part of the Phase 00 decision baseline.

---

### SUP-1 — Open-Source License: MIT

**Decision:** The repository is licensed under the **MIT License**
(SPDX: `MIT`). The `LICENSE` file at the repo root is the MIT full text with
the copyright notice `Copyright (c) 2026 Chwezi Core Systems / Peter
Bamuhigire`. This was applied by the owner (first commit, 2026-05-30, per
git log).

**Rationale (per Phase 00 README §6):** MIT is the simpler permissive licence.
The recommended default was Apache 2.0 (patent grant). The owner applied MIT
at first commit; this document records and ratifies that choice. If a patent-
grant requirement arises (e.g. novel AI/search algorithms), an ADR amendment
can record a re-evaluation; the codebase can be re-licensed under Apache 2.0
by owner decision at any time.

**Impact:** All source files must carry the MIT SPDX header. `CONTRIBUTING.md`
references the MIT license. `THIRD-PARTY-NOTICES` lists all dependencies and
their licenses. Phase 02 establishes the header-check CI rule.

**Sign-off:** Owner decision (license applied at first commit 2026-05-30).

---

### SUP-2 — CLA Mechanism: DCO (Developer Certificate of Origin)

**Decision:** Contributor sign-off uses the **Developer Certificate of Origin
(DCO)**. Every commit from a contributor must include a `Signed-off-by: Name
<email>` trailer line, certifying the DCO 1.1 statement. The DCO check is
enforced by a GitHub Actions workflow (`.github/workflows/dco-check.yml`).
`CONTRIBUTING.md` explains the requirement and provides the `git commit -s`
shortcut.

**Rationale:** DCO is simpler than CLA Assistant for an early-stage open-source
project: no additional GitHub App installation required, no CLA database to
maintain, standard tooling support. This follows the Phase 00 README
recommendation.

**Impact:** `CONTRIBUTING.md` and the DCO check workflow must exist before the
first external-contributor PR is accepted. Phase 02 verifies the workflow runs.

**Sign-off:** Owner: Peter Bamuhigire — recommended default applied, pending
explicit sign-off in WP6 session.

---

### SUP-3 — i18n Language Scope

**Decision:** MVP ships with **English (en) and French (fr)** as the two
mandatory, fully translated locales. The final release target (post-V1) adds
**Spanish (es), Italian (it), and German (de)**. All user-facing strings must
be externalized from Phase 03 onward; no hardcoded string in a UI layer is
permitted in any PR (enforced by the CIA checklist item "are all new strings
externalized in en + fr"). This is confirmed from SOURCE-SUMMARY §L item 4 and
DECISIONS.md.

**Impact:** Phase 03 establishes the i18n infrastructure (`IStringLocalizer`,
`.resx` or Fluent resource files). Every phase from 03 onward must ship its
strings in both `en` and `fr`.

**Sign-off:** Owner decision (SOURCE-SUMMARY §L, 2026-05-30).

---

### SUP-4 — Icon System: Flat Full-Color SVG from Flaticon

**Decision:** Icons use the **flat full-color** style (DECISIONS.md D-001) with
**SVG as the master format** (DECISIONS.md D-004) sourced from **Flaticon**
(DECISIONS.md D-005). PNG exports at @1x/2x/3x (16/24/32/48 px) are the
wired runtime assets. The Avalonia SVG control handles SVG masters; rasterized
PNG is used where colorful assets are simpler. The `ICON-SYSTEM.md` and
`_icons/MASTER-MANIFEST.md` govern the full icon catalog. Flaticon Premium
(no-attribution required) is the recommended license path for store distribution.

**Impact:** Phase 03 procures and integrates the first icon set. Every
subsequent phase with a UI surface must include an `icons.md` stub listing
icons to procure and their Flaticon source IDs.

**Sign-off:** Owner decisions D-001, D-004, D-005 (all 2026-05-30, in
DECISIONS.md).

---

### SUP-5 — LAN / Classroom Scope: Post-MVP

**Decision:** The LAN / classroom e-library (Library Host, Student Client,
School Admin & Managed AI) ships **post-MVP, in V1→V2 as planned**, in Phases
16-18 (DECISIONS.md D-002). MVP is a polished, cross-platform, single-user
desktop product. LAN groundwork (clean read-model projections, contract
boundaries, AI gateway as a reusable chokepoint) is laid in core phases so
Phases 16-18 are an extension, not a rewrite.

**Impact:** No phase before Phase 16 implements any LAN listener, host, or
student-client code. Phase 04 defines `ICatalogueReadModel` for LAN-ready
consumption. ADR-0010 (opt-in Library Host mode) is drafted as Proposed in
Phase 00 and will be ratified in Phase 16.

**Sign-off:** Owner decision D-002 (2026-05-30, in DECISIONS.md).

---

### SUP-6 — Avalonia License: Community

**Decision:** Avalonia is used under the **community license** registered by
the owner (`peter@techguypeter.com`, DECISIONS.md D-006). Avalonia core is
MIT. Phase 02 records the exact license terms in `THIRD-PARTY-NOTICES`.

**Sign-off:** Owner decision D-006 (2026-05-30, in DECISIONS.md).

---

## Consolidated Owner Sign-off Table

| ID | Decision summary | Status | Sign-off date |
|---|---|---|---|
| OQ-01 | .NET 10 LTS (ADR-0001) | Applied default | |
| OQ-02 | PDFium behind adapter, wrapper pending Phase 01 spike (ADR-0004) | Applied default | |
| OQ-03 | DB-first annotations, PDF write-back deferred & schema-ready (ADR-0008) | Applied default | |
| OQ-04 | Sidecar folder on disk, SHA-256-sharded (ADR-0005) | Applied default | |
| OQ-05 | Metadata search MVP; FTS5 deferred to V1 (ADR-0006 note) | Applied default | |
| OQ-06 | OCR deferred to V1 (Phase 15) | Applied default | |
| OQ-07 | EPUB/CBZ post-V1; schema-extensible `FormatType` column | Applied default | |
| OQ-08 | Cloud sync excluded MVP/V1; schema-ready; DPIA-gated | Applied default | |
| CON-1 | W-REF-01 (Core i5-10210U, 8 GB, SATA SSD, Win 10 22H2) + M-REF-01 (M1, 8 GB, macOS 13.6) | **Needs owner confirm** | |
| CON-2 | Install minimums: 4 GB RAM min / 8 GB rec; Win 10 1903+ / macOS 13+ | Applied default | |
| CON-3 | Linux = bonus, not MVP/V1/V2 gate | Applied default | |
| CON-4 | ~30-command palette first-pass list | **Needs owner confirm** | |
| CON-5 | Work 1..n Editions; nullable MVP layer; merge/split rules | **Needs owner confirm** | |
| CON-6 | SHA-256-sharded sidecar naming convention | Applied default | |
| CON-7 | Global/multi-region: Uganda DPPA + EU/UK GDPR + US COPPA/FERPA | Owner decision D-003 applied | 2026-05-30 |
| CON-8 | Google Books 0.85 / Open Library 0.80; FieldConfidence formula | **Needs owner confirm** | |
| CON-9 | Public-domain / CC / owner-synthetic corpus only; unlicensed files replaced before Phase 02 | **Needs owner confirm** | |
| SUP-1 | License = MIT (owner-applied at first commit) | Owner decision applied | 2026-05-30 |
| SUP-2 | CLA = DCO | Applied default | |
| SUP-3 | i18n = en/fr MVP → es/it/de post-V1 | Owner decision applied | 2026-05-30 |
| SUP-4 | Icons = flat full-color SVG, Flaticon vendor | Owner decisions D-001/D-004/D-005 applied | 2026-05-30 |
| SUP-5 | LAN = post-MVP (Phases 16-18) | Owner decision D-002 applied | 2026-05-30 |
| SUP-6 | Avalonia community license | Owner decision D-006 applied | 2026-05-30 |

> **Items marked "Needs owner confirm"** are CON-1, CON-4, CON-5, CON-8, and
> CON-9. Recommended defaults have been applied and recorded above. Explicit
> owner sign-off on these five items is the primary agenda for the WP6 sign-off
> session.

---

*End of Phase 00 Decision Closure Log — v1.0 — 2026-05-30*
