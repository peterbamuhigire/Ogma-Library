# LAN / Classroom E‑Library Architecture

> Owner vision: *"A world‑class tool that can create e‑libraries, e.g. in a LAN
> setting where a central computer has the folder with PDFs and students use
> Ogma Library on any computer to access them; and schools can have AI API keys
> that we set up so students can do smart searches of the books."*

This is the architectural reconciliation for the networked product. It extends —
without breaking — the local‑first single‑user design in the HLD. It is realized
in **Phase 16 (Host)**, **Phase 17 (Client/Classroom)**, and **Phase 18 (School
Admin & Managed AI)**, each with its own ADRs and DPIA.

---

## 1. The tension we must resolve

The signed baseline says (SRS CI‑2, HLD §1.3): *"The application opens no inbound
network listener and exposes no server endpoint."* The classroom vision requires
exactly such a listener on the **host** machine. We resolve this not by deleting
CI‑2 but by **scoping** it: CI‑2 remains the default for the single‑user product,
and a **new, opt‑in "Library Host mode"** introduces a deliberately‑designed,
authenticated, LAN‑bounded server surface with its own threat model and controls.
This is recorded in a new ADR (proposed ADR‑0010) that *amends* CI‑2's scope.

## 2. Three roles, one codebase

The same Avalonia/.NET 10 application runs in one of three modes; the mode is a
runtime configuration, not a separate product.

| Role | Who | What it does |
| --- | --- | --- |
| **Standalone** (default) | Any user | The local‑first product exactly as in the HLD. No network listener. |
| **Library Host** | The central classroom computer | Holds the PDF folder + catalogue of record; serves catalogue projections, covers/spines, page renders or file streams, and gated AI search to LAN clients. Opt‑in; explicitly started by an admin. |
| **Client / Classroom** | Student computers on the LAN | Discovers and connects to a Host; browses, reads, searches; keeps **private** per‑student reading state, annotations, and progress; works offline against a local cache. |

The bounded‑context discipline is preserved: the **Library Catalogue context
stays the single source of truth** and lives on the Host. Clients are *projection
consumers* exactly as the Reader/3D/Search contexts are in the monolith — they
read identity through contracts, never own it.

## 3. Host architecture (Phase 16)

- A new bounded context, **Library Sharing / Host**, owns the LAN server surface.
  It is the *only* place with an inbound listener, isolated from the credential
  store and from the untrusted‑PDF workers exactly as those workers are isolated
  today (CTRL‑OGMA‑005).
- **Transport:** HTTPS over the LAN with a Host‑generated certificate the admin
  can trust‑pin to clients (self‑signed root provisioned at setup), or a
  mutually‑authenticated scheme — chosen by a **Phase 01 LAN spike** and recorded
  in ADR‑0010. No plaintext content on the wire.
- **Discovery:** zero‑config LAN discovery (mDNS/DNS‑SD style) so a student finds
  "Room 12 Library" without typing an IP; manual host‑address entry is the
  fallback.
- **Content serving:** the Host serves (a) catalogue projections (the same
  read‑model the grid/list/3D consume), (b) `ogma://`‑equivalent cover/spine/
  thumbnail assets, and (c) either **page renders** (Host renders, streams
  images — keeps PDFs from leaving the Host) or **file streams** (client renders
  locally) — a privacy/perf trade decided per deployment in admin settings.
- **No inbound write to identity from clients** except through authorized,
  audited use cases (e.g. a teacher curating shelves). Student annotations/
  progress are **client‑private by default** and optionally synced to a
  per‑student store the Host holds.
- **Capacity:** a classroom is ~20–40 concurrent clients; the Host is a desktop,
  not a datacenter. Budgets (concurrent readers, render throughput) are set in
  Phase 16 and benchmarked in Phase 20.

## 4. Client / Classroom mode (Phase 17)

- **Profiles & roles:** student, teacher, guest. A student's reading state,
  annotations, bookmarks, reading memory, and AI history are **theirs**, private
  from other students, stored locally and optionally backed to the Host under
  the student's identity.
