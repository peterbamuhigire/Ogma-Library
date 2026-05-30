# Phase 06 — Skills & Slash Commands

Phase-scoped detail for the catalogue browsing UI. Every skill is tied to a specific
work package and produces a real artifact.

---

## Always-on (inherited)

| Skill / command | When | Artifact |
| --- | --- | --- |
| `superpowers:brainstorming` | Before WP6 (shelf UX), WP7 (detail panel layout), WP9 (bulk-edit preview) — required before creative design decisions | Options explored, preferred approach documented before implementation |
| `superpowers:writing-plans` | Before WP1 | Ordered WP checklist with per-WP acceptance criteria |
| `superpowers:executing-plans` | Drive each WP | Completed, tested WP |
| `superpowers:test-driven-development` | Before each ViewModel (WP2-WP9) | Failing ViewModel unit test exists before implementing it |
| `superpowers:verification-before-completion` | Before marking each WP done | All tests green; build zero warnings; no hard-coded strings |
| `superpowers:requesting-code-review` + `/code-review` | After WP7 (book detail), WP9 (bulk edit), and before phase close | Review findings resolved; no R1 items |
| `superpowers:systematic-debugging` | Any failing test or performance regression | Root-cause documented before fix |
| `superpowers:using-git-worktrees` | Branch `feature/P06-catalogue-browsing` | Isolated branch; PR after DoD |

---

## Phase-06-specific skills

### `frontend-ux:practical-ui-design`
**When:** WP1 (shell layout), WP3 (list view), WP5 (filter panel), WP10 (settings).
**Task linkage:** P06-WP1-T1 (shell proportions), P06-WP5-T3 (filter chip UX),
P06-WP10-T1 (settings layout).
**Artifact:** Shell and filter panel layouts reviewed for usability; no cramped or
overlapping elements in the pseudolocale render.

### `frontend-ux:premium-ui-ux-design`
**When:** WP2 (grid cell design), WP7 (book-detail panel), empty states.
**Task linkage:** P06-WP2-T1 (cover + title + chip composition), P06-WP7-T2 (detail
panel, five tabs, star rating control).
**Artifact:** Grid cells and detail panel pass a "calm control" audit — refined, not
busy; appropriate use of whitespace; colorful icons enhance not distract.

### `frontend-ux:interaction-design-patterns`
**When:** WP4 (drag-and-drop shelf assignment), WP9 (multi-select, bulk-edit toolbar).
**Task linkage:** P06-WP6-T4 (drag-and-drop), P06-WP9-T1 (Ctrl+Click / Shift+Click
multi-select pattern).
**Artifact:** Drag-and-drop implementation matches the platform convention (Avalonia
DragDrop API); multi-select matches OS expectations for Ctrl/Shift click.

### `frontend-ux:frontend-performance`
**When:** WP2 (grid virtualization), WP3 (list virtualization), WP11 (benchmarks).
**Task linkage:** P06-WP2-T5 (60 FPS assertion), P06-WP3-T4 (list scroll stall test),
P06-WP11-T3 (frame rate fix if < 60 FPS).
**Artifact:** `AsyncCoverLoader` implementation reviewed for correct thread dispatch;
`ItemsRepeater` recycle configuration confirmed; frame-rate measurement method
documented.

### `frontend-ux:data-visualization`
**When:** WP5 (filter chip counts), WP6 (shelf book-count badge).
**Task linkage:** P06-WP5-T3 (tag multi-select with counts), P06-WP6-T3 (shelf count
badge).
**Artifact:** Badge and count displays correctly update reactively via MVVM binding;
localized number formatting (e.g., "1 247 livres" in fr-FR).

### `frontend-ux:design-audit`
**When:** WP12, after all icons are wired.
**Task linkage:** P06-WP12-T5 (design audit run).
**Artifact:** All icons consistent in style, color, and size with Phase 03 tokens;
no orphan icons without labels; palette audit pass documented.

### `avalonia-desktop-development` (reference `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md`)
**When:** Every WP that touches Avalonia XAML (WP2-WP10).
**Task linkage:** All `CatalogueGridView`, `CatalogueListView`, `BookDetailView`,
`ShelfSidebarView`, `BulkEditView` implementations.
**Artifact:** All views follow the MVVM conventions in `AVALONIA-STANDARDS.md`:
no code-behind logic; `DataTemplate` via `DataTemplateSelector`; `Binding` not
`x:Reference`; `Dispatcher.UIThread.Post` for cross-thread updates.

### `architecture:validation-contract`
**When:** WP8 (navigation contracts).
**Task linkage:** P06-WP8-T1..T4 (`IBookDetailNavigationService`, `IReaderNavigationService`).
**Artifact:** Both interfaces are stable contracts with sealed parameter types;
architecture test `AllViews_NavigateVia_IBookDetailNavigationService` passes.

### `comprehensive-review:full-review`
**When:** At phase close (end of WP12).
**Task linkage:** Full catalogue browsing UI reviewed holistically.
**Artifact:** Full review findings resolved; no outstanding design, accessibility, or
performance issues.

---

## Slash commands

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review` | After WP7 (detail panel) and WP9 (bulk edit); before phase close | Correctness + accessibility findings |
| `/code-review --effort high` | WP9 (undo/redo logic is subtle) | Deeper review of `ICommandHistory` correctness |
| `/simplify` | After WP6 (shelf) and WP9 (bulk edit) | Reduce any over-engineered shelf or command-history code |
| `/verify` | Before marking phase done | Confirm `dotnet build`, `dotnet test`, performance baselines all pass on both CI runners |
| `/init` | End of phase | Update `CLAUDE.md` with new ViewModels, navigation contracts, and write services |
