# Colorful Premium Icon System

> Owner directive: *"I want a beautiful app. Buttons and menus must have colorful
> icons. Design it with that in mind and always ask for them — I will buy premium
> PNG icons."*

This document defines how Ogma Library uses colorful premium icons consistently,
accessibly, and across Windows + macOS at every density — and the standing
**procurement loop** by which we ask Peter to buy each icon set.

---

## 1. Design intent

- Every **button, menu item, toolbar action, navigation entry, status chip, and
  empty‑state** carries a **colorful icon**. Icons are a primary part of the
  visual language, not decoration.
- The aesthetic target is the PRD's **"premium means calm control"**: warm,
  refined, library‑like (oak, amber, parchment, ink), colorful but not noisy.
  Color differentiates *function*, not just adds saturation.
- Icons reinforce, never replace, meaning. **Accessibility rule:** every icon is
  paired with a visible or `aria` text label; color is never the sole carrier of
  state (WCAG 2.2 — NFR‑PROD‑007/008). A colorblind or screen‑reader user must
  lose nothing.

## 2. Asset format & pipeline

- **Source format:** premium **PNG** sets purchased by the owner, plus an SVG
  fallback where the vendor supplies it (SVG preferred for crisp scaling; PNG is
  the agreed deliverable the owner buys).
- **Densities:** ship **@1x, @2x, @3x** so both standard and HiDPI/Retina
  displays are crisp on Windows and macOS. Base sizes: **16, 24, 32, 48 px**
  (toolbar/menu = 24; large actions/empty‑states = 48).
- **Storage:** `OgmaLibrary.App/Assets/icons/<category>/<icon_key>@Nx.png`.
  Categories mirror bounded contexts (library, catalogue, reader, search, ai,
  shelf3d, settings, classroom, admin).
- **Indexing:** a generated `IconCatalog` (an enum/keyed registry) maps each
  `icon_key` to its assets and a required accessible‑label resource key, so an
  icon with no localized label fails the build. This is the single registry the
  UI binds to.
- **Theming:** light + dark variants where the vendor provides them; otherwise a
  tasteful tint/treatment defined in the design tokens (Phase 03). The 3D shelf
  spine/cover textures are a separate asset class (SkiaSharp‑generated), not
  icons.

## 3. Per‑phase icon manifest (the workflow)

Each UI‑bearing phase produces an `icons.md` **manifest** (format in
`CONVENTIONS.md`) listing every new icon: key, where used, meaning, proposed
style/color, sizes, and status. The lifecycle of each icon is:

```
⬜ to procure  →  🟨 placeholder in use  →  ✅ premium PNG wired
```

- During build we may use neutral placeholders so work isn't blocked, but a
  shipping release with placeholder icons is a **release blocker**.
- When a phase reaches its UI work, we **ask Peter** (an "Owner ask") to buy the
  named premium PNG set in the agreed style/sizes. The request lists exact keys,
  the style tokens below, and the density matrix.

## 4. Style tokens (style locked by D‑001; palette ratified in Phase 03)

> **Decision D‑001:** the icon style is **flat full‑color** (see `DECISIONS.md`).
> The palette below maps color *families* to *functional areas* so the
> flat‑color set reads as calm and learnable rather than noisy. The owner
> approves/adjusts exact hues when choosing the vendor set in Phase 03.

| Token | Proposed value | Use |
| --- | --- | --- |
| `accent/oak` | warm oak amber | primary actions, library identity |
| `accent/ink` | deep ink blue | reading & navigation |
| `accent/sage` | muted green | success, "available", health‑OK |
| `accent/clay` | terracotta | warnings, "needs attention" |
| `accent/plum` | soft plum | AI / intelligence surfaces |
| `accent/slate`| neutral slate | settings, secondary actions |
| `surface/parchment` | warm off‑white | light theme base |
| `surface/walnut` | dark warm brown | dark theme base |

> Color *families* map to *functional areas* so a user learns the language: AI
> surfaces read plum, health reads sage/clay, reading reads ink, library/identity
> reads oak.

## 5. Vendor / set selection (an Owner ask in Phase 03)

We ask the owner to pick **one cohesive premium icon family** (single vendor,
consistent grid/stroke/corner radius) to avoid a patchwork look. Per D‑001 the
family must be **flat full‑color**. Candidate qualities to confirm with the owner:

- **Flat full‑color** style consistent with the warm library aesthetic; vivid but
  constrained to the functional palette above so it stays calm, not busy.
- Full coverage of our domain (books, shelves, scan, search, AI, reader, tags,
  classroom, admin) or a vendor that does custom additions.
- PNG @1x/2x/3x + license that permits redistribution inside a signed desktop
  app **and** Store distribution (Mac App Store / Windows Store) — license must
  allow app‑store resale embedding.
- Light/dark variants.

## 6. Governance

- The `design-audit` and `practical-ui-design` / `premium-ui-ux-design` skills
  gate icon coherence at Phases 03, 06, 14, and 21.
- No icon ships without (a) a wired premium PNG at all densities, (b) a localized
  accessible label in en + fr (and es/it/de by Phase 21), and (c) a placeholder
  status cleared to ✅ in its phase `icons.md`.
- A consolidated **master icon manifest** is maintained at
  `docs/plans/grand-plan/_icons/MASTER-MANIFEST.md` (created in Phase 03),
  aggregating every phase's icons so the owner can buy in efficient batches.
