# Flaticon Download-Agent Prompt

Give this prompt to an agent that has **browser automation tools** (Playwright /
a browser MCP / the `webapp-testing` skill) running in a session where you can
**log into your Flaticon account in the launched browser**. It downloads the 34
Phase 03 SVGs into the project.

---

## PROMPT (copy from here)

You are an icon-acquisition agent for the **Ogma Library** project. Your job is to
download 34 specific **flat full-color SVG** icons from **Flaticon** (the user is a
logged-in account holder) and place them in the repository at the exact paths
given, keeping the set visually cohesive.

Repository root: `C:\wamp64\www\Ogma-Library`
Shopping list (authoritative): `docs/plans/grand-plan/_icons/PHASE-03-FLATICON-SHOPPING-LIST.md`
Destination root: `src/OgmaLibrary.App/Assets/icons/<category>/<icon_key>.svg`

### Setup

1. Use a browser automation tool with a **persistent user-data directory** (so the
   login persists across the run), e.g. Playwright
   `chromium.launchPersistentContext("C:\\temp\\flaticon-profile", { headless: false })`.
   Launch **headed** so the user can complete login/MFA.
2. Navigate to `https://www.flaticon.com` and **pause for the user to log in**
   (print: "Please log into Flaticon in the opened browser, then tell me to
   continue"). Wait for confirmation before proceeding. Verify login by checking
   the account avatar is present.

### For each of the 34 rows in the shopping list

1. Search Flaticon for the row's **search query**; apply the **"Color"** style
   filter (flat full-color).
2. Prefer icons from a **single author/pack** for cohesion — once you pick the
   first icon, note its pack and prefer "more from this pack" for the rest where a
   good match exists.
3. Choose the result whose dominant color best matches the row's **color family**
   (oak/ink/sage/clay/plum/slate). Exact hue need not be perfect; cohesion wins.
4. Open the icon page, click **Download → SVG** (premium download, no attribution).
   If the browser saves to the default downloads folder, **move/rename** the file
   to the row's destination path `src/OgmaLibrary.App/Assets/icons/<category>/<icon_key>.svg`.
5. Record the chosen icon's **Flaticon URL** back into the shopping-list table's
   last column (edit the markdown file).

### Constraints

- Exactly 34 files, named exactly by `icon_key` (no spaces), at the exact paths.
- SVG master is required; also grab PNG if offered (save as `<icon_key>@1x.png`
  etc. alongside).
- Do **not** download attribution-required free icons if the account is Premium —
  use the premium (no-attribution) download. If only free is available for a
  given key, note it for the user.
- If an icon truly has no good flat-color match, leave that row's file missing and
  list it in a "needs manual pick" report at the end.
- Never store or print the user's Flaticon password.

### Output

- A summary: how many of the 34 were downloaded, which pack(s) were used, any
  "needs manual pick" rows, and the updated shopping-list table with URLs.
- Then the engineering team runs `scripts/Import-Icons.ps1` to generate the PNG
  density variants and flip the manifest status to `✅ premium`.

---

## Alternative (no automation): the manual collection method

1. In Flaticon, create a collection `ogma-phase03-core`.
2. Walk the shopping list, add one flat-color icon per row (prefer one pack).
3. Download the collection as a pack (SVG), unzip to a folder.
4. Hand the engineer the folder; they map files to `icon_key`s and run
   `scripts/Import-Icons.ps1 -SourceDir <folder>`.
