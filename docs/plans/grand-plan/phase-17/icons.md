# Phase 17 — Icon Manifest

Icons introduced by Client / Classroom Mode & Multi-User: discovery screen,
TOFU enrollment flow, profile switcher, mode indicator, sync panel, and offline
status chip.

Style tokens reference: `ICON-SYSTEM.md §4`.

---

## Icon manifest

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_connect_to_library` | Discovery screen header; mode-switcher card | "Connect to a Host library" — the entry point for Client mode | Book with a plug/link symbol; oak-amber accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_mode_standalone` | Mode-switcher (Standalone option) | Local-only / standalone mode | Single book; slate accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_mode_classroom` | Mode-switcher (Classroom / Client option) | Classroom / connected mode | Books-with-signal-arc; ink-blue accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_profile_student` | Profile switcher chip; profile creation role selector | Student role | Student/graduation-cap silhouette; plum accent (learning/AI surfaces) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_profile_teacher` | Profile switcher chip; role selector | Teacher role | Teacher/chalkboard silhouette; ink-blue accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_profile_guest` | Profile switcher chip; role selector | Guest (anonymous, no persistence) | Person-outline / ghost silhouette; slate accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_sync` | Sync settings panel; "Sync now" button; sync badge | Synchronize private state with Host | Circular arrows; sage green accent (positive/active) | 16/24/32/48 @1x‑3x | ⬜ to procure |
| `ic_offline` | Offline status chip (toolbar) | LAN connection lost; reading from cache | No-signal or cloud-with-slash; clay/terracotta accent (caution/warning) | 16/24/32/48 @1x‑3x | ⬜ to procure |

---

## Accessible label resource keys

| Icon key | `en` label | `fr` label |
| --- | --- | --- |
| `ic_connect_to_library` | "Connect to Library" | "Se connecter à la bibliothèque" |
| `ic_mode_standalone` | "Standalone Mode" | "Mode autonome" |
| `ic_mode_classroom` | "Classroom Mode" | "Mode salle de classe" |
| `ic_profile_student` | "Student" | "Élève" |
| `ic_profile_teacher` | "Teacher" | "Enseignant" |
| `ic_profile_guest` | "Guest" | "Invité" |
| `ic_sync` | "Sync" | "Synchroniser" |
| `ic_offline` | "Offline" | "Hors ligne" |

`es`, `it`, `de` label keys created (empty) from Phase 17; translations in Phase 21.

---

## Color & style guidance

- `ic_profile_student` / `ic_profile_teacher` / `ic_profile_guest`: role icons
  are the most-repeated icons in the classroom UI. They must be instantly
  distinguishable by color family (plum / ink / slate) and silhouette at 16 px.
- `ic_sync`: must be visually distinct from `ic_host_sharing` (Phase 16). Sync
  is a user-initiated action on private state; sharing is the Host broadcast.
  Sage green for sync connotes "this is safe and good."
- `ic_offline`: the offline chip must not use red (reserved for errors/R1 risks
  in Phase 19). Clay/terracotta signals "degraded but not broken."
- No icon is the sole carrier of state: all chips pair icon + text label.

---

## Owner procurement request

> **Peter — action required before Phase 17 UI work (WP9) ships:**
>
> Please procure the following **8 premium PNG icon set** entries for the
> Client / Classroom mode UI. Same vendor family as Phases 03 and 16.
>
> **Icon keys to procure:**
> `ic_connect_to_library`, `ic_mode_standalone`, `ic_mode_classroom`,
> `ic_profile_student`, `ic_profile_teacher`, `ic_profile_guest`,
> `ic_sync`, `ic_offline`
>
> **Required sizes:** 16 px, 24 px, 32 px, 48 px — @1x, @2x, @3x density.
>
> **Required variants:** light + dark theme.
>
> **License requirement:** redistribution in signed desktop app + Mac App Store
> + Microsoft Store permitted.
>
> **Delivery path:** `OgmaLibrary.App/Assets/icons/classroom/<icon_key>@Nx.png`
>
> **Accessibility note:** the three profile-role icons (`student`, `teacher`,
> `guest`) must be distinguishable by shape/silhouette alone, not only by color,
> because they appear at 16 px in the profile chip and must pass WCAG 2.2 AA
> non-text contrast.
