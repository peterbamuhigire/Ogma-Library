# Owner Decisions Log

Decisions made by the product owner (Peter Bamuhigire) that bind the grand plan.
Each entry is dated and supersedes any conflicting default in the foundation
docs or phase folders. Phase folders are reconciled to these decisions.

---

## D‑008 — Key management, identity & data location model (2026‑05‑30)

Confirms how API keys, login, and the database are handled across the two
deployment modes of the one app. Refines ADR‑0007 (AI gateway), ADR‑0010/0011/
0012 (LAN Host / identity / managed AI), and Phases 12, 16–18.

**Two deployment modes, one codebase:**

| | Standalone (home, MVP default) | Classroom / LAN Host (school) |
| --- | --- | --- |
| AI key set by | the user | **the admin** (Host installer) only |
| Key stored in | local OS credential store | **the Host's** OS credential store |
| Key visible to | that user | **no one else** — masked after entry, never shown |
| AI call made by | the local app | **the Host proxies** every call (single egress chokepoint) |
| Database (catalogue + PDFs + state) | **local**, per computer | **central on the Host**; clients cache for offline, source of truth is central |
| Login | **none** (OS user is the boundary) | **yes** — admin creates student/teacher accounts with roles |

Owner decisions (D‑008):

1. **Model = two modes** (confirmed). Standalone is the zero‑friction MVP;
   classroom is the central‑Host model with admin‑set keys + logins.
2. **Classroom access = LAN‑only** (confirmed). Students connect only on the
   school's local network. The Host opens **no inbound internet listener** — only
   the LAN surface (ADR‑0010). This keeps the product local‑first, minimizes the
   attack surface, and keeps minors'‑data compliance lightest. Internet/remote
   access is explicitly **out of scope** (revisit post‑V2 only, behind a fresh
   DPIA).
3. **Standalone = no login** (confirmed). The OS user account is the boundary;
   one library per OS user. Login/roles are a **classroom‑only** concept. (No
   optional local profiles for now.)

**Key handling specifics (binding):** the AI key never leaves the Host; clients
never receive it. The Host runs the `IAiProvider` gateway and proxies student
requests under the four privacy tiers, the class default being metadata‑only.
The key is held only in the Host OS credential store (CTRL‑OGMA‑001), never in
the DB/config/logs (CTRL‑OGMA‑002), and is not re‑displayed after entry (admin
sees "configured ✓", can rotate/remove, not read). Per‑student reading state,
annotations, and AI history are private to the student; the admin sees usage,
cost, and quotas (entitlements) but not students' private reading content. These
refinements are folded into Phases 16–18 and the relevant ADRs when that track
is scheduled.

---

## D‑001 — Icon visual direction: **Flat full‑color** (2026‑05‑30)

The colorful premium icon system uses a **flat full‑color** style (fully colored,
warm, friendly — well suited to the student/classroom audience while staying
"calm, not busy"). This locks `ICON-SYSTEM.md` §4–§5: the master icon manifest
(`_icons/MASTER-MANIFEST.md`) and every phase `icons.md` are specced for flat
full‑color PNGs at @1x/2x/3x (16/24/32/48 px) for Windows + macOS HiDPI/Retina,
with light/dark treatments. We continue to **ask the owner to procure** the named
premium PNG sets per phase. Discipline to keep flat‑color from becoming noisy:
a constrained palette mapped to functional areas (oak=library, ink=reading,
sage/clay=health, plum=AI, slate=settings), consistent grid/stroke, and the
`design-audit` gate at Phases 03/06/14/21. Accessibility unchanged: every icon
keeps a localized text/`aria` label; color is never the sole carrier of meaning.

## D‑002 — LAN / classroom scope: **Post‑MVP V1→V2 as planned** (2026‑05‑30)

The MVP is the polished, cross‑platform (Windows + macOS) **single‑user** desktop
product. The LAN / classroom e‑library (Host = Phase 16, Client = Phase 17,
School Admin & Managed AI = Phase 18) is delivered **after** the MVP, V1→V2, as
the grand plan already sequences it. The LAN groundwork — clean read‑model
projections, contract boundaries, the AI gateway as a reusable chokepoint — is
still laid in the core phases so 16–18 are an extension, not a rewrite. No change
to phase numbering; this confirms the planned sequencing.

## D‑003 — Target jurisdictions: **Global / multi‑region** (2026‑05‑30)

Launch targets span **Uganda (DPPA 2019)**, **EU/UK (GDPR + UK DPA)**, and the
**USA**, i.e. **global / multi‑region**. Compliance is therefore designed to the
**strictest common denominator**. Consequences threaded into the plan:

- **Phase 00** closes the jurisdiction context gap with this multi‑region answer
  and scopes the DPIA accordingly.
- **Phase 18 / 19** treat minors' data as in scope from the start: GDPR (incl.
  GDPR‑K children's consent / age‑of‑consent variance), **US COPPA + FERPA**
  considerations for schools and students, and **Uganda DPPA 2019** — using the
  `uganda-dppa-compliance`, `dpia-generator`, and `security-auditor` skills.
