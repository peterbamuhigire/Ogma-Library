# Ogma Library — Grand Master Development Plan

> A local-first, premium personal **and** classroom PDF library operating system.
> By **Chwezi Core Systems** · Product Owner **Peter Bamuhigire**.
>
> This is the single, authoritative 24‑phase plan to take Ogma Library from the
> signed requirements baseline all the way to a signed, notarized, multilingual,
> beautifully iconified product published on **GitHub**, the **Mac App Store**,
> and the **Microsoft (Windows) Store**.

---

## 0. How to read this plan

This directory (`docs/plans/grand-plan/`) is structured as follows:

| File / Folder | Purpose |
| --- | --- |
| `README.md` (this file) | The master index: vision, the 24 phases, principles, global gates, and the cross‑cutting threads (icons, i18n, accessibility, privacy, LAN). |
| `CONVENTIONS.md` | The mandatory template every phase folder follows, plus writing, traceability, and Definition‑of‑Done standards. |
| `SOURCE-SUMMARY.md` | A distilled, single‑source‑of‑truth digest of the PRD, SRS, HLD, ADRs, Test Strategy and Deployment/Ops docs (FR/NFR IDs, bounded contexts, budgets, open questions). Read this before any phase. |
| `SKILLS-INDEX.md` | The full map of which **skills** (from `~/.claude/skills`) and **slash commands** to invoke in which phase, and why. |
| `ICON-SYSTEM.md` | The colorful premium‑icon design system, the per‑phase **icon manifest** convention, and the procurement workflow (we *always ask the owner* for the premium PNG icons). |
| `I18N-STRATEGY.md` | The localization plan: MVP = full **English + French**; final = add **Spanish, Italian, German**. Pseudolocalization, RTL‑readiness, and translation governance. |
| `LAN-CLASSROOM-ARCHITECTURE.md` | The architectural reconciliation for the networked e‑library / classroom mode (host + client + school admin + managed AI). |
| `DECISIONS.md` | The owner decisions log (icon style, LAN scope, jurisdictions, …) — binds the plan and supersedes conflicting defaults. |
| `_reference/AVALONIA-STANDARDS.md` | Avalonia engineering standards mined from *Avalonia UI Succinctly*; paired with the `avalonia-desktop-development` skill. UI phases build to this. |
| `phase-00/ … phase-23/` | One folder per phase, each broken down in full detail per `CONVENTIONS.md`. |

> **Reading order for a contributor:** `SOURCE-SUMMARY.md` → this README → the
> `CONVENTIONS.md` → the specific `phase-NN/README.md` you are about to execute.

---

## 1. The product, in one paragraph

Ogma Library turns a folder of PDF files into a **visible, usable, intelligent,
private, durable, and beautiful** library. It is built as a **local‑first
modular monolith** on **.NET 10 LTS** with an **Avalonia** desktop shell, a
**SQLite catalogue of record** plus a sidecar asset folder, a **PDFium**-backed
reader, **FTS5 + embeddings** hybrid search, a **provider‑neutral AI gateway**
with four privacy tiers, and a signature **WebGL2 / Three.js 3D bookshelf**.
Beyond the single‑user desktop product, Ogma Library extends to a **LAN /
classroom e‑library**: a central computer holds the PDF folder, students open
Ogma on any computer on the network to read and search the collection, and a
school can supply its own AI API keys so students perform smart, explainable
searches of the books — all under the same privacy, reversibility, and
accessibility guarantees.

The four nouns of the product promise — **private, durable, beautiful,
command** — are acceptance criteria, not adjectives, and every phase below is
gated against measurable thresholds.

---

## 2. Standing principles (binding on every phase)

These extend the seven product principles in the PRD. A deliverable that
violates one is rejected at its phase gate.

1. **Local‑first by default.** The default install transmits **0 bytes**
   off‑device. Every off‑device call is an explicit, consented, reversible
   exception routed through the single AI/egress gateway.
2. **AI is explainable.** Every recommendation states *why* it matched, *what*
   supported the match, and its *confidence*.
3. **Files remain the user's files.** The catalogue augments the folder; it
   never locks books into a proprietary container.
4. **Everything destructive is reversible.** Backup → diff → verify → restore.
   Zero irreversible operations.
5. **3D is functional, not gimmick.** Grid and list stay first‑class; no
   capability is reachable *only* through the 3D shelf.
