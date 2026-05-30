# Phase 23 — Icon Manifest

> Phase 23 is an operations, Extension SDK, and launch phase. It introduces
> two small new UI surfaces with icon requirements:
> 1. **Import wizard menu items** (Zotero, Calibre, Goodreads importers).
> 2. **MCP listener Settings toggle**.
>
> All other Phase 23 work (SLO monitoring, runbooks, SDK interfaces,
> documentation) is non-UI and produces no icon surfaces.

---

## Icon manifest

| Icon key | Used on | Meaning | Style / color note | Sizes | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_import_zotero` | Library menu > "Import from Zotero" | Import a Zotero library export (RDF/JSON) | Zotero-inspired red/orange motif — but must be a **custom, non-trademark icon** (not Zotero's logo); book-plus or library-import motif; `accent/oak` | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_import_calibre` | Library menu > "Import from Calibre" | Import a Calibre library export (OPF/DB) | Library-import motif; `accent/oak` family, distinct from Zotero import | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_import_goodreads` | Library menu > "Import from Goodreads" | Import a Goodreads export CSV | Reading-list import motif; `accent/ink` (reading context) | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_mcp_server` | Settings > Advanced > MCP Listener toggle | Represents the local MCP (Model Context Protocol) server | Network-connection or plug/socket motif; `accent/plum` (AI / intelligence surfaces) | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_mcp_server_active` | MCP listener toggle in Active state | MCP server is running | Same motif as above with an active/pulse indicator; `accent/sage` (active / OK) | 16/24/32 @1x-3x | ⬜ to procure |

---

## Accessible label keys

| Icon key | Label resource key | en text |
| --- | --- | --- |
| `ic_import_zotero` | `Library.Import.Zotero.Label` | "Import from Zotero" |
| `ic_import_calibre` | `Library.Import.Calibre.Label` | "Import from Calibre" |
| `ic_import_goodreads` | `Library.Import.Goodreads.Label` | "Import from Goodreads" |
| `ic_mcp_server` | `Settings.Mcp.Server.Label` | "Local MCP server" |
| `ic_mcp_server_active` | `Settings.Mcp.Server.Active.Label` | "Local MCP server (running)" |

All label keys must be present in `en`, `fr`, `es`, `it`, `de` by Phase 23
(Phase 21 completed the 5-locale foundation; Phase 23 adds these new keys
in all 5 locales).

---

## Note on importer icons (trademark)

The icons for the Zotero, Calibre, and Goodreads importers must **not** use
the trademarks or logos of those products. They must be original, custom icons
that communicate "import from an external library tool" in the Ogma Library
style. The product names appear in the menu item text labels; the icons
differentiate the three importers visually without infringing on trademarks.

---

## Owner procurement request

**To: Peter Bamuhigire**
**For: Phase 23 — Beta, Launch & Post-Launch Operations + Extension SDK**

Please procure the following premium PNG icons for the Phase 23 importer and
MCP surfaces:

**Icons needed (5 icons):**

1. `ic_import_zotero` — book-import or library-import motif; oak/amber accent;
   communicates "import from Zotero" without using Zotero's trademark logo.
2. `ic_import_calibre` — library-import motif; oak family, visually distinct
   from the Zotero importer icon.
3. `ic_import_goodreads` — reading-list or books-check motif; ink accent.
4. `ic_mcp_server` — plug/socket or network-node motif; plum accent;
   communicates "local AI connection point."
5. `ic_mcp_server_active` — same motif with a pulse or active indicator;
   sage/green accent.

**Style requirements (from `ICON-SYSTEM.md`):**
- Colorful, duotone or flat-color style; consistent with the Phase 03 icon family.
- PNG at **@1x, @2x, @3x** in base sizes **16, 24, 32 px**.
- Light and dark variants (if vendor provides them).
- License permitting redistribution in a signed desktop app and Store distribution.
- **No third-party trademarks** (Zotero Z logo, Calibre book logo, Goodreads G logo
  must not appear; custom iconography only).

**Storage paths:**
- `OgmaLibrary.App/Assets/icons/library/` (import icons)
- `OgmaLibrary.App/Assets/icons/settings/` (MCP icons)

> Note: If Phase 03's purchased icon family includes generic import/export and
> network/connection icons, those may be reused for the import and MCP icons
> respectively, provided they are visually distinguishable from each other.
> Please check the Phase 03 icon purchase against these 5 requirements before
> commissioning new artwork.