- **Offline‑first cache:** a client caches catalogue + opened books so a dropped
  LAN link degrades gracefully (principle 6). `pwa-offline-first` patterns inform
  the cache/sync design.
- **Sync:** last‑write‑wins per‑student state with conflict surfacing; designed
  to admit the schema‑ready cloud‑sync direction (OQ‑08) without committing to it.
- **The single‑user product is unaffected:** a student can also run Standalone
  on their own books.

## 5. School administration & managed AI (Phase 18)

- **Admin console:** manage the shared library (which folders are published,
  curation into shelves, availability), enroll student/teacher profiles, set
  permissions, and view audit/usage.
- **School‑provisioned AI keys:** the school supplies its own provider API
  key(s); the Host holds them in OS credential storage (CTRL‑OGMA‑001) and is the
  **single egress chokepoint** for the whole classroom — students never see or
  hold keys, and all AI traffic routes through the Host's `IAiProvider` gateway
  under the four privacy tiers. A class default of **metadata‑only** is enforced;
  content‑aware is an admin opt‑in per library.
- **Entitlements & quotas:** per‑student/per‑class AI usage budgets, rate limits,
  and cost visibility (`saas-entitlements-and-plan-gating`,
  `saas-rate-limiting-and-quotas`, `ai-cost-and-metering`,
  `ai-entitlements-and-feature-gating`). A student smart‑search consumes the
  school's metered budget; the admin sees spend.
- **Moderation & safety:** student‑facing AI output is bounded to the curated
  collection (answer mode cites local evidence only, FR‑AI‑008); admin controls
  and audit (`ai-agent-governance-and-limits`, `ai-agent-safety-and-red-team`)
  govern misuse. Every off‑device call still produces a local audit entry
  (CTRL‑OGMA‑018) and is DPIA‑screened (CTRL‑OGMA‑024) — schools handle minors'
  data, so the DPIA and jurisdiction work (Phase 00 context gap) is **critical**
  here.

## 6. Security & privacy posture for the networked product

- The Host's inbound surface is the new highest‑risk asset → full threat model in
  Phase 19 (`stride-analysis-patterns`, `attack-tree-construction`,
  `dual-auth-rbac`, `network-security`).
- Students are often **minors** → child‑data protection raises the compliance
  bar: explicit lawful basis, data minimization, the school as data controller,
  and the `uganda-dppa-compliance` / GDPR‑style `dpia-generator` work apply per
  off‑device feature.
- Authentication: clients authenticate to the Host; teachers/admins have elevated
  roles (`dual-auth-rbac`, `mobile-rbac` patterns). No anonymous write to shared
  curation.
- The privacy‑tier model and payload preview operate **on the Host** on behalf of
  the class; a student sees the active tier and a payload preview before any
  off‑device search, preserving the explainability and consent principles even
  in managed mode.

## 7. Sequencing & dependency notes

- Phase 16 depends on the catalogue (04–05), reader (08), and search (10–11)
  being solid — you cannot share what isn't built. The **LAN transport spike is
  pulled forward to Phase 01** so the architecture is proven early.
- Phase 18 depends on the AI gateway + privacy center (12) and entitlement
  patterns; it is where the school‑managed‑AI value is realized.
- The networked product is **V1→V2** in tiering; the MVP is the standalone
  cross‑platform desktop app. The LAN groundwork (clean read‑model projections,
  contract boundaries) is laid in the core phases so 16–18 are an extension, not
  a rewrite.

## 8. New ADRs introduced by this track

- **ADR‑0010** — Opt‑in Library Host mode amends CI‑2; LAN server surface,
  transport, and isolation.
- **ADR‑0011** — Classroom identity, roles, and per‑student private state model.
- **ADR‑0012** — School‑managed AI: keys on Host, class‑level gateway,
  entitlements/quotas, minors' data handling.
- (Numbers proposed; ratified when the track is scheduled.)
