# Phase 07 IA map and wireframes

Status: **approved by owner proxy** (EXECUTION-LOG authority, 2026-09-25; decision D-13 in the
completion record). Source: the Phase 07 plan, the design engine's
`navigation-and-information-architecture` skill (hybrid scheme: task destinations plus a faceted
catalogue plus search) and the Sept-23 UI/UX audit.

## 1. Inventory of former entry points (before, `6e84a18`)

| Entry | Where | Target | Capability dependency | Fate |
|---|---|---|---|---|
| Sidebar toggle | toolbar | shelf sidebar | none | replaced by the rail collapse toggle |
| Library | toolbar | catalogue | none | rail: Library (Ctrl+1) |
| Choose library folder | toolbar | folder picker | none | Library toolbar: Add folder (pinned) |
| Open PDF | toolbar | file picker | none | Library More menu; empty state; Ctrl+O |
| Grid / List / Directory | toolbar | catalogue view | none | Library toolbar view switcher |
| 3D shelf | toolbar (disabled look) | 3D route | `OGMA_ENABLE_3D_SHELF` | view switcher, **shown only when the capability is on** |
| Search | toolbar | stacked panel | none | rail: Search (Ctrl+2, Ctrl+F) |
| AI Smart Search | toolbar | classroom search | classroom client | Classroom destination, **only when connected** |
| Advisor | toolbar | advisor | AI provider | rail: Advisor (Ctrl+4) with a set-up state and a route to Settings |
| Reading plan | toolbar | reading plan | AI provider | Advisor tab |
| Filter | toolbar | stacked panel | none | Library drawer + filter chips |
| Index Manager | toolbar | stacked panel | none | rail: Activity (Ctrl+6) |
| More → Split view | stopgap menu | split reader | none | Reading toolbar; palette |
| More → Sharing | stopgap menu | blank page without env flag | classroom Host | Classroom destination, **only when the Host capability is on** |
| More → Relocation reviews | stopgap menu | overlay panel | none | Library drawer (More menu) |
| Book count | toolbar | text | none | Library toolbar (lowest priority) |
| Shelves list + create/rename/delete | always-open sidebar | shelf filter (wired to a second filter instance, so it did nothing) | none | rail: Collections (Ctrl+5); "Show books in Library" applies a visible chip |
| Library folders + Needs attention | sidebar bottom | folder management | none | Library drawer (Folders); Phase 08 moves it into Settings |
| Back to Library | reader strip | closes the book | reader | Reading toolbar |
| Three `IsVisible="False"` Host panels | shell | nothing | — | deleted with their handlers |
| Palette (7 commands) | Ctrl+Shift+P | hand-kept list | — | registry of 30 commands, fuzzy, recents, shortcuts |
| `MainWindow` | dead window | — | — | deleted |

## 2. Destinations (rail order) and their tools

| # | Destination | Routes | Toolbar (≤ 7 visible, priority order) | Overflow (More) | Hidden when |
|---|---|---|---|---|---|
| 1 | Library | `Library`, `Shelf3D` | Add folder (pinned), Filter, view switcher, Rescan, Sort, Folders, count | Open PDF, Relocation reviews, Export diagnostics, anything that does not fit | never |
| 2 | Search | `Search` | — | — | never |
| 3 | Reading | `Reader(book,page)`, `SplitView` | Back to Library, Split view | — | never (empty state reopens the last book) |
| 4 | Advisor | `Advisor`, `ReadingPlan` | Ask \| Reading plan | — | never; explains and routes to Settings when AI is not set up |
| 5 | Collections | `Collections` | Show books in Library | — | never |
| 6 | Activity | `Activity` | (Index Manager's own actions) | — | never |
| 7 | Settings | `Settings(section)` | — | — | never (Phase 08 fills it; lists every capability now) |
| 8 | Classroom | `Classroom(section)` | Sharing \| Smart search (both present only) | — | Host capability off **and** no client connection |

Drawers (Library only, exactly one): Filter, Folders, Relocation reviews. They close on Escape,
on their close button and on any navigation. Active filters always show as removable chips above
the catalogue (K12).

## 3. Responsive rules

| Width | Rail | Inspector | Toolbar |
|---|---|---|---|
| < 1100 px | icons only (64 px), labels as tooltips and UIA names | overlays the content | priority overflow into More |
| 1100–1279 px | icons + labels (208 px) | overlays the content | priority overflow |
| ≥ 1280 px | icons + labels | own column | all items at ≥ 1920 px |

The user can pin the rail collapsed or expanded (toggle at the rail's foot, or palette).

## 4. Wireframes (low fidelity)

860 px (rail collapsed; Sort, Folders and count in More):

```
┌──┬─────────────────────────────────────────────────────────┐
│▣ │ Library  [Add folder] [Filter] [Grid|List|Dir] [Rescan] [More▾]│
│⌕ ├─────────────────────────────────────────────────────────┤
│▤ │ (Title: foo ×) (Clear filters)            ← only if filters│
│✦ ├──────────────────────────────────────┬──────────────────┤
│▦ │  catalogue grid                      │ drawer (340 px)   │
│◷ │                                      │ one of Filter /   │
│  │                                      │ Folders / Reviews │
│⚙ │                                      │                   │
│» │  pager (only when > 1 page)          │                   │
├──┴──────────────────────────────────────┴──────────────────┤
│ status                                                     │
└────────────────────────────────────────────────────────────┘
```

1280 px (rail with labels; inspector in its own column):

```
┌────────────┬───────────────────────────────────────────────┬──────────┐
│ Library    │ Library [Add folder][Filter][Grid|List|Dir]    │ inspector│
│ Search     │         [Rescan][Sort ▾ ↕][Folders]   [More ▾] │ (when a  │
│ Reading    ├───────────────────────────────────────────────┤ book is  │
│ Advisor    │  catalogue                                    │ selected)│
│ Collections│                                               │          │
│ Activity   │                                               │          │
│ Settings   │                                               │          │
│ « Collapse │                                               │          │
├────────────┴───────────────────────────────────────────────┴──────────┤
│ status                                                                │
└───────────────────────────────────────────────────────────────────────┘
```

1920 px: as 1280 px, with the book count visible in the toolbar and wider content.

## 5. Keyboard map

| Keys (Cmd on macOS) | Action |
|---|---|
| Ctrl+1 … Ctrl+7 | Library, Search, Reading, Advisor, Collections, Activity, Settings |
| Ctrl+8 | Classroom (when available) |
| Ctrl+K, Ctrl+Shift+P | Command palette |
| Ctrl+F | Search |
| Ctrl+O | Open a PDF |
| F5 | Rescan the library |
| Alt+Left / Alt+Right, mouse buttons 4/5 | Back / Forward |
| Esc | Close the drawer, the palette or the shortcuts sheet; leave Search |
| Ctrl+/ | Keyboard shortcuts sheet |
| Up / Down / Home / End in the rail | Move between destinations |

Focus order: rail → destination toolbar → content → inspector → status bar (headless test).