6. **Offline stays useful.** Loss of connectivity degrades optional cloud
   features only — never the core library.
7. **Premium means calm control.** Refined, not busy; a large collection feels
   *less* intimidating, not more.
8. **Beautiful and iconified.** Every button, menu item, and primary surface
   carries a **colorful premium icon**. UI altitude, motion, and color are
   designed deliberately (see `ICON-SYSTEM.md`). We **always ask the owner** to
   procure the premium PNG icons named in each phase's icon manifest.
9. **Multilingual by construction.** No hard‑coded user‑facing string. MVP ships
   **full English + French**; the final product adds **Spanish, Italian,
   German** (see `I18N-STRATEGY.md`).
10. **Accessible as a gate.** WCAG 2.2 AA — keyboard + screen‑reader operability
    of all core flows — is a release gate, and colorful icons never become the
    *only* carrier of meaning (always paired with text/`aria` labels).
11. **Everything is tested.** Each phase carries unit, integration,
    fault‑injection, performance, and (where UI) accessibility tests, asserted
    against the golden corpus. Code is documented with XML doc comments
    (`GenerateDocumentationFile=true`) and bounded‑context architecture tests.

---

## 3. The 24 phases at a glance

The plan is organized into seven parts. Phase numbers are permanent. Durations
are planning estimates in engineer‑weeks for a small team and assume the prior
phase's Definition of Done is met. Phases inside a part can overlap where their
dependency edges allow (see each phase's *Dependencies* section).

> **Mapping to the original 8 build phases (PRD §9):** the original Phase 0–7
> roadmap is *fully contained* here — Phase 00–01 (spikes/decisions), 04–05
> (catalogue foundation), 07 (metadata + health), 08–09 (reader), 10–11
> (search), 14 (3D shelf), 12–13 (AI), 21–23 (polish/launch). This grand plan
> expands that spine with the design system, the LAN/classroom product, the
> hardening tracks, store distribution, and post‑launch operations the original
> roadmap deferred to "post‑MVP iterations."

### Part I — Inception & Foundation
| # | Phase | Focus | Est. |
| --- | --- | --- | --- |
| **00** | **Decision Closure & Project Inception** | Close the 8 PRD open questions + 8 SRS context gaps; ratify ADR‑0001…0009; reference hardware; jurisdictions; repo, governance, licensing, CLA. | 2 wk |
| **01** | **Risk Spikes & Technical Proof** | .NET 10 dependency matrix, PDFium wrapper benchmark, WebView↔JS bridge, FTS5, AI gateway, 3D macOS WKWebView, LAN transport — each retired into an ADR amendment. | 2 wk |
| **02** | **Solution Scaffolding & Architecture Skeleton** | 9‑project solution, `Directory.Build.props`, DI composition root, domain model, architecture tests, CI baseline, golden‑corpus harness. | 3 wk |
| **03** | **Design System, Icon System & UX Foundation** | Calm‑control design language, color & motion tokens, Avalonia theming, **colorful icon system**, command palette, **i18n scaffold (en/fr)**, accessibility scaffold. | 3 wk |

### Part II — Core Library
| # | Phase | Focus | Est. |
| --- | --- | --- | --- |
| **04** | **Catalogue & Data Layer** | SQLite catalogue‑of‑record, EF Core, idempotent reversible migrations, sidecar layout, book‑identity model, export bundle. | 3 wk |
| **05** | **Ingestion Pipeline & Scanning** | Scan, content hashing, stable identity, incremental rescan, unavailable‑file flagging, background workers, thumbnails/spines. (FR‑LIB) | 3 wk |
| **06** | **Catalogue Browsing** | Grid / list / directory views, sort & filter, virtual + smart shelves, book detail, bulk edit preview/undo. (FR‑CAT) | 3 wk |
| **07** | **Metadata Enrichment & Collection Health** | ISBN detection, Google Books / Open Library lookup, confidence merge, reversible write‑back, provenance, library health dashboard. (FR‑META) | 3 wk |
| **08** | **PDF Reader Core** | Render, navigate, zoom, display modes, full‑screen, page‑render cache, resume position, in‑document text search. (FR‑READ 1–6) | 4 wk |
| **09** | **Annotations, Bookmarks & Reading Memory** | Durable highlights/notes, bookmarks, annotation layers, citation capture, reading memory. (FR‑READ 7–8, 11) | 3 wk |

### Part III — Intelligence
| # | Phase | Focus | Est. |
| --- | --- | --- | --- |
| **10** | **Search & Indexing** | Metadata search within budget, FTS5 external‑content index, extraction pipeline, index manager. (FR‑SEARCH 1,2,6) | 3 wk |
| **11** | **Semantic Search & Embeddings** | Embeddings (local Ollama path), cosine→ANN, hybrid ranking, match‑location explanation. (FR‑SEARCH 3,4,5) | 3 wk |
| **12** | **AI Gateway & Privacy Center** | Provider‑neutral `IAiProvider`, four privacy tiers, payload preview, audit, cost, query‑history erasure, Privacy Center. (FR‑AI 1,2,4,5,9,10) | 3 wk |
| **13** | **AI Reading Advisor & Plans** | Explainable ranked recommendations, reading plans, V2 local‑evidence answer mode. (FR‑AI 3,7,8) | 3 wk |

### Part IV — Signature Experience & Power Features
| # | Phase | Focus | Est. |
| --- | --- | --- | --- |
| **14** | **3D Bookshelf** | WebView‑hosted Three.js, typed C#↔JS bridge, `ogma://` asset scheme, spine textures, 60 FPS, accessible fallback. (FR‑CAT 3D) | 3 wk |
| **15** | **OCR, Advanced Reader & Power Tools** | OCR pipeline, password‑protected PDFs, split view, batch enrichment, smart shelves at scale. (FR‑READ 9,10,12; FR‑META‑006) | 3 wk |

### Part V — Networked E‑Library / Classroom
| # | Phase | Focus | Est. |
| --- | --- | --- | --- |
| **16** | **LAN Library Server (Host Mode)** | Opt‑in host that serves the catalogue + PDFs over the LAN; transport, discovery, auth, content streaming, new ADRs reconciling CI‑2. | 4 wk |
| **17** | **Client / Classroom Mode & Multi‑User** | Client connects to a host, per‑student profiles, roles, private reading state, offline cache, sync. | 4 wk |
| **18** | **School Administration & Managed AI** | Admin console, school‑provisioned AI keys, entitlements & quotas, curation, moderated student smart‑search, audit. | 4 wk |

### Part VI — Hardening & Quality
| # | Phase | Focus | Est. |
| --- | --- | --- | --- |
| **19** | **Security Hardening & Privacy / Compliance** | Threat model, untrusted‑PDF isolation, credential store, path validation, at‑rest encryption, DPIA per off‑device feature, audit trail, SAST. | 3 wk |
| **20** | **Performance Engineering & Reliability** | Reference‑hardware benchmarks for every NFR budget, fault injection, job recovery, index repair, observability. | 3 wk |
| **21** | **Accessibility, Full i18n & Comprehensive QA** | WCAG 2.2 AA audit, screen‑reader passes, **Spanish/Italian/German localization**, golden‑corpus E2E, full multi‑dimensional review. | 4 wk |

### Part VII — Distribution & Launch
| # | Phase | Focus | Est. |
| --- | --- | --- | --- |
| **22** | **Packaging, Signing & Store Submission** | Velopack feeds, MSIX + **Windows Store**, notarized DMG + **Mac App Store**, **GitHub** releases, signed reversible auto‑update, channels. | 3 wk |
| **23** | **Beta, Launch & Post‑Launch Operations** | Go‑live readiness gate, public beta, telemetry/SLOs, runbooks, incident response, support, extension points/plugins, roadmap to V1/V2. | 3 wk |

**Indicative total:** ~75 engineer‑weeks of build after the SRS baseline is
signed (the original PRD spine is ~23–24 weeks; the rest is the design system,
LAN/classroom product, hardening tracks, store distribution, full localization,
and operations the PRD scheduled as post‑MVP).

---

## 4. Release tiers across the plan

The PRD's **MVP / V1 / V2** tiering is preserved and threaded through the phases.

- **MVP** (gateable subset): Phases 00–10, 12 (Tier‑0/1 metadata‑only AI), 14,
  the **English + French** localization, the colorful icon system, and the core
  of 19–23. The MVP acceptance boundary (PRD §5) — scan 2,000 PDFs, browse in
  all four views, repair metadata online, read with resume + annotations, search
  metadata within budget, optional explainable AI — must pass with AI disabled
  not blocking any core path.
- **V1**: Phases 11, 13 (reading plans), 15 (OCR, password PDFs, batch), full
  health/provenance, semantic + hybrid search, local embeddings, plus the LAN
  groundwork.
- **V2**: split view, AI answer mode with local‑evidence citation, the full
  LAN/classroom product (16–18) hardened, and **Spanish/Italian/German**.

> The exact tier badge for each requirement lives in `SOURCE-SUMMARY.md` and is
> restated in each phase's `README.md` *Requirements covered* table.

---

## 5. Cross‑cutting threads (owned in one phase, enforced in all)

| Thread | Established in | Enforced by |
| --- | --- | --- |
| **Colorful icon system** (`ICON-SYSTEM.md`) | Phase 03 | Every UI phase ships an **icon manifest**; the owner is asked to procure the named premium PNG icons; design‑audit gate at 06, 14, 21. |
| **Multilingual** (`I18N-STRATEGY.md`) | Phase 03 (en/fr) | No hard‑coded strings (lint gate); pseudolocale CI check; full es/it/de in Phase 21. |
| **Accessibility (WCAG 2.2 AA)** | Phase 03 | axe‑style automated check + keyboard/SR pass at every UI phase; final audit Phase 21. |
| **Privacy & the single egress chokepoint** | Phase 12 | Architecture test forbids direct provider calls; payload‑preview integration test on every AI/enrichment path; DPIA per off‑device feature in Phase 19. |
| **Reversibility & data‑loss prevention (R1)** | Phase 04 | Backup‑write‑verify‑restore fault‑injection tests in Phases 07, 09, 20. |
| **Performance budgets (NFR‑OGMA / NFR‑PROD)** | Phase 02 (instrumentation) | CI benchmarks per budget; hard gates once reference hardware fixed (Phase 00 → 20). |
| **Bounded‑context discipline** | Phase 02 | `Architecture_DomainProject_HasNoOutwardDependencies` and dependency‑graph tests on every PR. |
| **Documentation** | Phase 02 | XML docs required; `docs-architect` / ADR updates per phase; `/init` CLAUDE.md kept current. |

---

## 6. Global Definition of Done (applies to every phase)

A phase does not close until **all** of the following hold (each phase adds its
own specific DoD on top):

1. Every in‑scope requirement (FR/NFR/CTRL ID) has a passing deterministic test
   or a recorded, tagged coverage gap.
2. The golden‑corpus suite passes; no open **R1 (data‑loss)** or **R2
   (privacy‑breach)** defect.
3. `dotnet format --verify-no-changes`, `dotnet build` (warnings‑as‑errors), and
   `dotnet test` all pass; architecture tests pass.
4. All new user‑facing strings are externalized and present in **en + fr**;
   pseudolocale check passes.
5. Every new interactive control has a colorful icon **and** an accessible
   text/`aria` label; keyboard + screen‑reader walkthrough passes; the phase's
   **icon manifest** is complete and procured icons are wired in (or
   placeholders flagged with a tracking item).
6. New ADRs/decisions recorded; affected reference docs updated; the engine
   validation gate `python -m engine validate Ogma-Library` (hybrid gate) passes
   where applicable.
7. Performance budgets touched by the phase are instrumented and within budget
   (or recorded as trend data pending reference hardware).
8. A code review (`/code-review` or the `code-reviewer` skill) and, for
   security‑ or privacy‑touching phases, a `security-review` are completed and
   findings resolved.

---

## 7. The two questions we keep asking the owner

Per the owner's standing instruction, two procurement/decision loops run for the
whole project:

1. **Premium PNG icons.** Whenever a phase's icon manifest names new icons, we
   **ask Peter** to buy/supply the matching premium PNG set (with the agreed
   style, color, and sizes from `ICON-SYSTEM.md`) before that UI work is marked
   done. Placeholders may be used during build but are a release blocker.
2. **Owner sign‑offs.** The 8 PRD open questions, the SRS context gaps, each
   phase gate, and any change to a baselined requirement require Peter's
   explicit sign‑off (recorded in the phase folder or an ADR).

---

## 8. Status

This plan is the **v1.0 baseline** of the grand plan, authored 2026‑05‑30. It is
itself subject to the owner sign‑off in Phase 00. Each phase folder is a living
document; substantive changes are recorded in that folder's `README.md` change
log and, where they alter a baselined decision, in a new ADR.
