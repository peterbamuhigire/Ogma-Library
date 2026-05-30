# Owner Decisions Log

Decisions made by the product owner (Peter Bamuhigire) that bind the grand plan.
Each entry is dated and supersedes any conflicting default in the foundation
docs or phase folders. Phase folders are reconciled to these decisions.

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

---

### Pending owner asks (tracked, not yet answered)

- **Premium PNG icon procurement** — per‑phase, as each `icons.md` matures
  (ongoing; the consolidated buy list will live in `_icons/MASTER-MANIFEST.md`).
- **Reference‑hardware specification** (CPU/RAM/storage) for the NFR budgets —
  to be fixed in Phase 00 with the owner before perf gates become hard.
- **Vendor selection** for the flat full‑color icon family (single cohesive set)
  — an Owner ask in Phase 03.
