# Phase 18 — Icon Manifest

Icons introduced by School Administration & Managed AI: the admin console
sections, profile enrollment, AI key management, usage dashboard, audit log,
and student smart-search surfaces.

This is the most icon-intensive phase in Part V. The admin console requires
a full set of functional icons to distinguish high-density, related actions.

Style tokens reference: `ICON-SYSTEM.md §4`.

---

## Icon manifest

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_admin_console` | Admin console nav entry; admin mode badge | "Administrator" — top-level admin identity | Shield or crown on a book; oak-amber accent (authority/library identity) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_enroll_profile` | "Enroll" button in enrollment table; enrollment token panel | Add a new student or teacher profile | Person-with-plus symbol; sage green accent (add/positive) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_permissions_roles` | Role column header; role selector in enrollment form | Roles and permissions | Key + person or shield-with-person; slate accent (settings/access) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_ai_key` | AI key panel header; "Save key" button; key status indicator | School AI API key management | Key icon with a circuit/AI motif; plum accent (AI surfaces) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_quota` | Quota progress bar label; per-student quota cell in table | AI query quota / budget | Gauge or fuel-tank icon; clay accent (approaching limit) / sage (healthy) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_usage_chart` | Usage dashboard nav entry; dashboard header | AI usage / spend visualization | Bar chart or trend line icon; ink-blue accent (informational) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_curate_shelf` | Shared shelf management section | Admin curates shared shelves | Bookshelf with a pencil/star; oak-amber accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_moderate_ai` | Student smart-search bar icon; AI policy section | AI moderation / governed AI search | Robot/AI head inside a safe or moderation ring; plum + slate accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_publish_folder_admin` | Library publishing table; "Publish" / "Unpublish" actions | Publish a folder to the shared library | Folder with an outward broadcast arrow; oak-amber; distinct from Phase 16 `ic_publish_folder` (admin variant: includes a checkmark) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_audit_log` | Audit log viewer nav entry; log table header | Tamper-evident activity log | Scroll/ledger with a lock or timestamp; slate accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_dpia_shield` | DPIA / legal basis configuration panel; DPIA status badge | Data Protection Impact Assessment / privacy compliance | Shield with a checkmark and a small person silhouette (data subject protection); plum accent | 16/24/32/48 @1x‑3x | ⬜ to procure |

---

## Accessible label resource keys

| Icon key | `en` label | `fr` label |
| --- | --- | --- |
| `ic_admin_console` | "Admin Console" | "Console d'administration" |
| `ic_enroll_profile` | "Enroll Profile" | "Inscrire un profil" |
| `ic_permissions_roles` | "Permissions and Roles" | "Autorisations et rôles" |
| `ic_ai_key` | "AI API Key" | "Clé API IA" |
| `ic_quota` | "AI Quota" | "Quota IA" |
| `ic_usage_chart` | "Usage Dashboard" | "Tableau de bord d'utilisation" |
| `ic_curate_shelf` | "Curate Shelf" | "Organiser l'étagère" |
| `ic_moderate_ai` | "AI Smart Search" | "Recherche intelligente IA" |
| `ic_publish_folder_admin` | "Publish Folder" | "Publier le dossier" |
| `ic_audit_log` | "Audit Log" | "Journal d'audit" |
| `ic_dpia_shield` | "Privacy Compliance" | "Conformité à la vie privée" |

`es`, `it`, `de` label keys created (empty) from Phase 18; translations in Phase 21.

---

## Color & style guidance

- `ic_admin_console`: this icon appears in the mode indicator and nav sidebar.
  It must convey authority without being alarming. The oak-amber shield/crown-
  on-book motif matches the library identity while signaling a privileged mode.
- `ic_ai_key` vs `ic_certificate` (Phase 16): these must be visually distinct
  at 16 px. `ic_certificate` = TLS certificate (security, slate); `ic_ai_key` =
  API key for AI (intelligence, plum). Different shapes required.
- `ic_quota` gauge: the same icon is used at healthy (sage) and warning (clay)
  states via color tinting — the shape/silhouette must be legible at both. Use a
  simple gauge/fuel-tank silhouette, not a complex pie chart.
- `ic_dpia_shield`: this icon appears in a critical admin setup step (legal basis
  configuration). It should feel serious and trust-building, not alarming. Plum
  (AI/intelligence) is appropriate because DPIA is the privacy gate for AI.
- `ic_moderate_ai`: used both in the student-facing smart-search bar and in the
  admin AI policy section. It must work at 24 px (toolbar) and 48 px (section
  header). The "AI inside a moderation ring" motif conveys both intelligence and
  oversight.
- No icon is the sole carrier of state: the quota progress bar always shows a
  numeric label; the DPIA badge always has a text status; the audit log entry
  always has a text action field.

---

## Owner procurement request

> **Peter — action required before Phase 18 UI work (WP2..WP9) ships:**
>
> Please procure the following **11 premium PNG icon set** entries for the
> School Administration & Managed AI console. Same vendor family as Phases 03,
> 16, and 17.
>
> **Icon keys to procure:**
> `ic_admin_console`, `ic_enroll_profile`, `ic_permissions_roles`,
> `ic_ai_key`, `ic_quota`, `ic_usage_chart`, `ic_curate_shelf`,
> `ic_moderate_ai`, `ic_publish_folder_admin`, `ic_audit_log`,
> `ic_dpia_shield`
>
> **Required sizes:** 16 px, 24 px, 32 px, 48 px — @1x, @2x, @3x density.
>
> **Required variants:** light + dark theme.
>
> **License requirement:** redistribution in signed desktop app + Mac App Store
> + Microsoft Store permitted.
>
> **Delivery path:** `OgmaLibrary.App/Assets/icons/admin/<icon_key>@Nx.png`
>
> **Design note:** `ic_ai_key` and `ic_certificate` (Phase 16) must be visually
> distinct at 16 px — please ensure the vendor produces clearly different shapes
> for these two (not just color variants). The `ic_quota` gauge icon must be
> legible when tinted both sage (healthy) and clay (warning).