- The **default posture stays Tier‑0 offline / metadata‑only**, which keeps most
  users out of any controller/processor relationship entirely; the compliance
  surface engages only at opt‑in off‑device transmission (SRS §7.7), now assessed
  against all three regimes.
- A per‑off‑device‑feature **DPIA** (CTRL‑OGMA‑024) is required before each such
  feature ships, covering data categories, lawful basis per region, provider
  processing location, cross‑border transfer, retention, erasure, and residual
  risk — with extra rigor for the school‑managed‑AI track (minors).

## D‑004 — Icon master format: **SVG‑first** (2026‑05‑30)

The icon source can provide **SVG**. We therefore standardize on **SVG as the
master format**, with **PNG @1x/2x/3x exported** for the agreed sizes
(16/24/32/48 px) as the wired runtime assets on Windows + macOS. SVG masters give
crisp scaling at any DPI and a clean path to future densities; Avalonia bundles
them via `avares://` (`AvaloniaResource`) — SVG through the Avalonia SVG control
where vector is preferred, PNG where a rasterized colorful asset is simpler. This
updates `ICON-SYSTEM.md` §2: SVG master in `Assets/icons/<category>/<key>.svg`
plus exported `@Nx.png`. The IconCatalog still requires a localized accessible
label per icon or the build fails.

## D‑005 — Icon vendor: **Flaticon** (flat full‑color, SVG) (2026‑05‑30)

The owner has a **Flaticon** account (`flaticon.com`), which is the selected
vendor for the flat full‑color icon family. Flaticon supplies both SVG and PNG
and supports **collections**, which we use to keep one cohesive pack
(consistent grid/stroke/corner radius) across all phases. **Licence
requirement:** Ogma ships commercially on the Mac App Store and Windows Store, so
the icons must be used under **Flaticon Premium** (royalty‑free, **no attribution
required**, redistribution inside a signed/store‑distributed app permitted) — or,
if free‑tier assets are used, the required Flaticon attribution must appear in an
in‑app credits screen. Premium (no‑attribution) is the recommended path for a
premium store product. The per‑phase `icons.md` "to procure" lists are filled
from one or a few coherent Flaticon collections; each `icon_key` → chosen
Flaticon asset URL/ID is recorded in `_icons/MASTER-MANIFEST.md`.

## D‑006 — Avalonia licence: **Community licence registered** (2026‑05‑30)

The owner has signed up for the **Avalonia community licence** under
`peter@techguypeter.com`. Avalonia core is open‑source (MIT); the community
registration covers the team's use and any Avalonia Accelerate/community
entitlements. Phase 02 records the exact licence terms and obligations (if any)
in an ADR/notice and ensures the `THIRD-PARTY-NOTICES` file lists Avalonia and
its licence for the open‑source release. No blocker; recorded for compliance.

## D‑007 — Phase 00 blanket sign‑off + best‑choice defaults (2026‑05‑30)

The owner approved all Phase 00 decisions ("I agree with your decisions, take the
best choices and go ahead"). This **closes the 5 items** previously marked
"Needs owner confirm" in `phase-00/decisions.md` — CON‑1 (reference hardware),
CON‑4 (command‑palette set), CON‑5 (Work/Edition rules), CON‑8 (provider trust
weights), CON‑9 (corpus licensing) — by adopting the recommended defaults as
owner‑ratified. Runtime reconfirmed: **.NET 10 LTS** (OQ‑01/ADR‑0001).

Two best‑choice picks made under this authority:

- **OQ‑02 PDFium wrapper candidates (for the Phase 01 Spike 2 benchmark):**
  Candidate A = **PDFtoImage** (sungaila; wraps bblanchon PDFium, SkiaSharp
  render, cross‑platform, MIT + PDFium BSD). Candidate B = **Docnet.Core**
  (PDFium wrapper, cross‑platform, MIT + PDFium BSD). Both permit Mac App
  Store + Windows Store redistribution. The winner is fixed by the measured
  benchmark and recorded as an ADR‑0004 amendment.
- **CON‑9 golden corpus:** use a **fully synthetic corpus** generated
  programmatically (no third‑party copyrighted PDFs), which is unambiguously
  clear for redistribution in the open‑source test suite. Real‑world edge‑case
  fixtures may be added later only if their licence is recorded.

---

### Pending owner asks (tracked, not yet answered)

- **Premium PNG icon procurement** — per‑phase, as each `icons.md` matures
  (ongoing; the consolidated buy list will live in `_icons/MASTER-MANIFEST.md`).
- **Reference‑hardware specification** (CPU/RAM/storage) for the NFR budgets —
  to be fixed in Phase 00 with the owner before perf gates become hard.
- **Vendor selection** for the flat full‑color icon family (single cohesive set)
  — an Owner ask in Phase 03.
