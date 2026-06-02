# Source Summary — Ogma Library single‑source‑of‑truth digest

> Distilled from the signed‑baseline reference set in `docs/references/`:
> PRD, SRS, HLD, ADRs, Test Strategy, Development Standards, Deployment & Ops,
> DPIA, Risk Register, Stakeholder Analysis, Agile Artifacts.
> **This digest is canonical for the grand plan.** If it conflicts with a phase
> folder, this digest and the underlying reference docs win.

---

## A. Identity & platforms

- **Product:** Ogma Library — local‑first premium PDF library OS.
- **Owner:** Chwezi Core Systems / Peter Bamuhigire. Confidential — Internal Use.
- **Methodology:** Hybrid (Water‑Scrum‑Fall). Signed Waterfall SRS baseline →
  Agile build. Hybrid validation gate `python -m engine validate Ogma-Library`
  blocks Phase‑07‑class outputs until it passes.
- **Runtime:** **.NET 10 LTS** (supported to 2028‑11‑14). .NET 8 only as a
  documented, ADR‑recorded temporary bridge.
- **Shell:** **Avalonia** (C# / XAML), one codebase for **Windows + macOS**
  (Linux is a bonus, not an MVP gate — CON‑6 context gap to confirm in Phase 00).
- **Cross‑platform parity is mandatory:** the **MVP must run on Windows
  (WebView2) and macOS (WKWebView)**. Every phase must keep both platforms green.

## B. The product promise & principles

- Promise: *"Ogma Library gives readers private, durable, beautiful command over
  their PDF collections."* The nouns are acceptance criteria.
- Seven product principles (PRD §3): local‑first by default; AI is explainable;
  files remain the user's files; metadata is reversible; 3D is functional not
  gimmick; offline remains useful; premium means calm control.

## C. Personas (5)

1. **Serious reader** — re‑discover & resume; signal: find/resume without file
   names. Features: 3D shelf, grid, progress, shelves, stats.
2. **Researcher** — intent→source fast. Features: full‑text + semantic search,
   AI advisor, annotations, citation export.
3. **Collector** — correct & preserve metadata safely. Features: ISBN detection,
   Google Books/Open Library, metadata editor, backups.
4. **Power librarian** — maintain at scale. Features: health dashboard,
   duplicate detection, batch jobs.
5. **Privacy‑sensitive user** — intelligence without exposure. Features: privacy
   tiers, payload preview, local embeddings, explicit upload controls.

> Owner's expanded vision adds **classroom / school** actors: a **library host**
> (central computer), **students** (LAN clients), and a **school administrator**
> (manages access + AI keys). These are first‑class in Phases 16–18.

## D. Functional requirement groups & IDs

Stable ID prefixes (from `_context/features.md`). Tier in brackets.

### LIB — Library setup & scanning (BG‑1, BG‑4)
- FR‑LIB‑001 [MVP] select/store/rescan library root.
- FR‑LIB‑002 [MVP] recursive PDF discovery; honor excluded folders.
- FR‑LIB‑003 [MVP] stable identity = relative path + content hash; re‑match
  renamed/moved.
- FR‑LIB‑004 [MVP] flag removed/unavailable without deleting user data.
- FR‑LIB‑005 [MVP] background thumbnail/spine jobs; failures visible+retryable.
- FR‑LIB‑006 [MVP] incremental rescan (mtime/size/hash cache).
- FR‑LIB‑007 [V1] scan health report (failed, password, unsupported, missing
  thumbnails, metadata gaps).

### CAT — Catalogue browsing (BG‑1, BG‑2)
- FR‑CAT‑001 [MVP] 3D shelf / grid / list / directory; all open the same
  book‑detail + reader.
- FR‑CAT‑002 [MVP] sort & filter (title, author, year, status, rating, tag,
  shelf, availability); conjunctive; single clear.
- FR‑CAT‑003 [MVP] virtual shelves; a book in multiple shelves independent of
  path.
- FR‑CAT‑004 [MVP] full metadata across file/biblio/reading/enrichment/AI
  groups.
- FR‑CAT‑005 [V1] previewed, undoable bulk edit of tags/shelves/status/
  confidence.

### META — Metadata enrichment (BG‑1, BG‑4)
- FR‑META‑001 [MVP] detect ISBN‑10/13 from filename, XMP, DocInfo, first pages;
  normalize, validate check digit, rank.
- FR‑META‑002 [MVP] lookup in Google Books + Open Library; store source,
  timestamp, confidence.
- FR‑META‑003 [MVP] reviewable confidence merge; accept/reject/manual edit.
- FR‑META‑004 [V1] field‑level provenance (title, author, cover, ISBN,
  publisher, description, categories).
- FR‑META‑005 [V1] PDF write‑back only after confirmation; backup + field diff;
  original intact on failure.
- FR‑META‑006 [V1] batch enrichment under rate limits + retries; paused/failed
  visible.
- FR‑META‑007 [V1] metadata quality score; filter books missing fields.

### READ — PDF reader (BG‑2, BG‑4)
- FR‑READ‑001 [MVP] open & resume last page + scroll offset.
- FR‑READ‑002 [MVP] first/prev/next/last/jump/history (keyboard + UI).
- FR‑READ‑003 [MVP] fit‑width/fit‑page/fixed‑% zoom; persist per book.
- FR‑READ‑004 [MVP] single/two‑page/continuous; persist per book.
- FR‑READ‑005 [MVP] full‑screen; Escape exits.
- FR‑READ‑006 [MVP] text‑search highlight + page list (text layer).
- FR‑READ‑007 [MVP] bookmarks with labels + page jump, durable.
- FR‑READ‑008 [MVP] highlights & notes persist in catalogue; reload accurately.
- FR‑READ‑009 [V1] password‑protected PDF via OS credential flow.
- FR‑READ‑010 [V1] OCR‑index scanned PDF (optional bg job); mark OCR‑derived.
- FR‑READ‑011 [V1] citation capture card (title, author, page, selection).
- FR‑READ‑012 [V2] split view.

### SEARCH — Search & indexing (BG‑2, BG‑3)
- FR‑SEARCH‑001 [MVP] metadata search while typing within NFR‑OGMA‑003 budget on
  2,000 books.
- FR‑SEARCH‑002 [V1] FTS5 index over extracted text, notes, tags, descriptions,
  TOC.
- FR‑SEARCH‑003 [V1] match location (title/author/note/text‑page/semantic).
- FR‑SEARCH‑004 [V1] semantic search over embeddings (NL query).
- FR‑SEARCH‑005 [V1] hybrid ranking (exact, recency, status, rating, semantic).
- FR‑SEARCH‑006 [V1] index manager (progress, indexed count, pending OCR, failed
  extraction, index size).

### AI — AI advisor (BG‑3, BG‑5)
- FR‑AI‑001 [MVP] fully disableable; no key required; never blocks core.
- FR‑AI‑002 [MVP] provider choice (OpenAI‑/Anthropic‑/DeepSeek‑compatible,
  Ollama) behind one `IAiProvider`.
- FR‑AI‑003 [MVP] ranked recommendations from own collection, each with
  explanation + confidence. *(VERIFIABILITY‑FAIL: relevance is judgement; oracle
  = structural completeness.)*
- FR‑AI‑004 [MVP] default metadata‑only; cloud sends only title/author/tags/
  categories/descriptions/notes.
- FR‑AI‑005 [V1] content‑aware only on explicit per‑library/per‑query opt‑in;
  privacy label before any chunk.
- FR‑AI‑006 [V1] local embeddings via Ollama (no cloud upload).
- FR‑AI‑007 [V1] reading plan (sequence, rationale, difficulty, checkpoints).
  *(VERIFIABILITY‑FAIL: pedagogy is judgement; oracle = structural.)*
- FR‑AI‑008 [V2] answer mode citing local evidence (book, page/chunk,
  confidence).
- FR‑AI‑009 [V1] local query history; delete + disable retention.
- FR‑AI‑010 [V1] per‑cloud‑call model usage + estimated cost.

### ADMIN — School administration & managed AI (V2 classroom)
- FR‑ADMIN‑001 [V2] admin can publish/unpublish folders to the shared Host
  library.
- FR‑ADMIN‑002 [V2] admin can create/edit/delete shared shelves and assign
  books.
- FR‑ADMIN‑003 [V2] admin can enroll, edit, and revoke student/teacher
  profiles.
- FR‑ADMIN‑004 [V2] school AI API key stored in the Host OS credential store;
  never transmitted to clients.
- FR‑ADMIN‑005 [V2] all student AI queries route through the Host AI gateway.
- FR‑ADMIN‑006 [V2] class default privacy tier is metadata‑only; content‑aware
  requires admin opt‑in.
- FR‑ADMIN‑007 [V2] student sees privacy tier label and payload preview before
  any off‑device AI call.
- FR‑ADMIN‑008 [V2] per‑student and per‑class AI quotas enforced.
- FR‑ADMIN‑009 [V2] per‑student rate limits enforced.
- FR‑ADMIN‑010 [V2] admin dashboard shows AI usage, cost, and quota
  utilization.
- FR‑ADMIN‑011 [V2] classroom answer mode cites only Host catalogue evidence.
- FR‑ADMIN‑012 [V2] student can delete own AI query history.
- FR‑ADMIN‑013 [V2] admin can purge all student AI history for the institution.

## E. Non‑functional budgets

**NFR‑OGMA (product‑specific, reference hardware):**
- 001 cold start ≤ 3 s P95 · 002 catalogue load ≤ 2 s P95 (2,000 books) ·
  003 metadata search ≤ 150 ms P95 · 004 full‑text ≤ 500 ms P95 warm ·
  005 page turn ≤ 100 ms P95 cached · 006 3D ≥ 60 FPS (500 books) ·
  007 AI metadata‑only ≤ 10 s P95 *(SMART‑FAIL: excludes provider latency)* ·
  008 annotation durable across abnormal termination ·
  009 background job recoverable without duplicate work.

**NFR‑PROD (productivity‑domain defaults, injected):** 001 local‑first core ·
002 cold start ≤ 3 s P95 (5,000 items) · 003 first screen ≤ 1 s, page ≤ 200 ms
(50,000) · 004 full‑text ≤ 500 ms / semantic ≤ 1.5 s (50,000) · 005 no UI stall
> 100 ms · 006 crash‑free ≥ 99.5% · 007 keyboard ops · 008 screen‑reader + AA
contrast · 009 portability / no lock‑in · 010 reversible transactional
destructive ops · 011 privacy‑tier + payload preview · 012 signed builds +
reversible migrations · 013 local audit trail · 014 AI history + embedding
erasure.

## F. Architecture (HLD)

- **Style:** local‑first modular monolith. **Catalogue is the single source of
  truth for book identity;** Reader, Search, AI, 3D read through contracts and
  never own identity.
- **9 projects:** `App` (composition root) · `Domain` (no outward deps) ·
  `Application` (use cases/interfaces) · `Infrastructure` (SQLite, FS, PDF
  adapters, HTTP, AI providers) · `Reader` · `Bookshelf3D` · `Workers` ·
  `Tests`. Dependencies point inward; only `App` binds implementations.
- **9 bounded contexts:** Library Catalogue · Ingestion Pipeline · Metadata
  Enrichment · Reader · Search Index · AI Advisor · Bookshelf Presentation ·
  Settings & Security · Packaging & Updates.
- **Data:** SQLite catalogue (EF Core) + sidecar asset folder (covers,
  thumbnails, spines, ocr, extracted‑text, embeddings, backups). Tables incl.
  Books, BookFiles, BookMetadataFields, Authors/BookAuthors, Shelves/ShelfBooks,
  ReadingProgress, Bookmarks, Annotations/AnnotationBodies, ExtractedPages,
  SearchChunks, EmbeddingVectors, AiQueryHistory, MetadataLookups, Jobs,
  AuditEvents.
- **Identity:** relative path → SHA‑256 content hash → size+mtime → PDF
  fingerprint → ISBN/DOI.
- **PDF pipeline tools:** PDFium (render) · PdfPig (extract text/metadata) ·
  Tesseract (OCR) · SkiaSharp (thumbnails/spines) · PDFsharp (write‑back, under
  backup→diff→verify→restore).
- **3D:** WebGL2 / Three.js in platform WebView; typed C#↔JS bridge; `ogma://`
  asset scheme; grid/list fallback when WebGL2 absent.
- **AI:** single `IAiProvider` gateway = the one egress chokepoint;
  `IAiAdvisorService` + `IAiPrivacyService`; four tiers (Offline default /
  metadata‑only / content‑aware opt‑in / local Ollama).
- **Security:** OS credential store (DPAPI/Keychain); untrusted‑PDF worker
  isolation; path validation vs library root; signed updates; optional at‑rest
  encryption; local tamper‑evident audit trail; no inbound listener (CI‑2 — to
  be reconciled for LAN host mode, see `LAN-CLASSROOM-ARCHITECTURE.md`).

## G. ADRs (all Proposed 2026‑05‑30, ratify in Phase 00)

- 0001 .NET 10 LTS · 0002 Avalonia shell · 0003 WebView Three.js 3D (spike‑gated)
  · 0004 PDFium behind adapter (2‑wrapper benchmark) · 0005 SQLite catalogue +
  sidecar · 0006 hybrid search (metadata + FTS5 + embeddings; brute‑force cosine
  first) · 0007 provider‑neutral AI gateway + 4 tiers · 0008 DB‑first
  annotations/metadata, PDF write‑back later · 0009 Velopack (direct) + MSIX
  (Store/enterprise) + notarized DMG.

## H. Open questions (PRD §10 — close in Phase 00, deadlines noted)

- OQ‑01 .NET 10 vs 8 (→ .NET 10; Phase 0) · OQ‑02 PDFium wrapper (Phase 0) ·
  OQ‑03 annotation write‑back strategy (before Phase 3) · OQ‑04 thumbnail/spine
  storage (sidecar; before Phase 1) · OQ‑05 FTS in MVP (defer to V1; before
  Phase 4) · OQ‑06 OCR in MVP (defer to V1; before Phase 4) · OQ‑07 EPUB/CBZ
  (post‑V1) · OQ‑08 cloud sync (exclude MVP/V1; schema‑ready; DPIA before any).

## I. Context gaps (SRS §8.5 — assign values in Phase 00)

Reference hardware (CPU/RAM/storage); install minimums; Linux MVP scope;
command‑palette command set; Work/Edition cardinality & merge/split; sidecar
naming convention per class; target‑user jurisdictions (Data Protection Acts);
provider trust weights + field‑match scoring for the confidence model;
licensing/provenance of redistributed corpus PDFs.

## J. Quality / V&V

- **Risk tiers:** R1 data loss · R2 privacy breach · R3 performance budget · R4
  recoverability · R5 functional. R1/R2 failures are unwaivable release blockers.
- **Golden corpus** (version‑pinned, hash‑oracle): simple text; scanned
  image‑only; password‑protected; very large (1,000+ pp); two‑column; bad
  metadata; embedded outline/TOC; rotated pages; non‑English; forms/unusual
  fonts. Plus synthetic 500‑/2,000‑book perf corpora by seed.
- **9 test layers:** Domain, Infrastructure, PDF, Search, AI, UI, 3D,
  Performance, Packaging. Pyramid: most unit, then integration, least E2E, plus
  targeted manual (a11y, 3D fidelity, install/update on real HW).
- **8 public‑beta gates (G1–G8):** WebView bridge · PDFium wrapper · 500‑book
  responsiveness · 2,000‑book responsiveness · write‑back backup/restore · AI
  payload preview · index rebuild · interrupted‑job recovery.

## K. Distribution & ops

- **Channels:** Dev → Alpha → Beta → Stable (Velopack feeds). Promote, don't
  rebuild.
- **Targets:** Velopack (direct, both OS, delta) · MSIX (Microsoft/Windows Store
  + enterprise) · notarized DMG (macOS direct). **Owner's added targets:** Mac
  App Store and Windows Store listings, plus public GitHub releases.
- **Update trust chain:** sign build + sign feed descriptor; verify descriptor
  *and* package independently of transport.
- **Telemetry:** opt‑in, minimized at source, device‑local default. SLOs:
  update‑success ≥ 99.0%, crash‑free ≥ 99.5%, release‑host ≥ 99.9%, median
  update download ≤ 8 s @25 Mbps.
- **Incident response:** detect→triage→contain→eradicate→recover→review;
  SEV‑1/2/3; signing‑key and malicious‑update runbooks.

## L. Owner's expansion deltas (beyond the signed baseline)

1. **LAN / classroom e‑library:** central host serves PDFs; students read &
   search on any LAN computer. Reconciles CI‑2 via an opt‑in host mode with its
   own security model (Phases 16–18; new ADRs).
2. **School‑managed AI:** schools supply AI API keys so students do smart,
   explainable searches; entitlements, quotas, moderation, audit (Phase 18).
3. **Colorful premium icons everywhere:** every button/menu carries a colorful
   premium PNG icon; owner procures icons on request (`ICON-SYSTEM.md`).
4. **Multilingual:** MVP **full English + French**; final adds **Spanish,
   Italian, German** (`I18N-STRATEGY.md`).
5. **Publish to GitHub + Mac App Store + Windows Store** (Phase 22).
6. **Cross‑platform MVP on Windows + macOS** (reaffirmed, gated each phase).
7. **Open‑source readiness from day one.** The codebase is authored for an
   eventual public open‑source release "in support of AI‑enabled learning."
   Every public type/member carries XML doc comments
   (`GenerateDocumentationFile=true`, enforced); architecture, ADRs, and a
   developer guide stay current (`docs-architect`, `doc-architect`,
   `tutorial-engineer`); a clean license, `CONTRIBUTING`, `CODE_OF_CONDUCT`,
   and CLA are in place before publication (Phases 02 + 23). No secrets, no
   proprietary lock‑in.
8. **Extensibility for generative‑AI uses of the curated book databases.** The
   product exposes stable extension points — plugin interfaces for metadata,
   export, OCR, and **AI providers**; a documented read API over the catalogue +
   search index; theme/icon packs; and importers (Zotero, Calibre, Goodreads) —
   so the community can invent new generative‑AI experiences on top of the
   libraries users curate, without forking. Designed across Phases 12–13 and
   delivered/hardened as a public **Extension SDK** in Phase 23. All extension
   surfaces still route off‑device traffic through the single AI gateway and the
   privacy‑tier model.
