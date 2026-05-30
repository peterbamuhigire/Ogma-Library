# Phase 09 — Skills

Skills and slash commands for Annotations, Bookmarks & Reading Memory.

---

## Always-on

| Skill / command | When | Artifact |
| --- | --- | --- |
| `superpowers:test-driven-development` | Write fault-injection test stubs (P09-WP1-T6) before any production write code | Red tests committed first |
| `superpowers:verification-before-completion` | After each WP; final phase gate | CI green; fault-injection green |
| `superpowers:requesting-code-review` + `/code-review` | After WP2 (rotation logic), WP9 (fault injection) | Findings resolved |
| `superpowers:systematic-debugging` | Any bounding-box regression on rotated pages | Root-cause note |
| `superpowers:using-git-worktrees` | One worktree per WP | Clean branches |

---

## WP1 — DB Schema & Durable Write

| Skill | Task | Artifact |
| --- | --- | --- |
| `backend-databases:database-reliability` | P09-WP1-T4, T5 — durable transaction pattern; WAL confirmation; idempotent migration | Production durable-write implementation |
| `backend-databases:database-design-engineering` | P09-WP1-T1, T2 — schema design; FK cascade rules; normalized-region JSON choice | Well-formed migrations |
| `sdlc-meta:advanced-testing-strategy` | P09-WP1-T6 — R1 fault-injection test design: what constitutes "consistent catalogue" as oracle | Fault-injection test stubs |

---

## WP2 — Highlight & Note Engine

| Skill | Task | Artifact |
| --- | --- | --- |
| `superpowers:brainstorming` | Before P09-WP2-T1 — evaluate coordinate systems: normalized vs. absolute pixel vs. PDFium point units; decide on `[0,1]` normalized | Design decision in code comments |
| `language-standards` (C# / .NET 10) | P09-WP2-T6 — rotation-matrix arithmetic in `AnnotationRenderHelper`; no floating-point accumulation error across double-precision | Correct rotation helper |
| `avalonia-desktop-development` | P09-WP2-T6 — map normalized coordinates to Avalonia `Rect` in the overlay panel | Correct overlay positioning |

---

## WP3 — Annotation Overlay UI

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:interaction-design-patterns` | P09-WP3-T2, T3 — text-selection UX: context-menu trigger, note pop-over dismiss/auto-save; drag-to-select behavior | Selection UX specification (inline notes) |
| `avalonia-desktop-development` | P09-WP3-T1 — custom `Control` overlay drawing on a `Canvas` above the page bitmap; `InvalidateVisual` dirty-flag pattern | `AnnotationOverlayPanel` implementation |
| `frontend-ux:frontend-performance` | P09-WP3-T6 — overlay invalidation strategy; benchmark ≤ 10 ms overhead | Performance assertion passing |
| `frontend-ux:motion-design` | P09-WP3-T3 — note pop-over appear/dismiss animation within calm design language | Smooth pop-over |

---

## WP4 — Annotation Layers

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:interaction-design-patterns` | P09-WP4-T2 — layer sidebar layout; visibility toggle; drag-to-reorder | Layer sidebar design |
| `superpowers:brainstorming` | Before P09-WP4-T1 — layer-delete semantics: "move orphans to default" vs. "cascade delete"; decide before implementing | Confirmed semantics in code |

---

## WP5 — Bookmarks

| Skill | Task | Artifact |
| --- | --- | --- |
| `backend-databases:database-reliability` | P09-WP5-T6 — bookmark fault-injection test | Fault test green |
| `avalonia-desktop-development` | P09-WP5-T4 — sortable bookmark list panel; `KeyBinding` Ctrl+B | Panel implementation |

---

## WP6 — Citation Cards

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:ux-content-strategy` | P09-WP6-T3 — citation export format copy; plain-text template wording | Export template in `en` + `fr` |
| `frontend-ux:interaction-design-patterns` | P09-WP6-T2 — citation card modal design; one-action capture flow | Citation card UI |

---

## WP7 — Reading Memory

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:practical-ui-design` | P09-WP7-T2 — structured journal panel layout; field labels; disposition widget | Reading memory panel |
| `product-business:premium-product-positioning` | P09-WP7-T2 — copy for field labels communicates value (e.g. "Why did you open this book?") — consult `ux-content-strategy` | Compelling field labels in `en.resx` |

---

## WP8 — i18n & Accessibility

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:ux-content-strategy` | P09-WP8-T1 — source `en` copy for annotation UI: tooltip wording, confirmation dialogs | `annotations.en.resx` |
| Content translation (native speaker) | P09-WP8-T1 — `fr` translation | `annotations.fr.resx` |
| `avalonia-desktop-development` | P09-WP8-T3 — Automation peers for overlay, layer sidebar, bookmark panel | Accessibility peers |

---

## WP9 — Tests & Fault Injection

| Skill | Task | Artifact |
| --- | --- | --- |
| `sdlc-meta:advanced-testing-strategy` | P09-WP9-T2 — R1 fault-injection suite design; mock filesystem for disk-full | Fault-injection suite green |
| `full-stack-orchestration:performance-engineer` | P09-WP9-T3 — regression benchmark: 100-annotation page-turn stays ≤ 100 ms | Benchmark assertion |
| `/run` + `/verify` | P09-WP9-T5 — drive the app; observe annotations survive restart | Verified session log |
| `comprehensive-review:full-review` | End of phase | Final review report |
