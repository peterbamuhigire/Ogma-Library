"""Generate Ogma's duotone "Lamplit" icon set from Lucide sources (Sept-23 Phase 09, D-07).

Usage:
    python scripts/icons/New-DuotoneIcons.py [path/to/@iconify-json/lucide/icons.json]

Without an argument the vendored subset (scripts/icons/lucide-subset.json) is used, so the set
is reproducible offline. With an argument, the subset is refreshed from that Lucide package
first (only the glyphs named in icon-map.txt are kept).

Each icon is the Lucide outline (ISC licence, see src/OgmaLibrary.App/Assets/icons/LICENSE-ICONS.txt) redrawn as
a duotone: a 1.75 px stroke in the category hue over a translucent fill of the same hue.
The hues are Ogma icon hues (Themes/Tokens.axaml, Color.Hue.*), chosen to hold >= 3:1
against the cream canvas, the raised card, the ink navigation rail and the Night canvas.
"""

from __future__ import annotations

import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
HERE = ROOT / "scripts" / "icons"
MAP = HERE / "icon-map.txt"
SUBSET = HERE / "lucide-subset.json"
CATALOG = ROOT / "src" / "OgmaLibrary.App" / "Icons" / "IconCatalog.cs"
ICONS = ROOT / "src" / "OgmaLibrary.App" / "Assets" / "icons"

# Keep in sync with Color.Hue.* in Themes/Tokens.axaml.
HUES = {
    "amber": "#B87414",
    "lapis": "#4A7CDC",
    "malachite": "#1E9468",
    "plum": "#A55DC0",
    "saffron": "#A8870A",
    "garnet": "#D14D5B",
    "teal": "#3B8FA0",
    "slate": "#6F86A0",
}


def read_map() -> list[tuple[str, str, str]]:
    rows = []
    for line in MAP.read_text(encoding="utf-8").splitlines():
        if line.strip() and not line.startswith("#"):
            key, glyph, hue = line.split()
            rows.append((key, glyph, hue))
    return rows


def refresh_subset(source: pathlib.Path, rows) -> None:
    data = json.loads(source.read_text(encoding="utf-8"))
    icons, aliases = data["icons"], data.get("aliases", {})
    subset = {}
    for _, glyph, _ in rows:
        name = glyph
        while name not in icons and name in aliases:
            name = aliases[name]["parent"]
        subset[glyph] = icons[name]["body"]
    SUBSET.write_text(
        json.dumps({"source": "@iconify-json/lucide", "license": "ISC", "icons": subset},
                   indent=1, sort_keys=True) + "\n",
        encoding="utf-8")


def catalog_paths() -> dict[str, str]:
    text = CATALOG.read_text(encoding="utf-8")
    return dict(re.findall(r'\["(ic_[a-z0-9_]+)"\]\s*=\s*"([^"]+)"', text))


def duotone(body: str, hue: str) -> str:
    # Drop Lucide's per-element paint so the group paint applies uniformly.
    body = re.sub(r'\s(fill|stroke|stroke-width|stroke-linecap|stroke-linejoin)="[^"]*"', "", body)
    return (
        '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">'
        f'<g fill="{hue}" fill-opacity="0.2" stroke="{hue}" stroke-width="1.75" '
        f'stroke-linecap="round" stroke-linejoin="round">{body}</g></svg>\n'
    )


def main() -> int:
    rows = read_map()
    if len(sys.argv) > 1:
        refresh_subset(pathlib.Path(sys.argv[1]), rows)
    subset = json.loads(SUBSET.read_text(encoding="utf-8"))["icons"]
    paths = catalog_paths()
    missing = [key for key, _, _ in rows if key not in paths]
    unmapped = [key for key in paths if key not in {r[0] for r in rows}]
    if missing or unmapped:
        print("catalog/map mismatch", missing, unmapped)
        return 1
    for key, glyph, hue in rows:
        target = ICONS / paths[key]
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(duotone(subset[glyph], HUES[hue]), encoding="utf-8", newline="\n")
    print(f"wrote {len(rows)} icons")
    return 0


if __name__ == "__main__":
    sys.exit(main())
