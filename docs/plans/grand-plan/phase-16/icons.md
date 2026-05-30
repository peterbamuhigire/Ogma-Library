# Phase 16 — Icon Manifest

Icons introduced by the LAN Library Server (Host Mode) UI surfaces: the
Settings > Sharing panel, Host mode status indicators, connection QR-code flow,
and the publish-folder picker.

Style tokens reference: `ICON-SYSTEM.md §4`.
Procurement workflow: `ICON-SYSTEM.md §3`.

---

## Icon manifest

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_host_sharing` | Settings > Sharing section header; nav entry | "Library Host / Sharing" — the top-level identity of the Host mode feature | Colorful, warm oak-amber; outline book with a radial signal arc | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_host_start` | "Start Hosting" primary action button | Start the LAN listener | Filled play arrow inside a rounded square; sage green accent (positive action) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_host_stop` | "Stop Hosting" button (shown when running) | Stop the LAN listener | Filled square-stop icon; clay/terracotta accent (stop/warning) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_network_lan` | Status chip; network status row | LAN / local network connectivity | Signal-tower or network-hub icon; ink-blue accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_clients_connected` | Connected-client count widget | Number of active student/client connections | Group-of-people or multi-device icon; sage green (positive, "connected") | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_publish_folder` | Folder picker for published library root | Which folder is being shared/published over LAN | Folder with an upward arrow or broadcast symbol; oak-amber | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_certificate` | Certificate / trust panel; CA fingerprint display | TLS certificate / trust anchor | Shield with a checkmark or lock-with-ribbon; slate accent (settings/security) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_qr_fingerprint` | QR-code display panel | QR code for scanning the CA fingerprint / join URL | QR-code grid icon; ink-blue accent | 16/24/32/48 @1x‑3x | ⬜ to procure |

---

## Accessible label resource keys

Every icon must be paired with a localized accessible label. The `IconCatalog`
build check fails if a label key is absent in any active locale.

| Icon key | `en` label | `fr` label |
| --- | --- | --- |
| `ic_host_sharing` | "Library Sharing" | "Partage de bibliothèque" |
| `ic_host_start` | "Start Hosting" | "Démarrer l'hébergement" |
| `ic_host_stop` | "Stop Hosting" | "Arrêter l'hébergement" |
| `ic_network_lan` | "Local Network" | "Réseau local" |
| `ic_clients_connected` | "Connected Clients" | "Clients connectés" |
| `ic_publish_folder` | "Published Folder" | "Dossier publié" |
| `ic_certificate` | "Security Certificate" | "Certificat de sécurité" |
| `ic_qr_fingerprint` | "Join QR Code" | "Code QR de connexion" |

`es`, `it`, `de` label keys must exist (empty strings flagged by pseudolocale
check) from Phase 16; translations provided in Phase 21.

---

## Color & style guidance for procurement

Reference: `ICON-SYSTEM.md §4` style tokens.

- **Primary palette for this phase:** `accent/oak` (sharing/identity actions),
  `accent/sage` (start, connected — positive), `accent/clay` (stop — caution),
  `accent/ink` (network, QR — informational), `accent/slate` (certificate —
  settings/security).
- **Style:** consistent with the vendor family chosen in Phase 03. Colorful
  duotone or flat-color style. Grid: 24×24 base. Stroke weight and corner
  radius must match Phase 03 ratified tokens.
- **Variants:** light and dark theme variants; if the vendor supplies only one,
  the design token tinting rule from Phase 03 is applied programmatically.
- **No icon is the sole carrier of state:** `ic_host_start` / `ic_host_stop`
  are always paired with button text; `ic_clients_connected` is always paired
  with a numeric count label; `ic_network_lan` status chip always has a text
  state label.

---

## Owner procurement request

> **Peter — action required before Phase 16 UI work (WP8) ships:**
>
> Please procure the following **8 premium PNG icon set** entries for the LAN
> Host mode UI surfaces. They must belong to the same cohesive family chosen in
> Phase 03 (same vendor, same grid/stroke/corner-radius).
>
> **Icon keys to procure:**
> `ic_host_sharing`, `ic_host_start`, `ic_host_stop`, `ic_network_lan`,
> `ic_clients_connected`, `ic_publish_folder`, `ic_certificate`,
> `ic_qr_fingerprint`
>
> **Required sizes:** 16 px, 24 px, 32 px, 48 px — at @1x, @2x, @3x density.
>
> **Required variants:** light theme + dark theme (or a single set that works
> on both via the Phase 03 tinting rule if the vendor does not supply variants).
>
> **License requirement:** must permit redistribution inside a signed desktop
> app and Store distribution (Mac App Store + Microsoft Store).
>
> **Delivery path:** `OgmaLibrary.App/Assets/icons/classroom/<icon_key>@Nx.png`
>
> **Style reference:** colorful duotone or flat-color; warm library palette
> (oak-amber, sage, clay, ink, slate) as described in `ICON-SYSTEM.md §4`.
>
> Placeholder icons will be used during build (status `🟨 placeholder in use`)
> but a release with any placeholder icon is a **release blocker**.
