# Phase 22 — Icon Manifest

> Phase 22 is a packaging, signing, and store submission phase. It introduces
> **no new interactive UI icons** in the application itself.
>
> The one UI surface added is the **"LAN Host not available in the App Store
> build" notice** in Settings. This notice uses the existing
> `ic_lan_host_disabled` icon (if introduced in Phase 16; otherwise the
> `ic_settings_info` icon from Phase 03 is reused). No new icon procurement
> is required for the app UI.
>
> **Store listing assets** — app icon variants and store artwork — are a
> separate category documented below and require Owner procurement.

---

## In-app icon manifest (Phase 22)

| Icon key | Used on | Notes | Status |
| --- | --- | --- | --- |
| `ic_lan_host_disabled` (Phase 16) or `ic_settings_info` (Phase 03) | Settings > Library Sharing > MAS notice | Reused from prior phase; no new procurement | ✅ (existing) |

No new in-app icon keys are introduced in Phase 22.

---

## Store listing asset requirements

The Mac App Store and Microsoft (Windows) Store require specific icon and
screenshot assets that are distinct from the in-app icons. These are store
**marketing assets**, not application icons. They must be procured or produced
before the store submissions in WP8 and WP9.

### Mac App Store icon sizes required

| Asset | Size | Format | Notes |
| --- | --- | --- | --- |
| App icon | 1024 × 1024 px | PNG (no alpha on MAS) | Used on the store listing page |
| App icon | 512 × 512 px | PNG | Finder / Spotlight |
| App icon | 256 × 256 px | PNG | |
| App icon | 128 × 128 px | PNG | |
| App icon | 64 × 64 px | PNG | |
| App icon | 32 × 32 px | PNG | |
| App icon | 16 × 16 px | PNG | |
| App icon (@2x) | 2048 × 2048 px | PNG | Retina |
| App icon (@2x) | 1024 × 1024 px | PNG | |
| App icon (@2x) | 512 × 512 px | PNG | |

All sizes are bundled in `icon.icns` by the CI pipeline via `iconutil`.

### Windows Store icon sizes required

| Asset | Size | Format | Notes |
| --- | --- | --- | --- |
| `StoreLogo.png` | 50 × 50 px | PNG | Required for Store listing |
| `Square44x44Logo.png` | 44 × 44 px | PNG | Taskbar / Start |
| `Square150x150Logo.png` | 150 × 150 px | PNG | Start menu medium tile |
| `Square310x310Logo.png` | 310 × 310 px | PNG | Start menu large tile |
| `Wide310x150Logo.png` | 310 × 150 px | PNG | Start menu wide tile |
| Store listing banner | 1920 × 1080 px | PNG or JPEG | Promotional banner |
| App screenshots | 1366 × 768 px or 1920 × 1080 px | PNG | Minimum 1, recommended 4 |

### Store screenshot requirements

Both stores require screenshots showing the key app surfaces. Recommended
screenshots (per locale, per platform):

1. 3D bookshelf view with a populated library.
2. Grid view with the book-detail panel open.
3. PDF reader with an annotation visible.
4. Search results with an AI-assisted result card.
5. Settings / Privacy Center.

Screenshots must be localized (UI visible in the locale for which the screenshot
is submitted) for each of the 5 locales.

---

## Owner procurement request

**To: Peter Bamuhigire**
**For: Phase 22 — Store Listing Assets**

Phase 22 requires the following store assets before the Windows Store and
Mac App Store submissions can be completed:

1. **App icon at all required sizes** (see tables above). The app icon should
   be the "Ogma Library" brand mark — a premium, colorful, library-themed icon
   consistent with the Phase 03 icon family. The 1024 × 1024 px master asset
   is the key deliverable; all other sizes can be derived from it.

2. **Store listing screenshots** (minimum 5 per platform, ideally localized
   for all 5 locales). The team can produce these by running the app and
   taking high-quality screenshots on the reference machines; owner review
   and approval of the final screenshot set is required before submission.

3. **Store listing banner (Windows Store)**: 1920 × 1080 px promotional image;
   can be derived from the 3D bookshelf screenshot with the Ogma Library
   brand mark overlaid.

**Style requirements:**
- App icon: follows the `ICON-SYSTEM.md` aesthetic (warm, premium, library-
  themed; oak-amber accent; duotone or flat-color style).
- Consistent with the Phase 03 icon family chosen by the owner.
- PNG format; no alpha channel on the MAS 1024 × 1024 asset.

**Deadline:** Store listing assets must be available before WP8 (Windows Store
submission) and WP9 (MAS submission) begin — approximately Week 2 of Phase 22.

**Existing Phase 03 icon:** If the Phase 03 premium icon family purchase
included an app icon at 1024 × 1024, it may already satisfy the store
requirements. Please confirm with the icon vendor whether the license permits
store distribution and whether the 1024 × 1024 master asset is available.
