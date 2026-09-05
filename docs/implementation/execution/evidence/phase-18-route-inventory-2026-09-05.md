# Phase 18 Route Inventory

Date: 2026-09-05

## Scope and authority

This inventory covers the desktop application shell routes represented by
`MainShellViewModel.ActiveView` and `CatalogueShellView`. It is a static
implementation inventory, not a substitute for keyboard, screen-reader, or
cross-platform UI acceptance.

## Route map

| Route | State | View binding | Entry points | Local proof |
| --- | --- | --- | --- | --- |
| Catalogue | `ShellView.Catalogue` | Grid, list, or directory catalogue panels | Initial shell state; Library command/button; Escape/return flows | Catalogue grid/list/directory render tests; shell navigation tests |
| Reader | `ShellView.Reader` | `reader:ReaderView` with `MainShellViewModel.Reader` | Open from book detail, search, citations, direct PDF; Library button | `ShellReaderNavigationTests`; `ReaderViewRenderTests` |
| Split view | `ShellView.SplitView` | `reader:SplitViewView` with `SplitView` | Shell Split View button; command palette `split-view` | `ShellReaderNavigationTests`; split-view tests |
| Sharing settings | `ShellView.SharingSettings` | `settings:SharingSettingsView` with `HostSharing` | Shell sharing-settings button; host settings flow | shell sharing navigation tests; sharing render tests |
| Student Smart Search | `ShellView.StudentSmartSearch` | `classroom:StudentSmartSearchView` with `StudentSmartSearch` | Shell classroom button; classroom capability-gated visibility | Student Smart Search view-model and render coverage |
| Advisor | `ShellView.Advisor` | `ai:RecommendationPanelView` with `Advisor` | Shell Advisor button; command palette `advisor`; citation navigation | `AdvisorViewRenderTests`; Advisor view-model tests |
| Reading plan | `ShellView.ReadingPlan` | `ai:ReadingPlanView` with `ReadingPlan` | Shell Reading Plan button; command palette `reading-plan` | `AdvisorViewRenderTests`; reading-plan tests |
| 3D bookshelf | `ShellView.Bookshelf3D` | `shelf3d:Bookshelf3DView` with `Bookshelf3D` | Capability-gated shell button and `OpenBookshelf3D` | `Bookshelf3DViewRenderTests`; bookshelf view-model tests |

## State and binding checks

- `ActiveView` raises change notifications for all eight route predicates:
  `IsCatalogueActive`, `IsReaderActive`, `IsSplitViewActive`,
  `IsSharingSettingsActive`, `IsStudentSmartSearchActive`, `IsAdvisorActive`,
  `IsReadingPlanActive`, and `IsBookshelf3DActive`.
- `CatalogueShellView` renders one route panel at a time through those
  predicates. The catalogue subviews are further selected by the catalogue
  view mode, while the reader and split-view routes own their reader view-model
  instances.
- Optional routes are null-safe and capability-gated in the shell view model;
  the corresponding controls and panels do not assume optional services exist.
- `OpenReaderAsync` switches the route before the reader warm-up completes and
  returns to the catalogue through the shared `ReturnToLibraryAsync` path.
- The command palette is wired to library, search, split-view, advisor,
  reading-plan, theme, and density actions. Search also remains reachable by
  Ctrl+F/Ctrl+K compatibility handling in the shell.

## Evidence references

- `src/OgmaLibrary.App/ViewModels/Catalogue/MainShellViewModel.cs`
- `src/OgmaLibrary.App/Views/Catalogue/CatalogueShellView.axaml`
- `src/OgmaLibrary.App/Views/Catalogue/CatalogueShellView.axaml.cs`
- `src/OgmaLibrary.App/Composition/ShellModule.cs`
- `tests/OgmaLibrary.Tests.Ui/ShellReaderNavigationTests.cs`
- `tests/OgmaLibrary.Tests.Ui/AdvisorViewRenderTests.cs`
- `tests/OgmaLibrary.Tests.Ui/Bookshelf3DViewRenderTests.cs`
- `tests/OgmaLibrary.Tests.Ui/CatalogueDirectoryViewRenderTests.cs`
- `docs/implementation/execution/evidence/phase-18-ai-accessibility-copy-2026-09-05.md`

## Gate disposition

Closed locally: a route inventory exists, route predicates are explicit, view
bindings are identifiable, optional routes are guarded, and automated evidence
references each major route family.

Still open: physical keyboard traversal, Narrator/VoiceOver journeys,
contrast snapshots across light/dark themes, and complete application-wide
literal-copy extraction. These require further implementation and/or physical
test evidence before Phase 18 can close.
