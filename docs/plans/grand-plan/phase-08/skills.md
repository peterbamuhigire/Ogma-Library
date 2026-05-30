# Phase 08 — Skills

Skills and slash commands to invoke during this phase, keyed to concrete tasks.
"Decorative" skill mentions are excluded per CONVENTIONS.md.

---

## Always-on (apply to every work package)

| Skill / command | When to invoke | Expected artifact |
| --- | --- | --- |
| `superpowers:test-driven-development` | Before writing any production class — write the contract test first | Test file committed before implementation |
| `superpowers:verification-before-completion` | Before marking any task or WP done | CI passes; benchmark green; checklist ticked |
| `superpowers:requesting-code-review` + `/code-review` | After each WP; escalate to `high` for WP1 (native interop) and WP2 (cache) | Code-review findings resolved |
| `superpowers:systematic-debugging` | Any failing test or performance regression | Root-cause note in the task comment |
| `superpowers:using-git-worktrees` | One worktree per WP: `feature/P08-WP1-pdfium-adapter`, etc. | Clean branch per WP, no mixed concerns |
| `documentation-generation:docs-architect` | After WP1 (ADR-0004 amendment) and after WP7 (developer guide text-layer section) | Updated ADR and developer guide |

---

## WP1 — PDFium Adapter

| Skill | Task | Artifact |
| --- | --- | --- |
| `language-standards` (C# / .NET 10) | P08-WP1-T3, P08-WP1-T4 — `SafeHandle`-guarded P/Invoke; `NativeLibrary.Load`; `unsafe` bitmap span handling; `IDisposable` finalizer pattern | Correct, idiomatic native-interop C# |
| `avalonia-desktop-development` (see `_reference/AVALONIA-STANDARDS.md`) | P08-WP1-T3, P08-WP1-T5 — thread-safety for Avalonia dispatcher; bitmap type selection (`WriteableBitmap` vs `Bitmap`) | Platform-correct bitmap handling |
| `superpowers:brainstorming` | Before P08-WP1-T3 — evaluate memory model for large-page rendering (streaming vs. full decode) | Design decision recorded in code comment |
| `documentation-generation:architecture-decision-records` | P08-WP1-T7 | ADR-0004 amendment committed |

---

## WP2 — Page-Render Cache

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:frontend-performance` | P08-WP2-T1 through T4 — benchmark-driven cache sizing; latency instrumentation; cancellation token discipline | Cache design meeting ≤ 100 ms P95 |
| `superpowers:brainstorming` | Before P08-WP2-T1 — decide between `MemoryCache`, `ConcurrentDictionary` + manual tracking, or custom ring-buffer | Design decision documented |
| `devops-cloud:reliability-engineering` | P08-WP2-T3 — cancellation-token discipline; no orphaned background tasks on navigation | Cancellation correctness |

---

## WP3 — Session & Reading Progress

| Skill | Task | Artifact |
| --- | --- | --- |
| `backend-databases:database-reliability` | P08-WP3-T2 — debounce write, avoid partial writes; SQLite WAL considerations | Durable progress writes |
| `avalonia-desktop-development` | P08-WP3-T5 — `IObservable<ReaderEvent>` integration with Avalonia reactive bindings | `IReaderSessionReadModel` wired to ViewModel |

---

## WP4 — Navigation

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:interaction-design-patterns` | P08-WP4-T2, T3 — navigation history depth, jump-to-page validation UX, keyboard shortcut map | Navigation UX spec (inline design note) |
| `avalonia-desktop-development` | P08-WP4-T4, T5 — `KeyBinding` in XAML; Automation peers for page-number announcements | Keyboard bindings file + Automation peers |

---

## WP5 — Zoom & Display Modes

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:frontend-performance` | P08-WP5-T5 — continuous-scroll virtualizing panel: render only visible pages; prevent jank during fast scroll | Non-blocking continuous scroll |
| `frontend-ux:interaction-design-patterns` | P08-WP5-T3, T4 — zoom increment/decrement UX; two-page spread even/odd alignment | Correct two-page spread logic |
| `avalonia-desktop-development` | P08-WP5-T5 — `VirtualizingPanel` or custom panel in Avalonia for continuous scroll | Panel implementation |

---

## WP6 — Full-Screen

| Skill | Task | Artifact |
| --- | --- | --- |
| `avalonia-desktop-development` | P08-WP6-T2, T3 — `WindowState.FullScreen`; macOS `NSWindow` interop if needed; title-bar chrome management | Cross-platform full-screen |
| `frontend-ux:motion-design` | P08-WP6-T2, T3 — enter/exit full-screen transition within calm design language (no jarring flash) | Smooth transition |

---

## WP7 — Text Layer & In-Document Search

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:interaction-design-patterns` | P08-WP7-T5, T6 — search panel layout; match cycling UX; Ctrl+F / F3 / Escape interaction model | Search panel UX |
| `frontend-ux:frontend-performance` | P08-WP7-T3 — async incremental search across pages; debounce query input; progress indicator for large documents | Non-blocking search |
| `language-standards` | P08-WP7-T1, T2 — PdfPig integration; sidecar JSON serialization format for text cache | Text-layer cache design |

---

## WP8 — UI, Icons, i18n, Accessibility

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:practical-ui-design` | P08-WP8-T2, T4 — reader toolbar layout; icon placement; two-page spread layout; focus ring visibility | Polished reader toolbar |
| `avalonia-desktop-development` | P08-WP8-T3 — Avalonia `AutomationPeer` implementation for `ReaderView` | Automation peers |
| `frontend-ux:ux-content-strategy` | P08-WP8-T1 — source `en` copy for tooltips, empty states, error messages; tone matches PRD "calm control" | `reader.en.resx` with review-ready copy |
| Content translation (native speaker) | P08-WP8-T1 — `fr` translation of all reader strings | `reader.fr.resx` complete |

---

## WP9 — Tests & Benchmarks

| Skill | Task | Artifact |
| --- | --- | --- |
| `sdlc-meta:advanced-testing-strategy` | P08-WP9-T1 through T5 — architecture tests; golden-corpus fixture design; memory-leak test pattern | Complete test plan as authored in `testing.md` |
| `full-stack-orchestration:performance-engineer` | P08-WP9-T2 — BenchmarkDotNet setup; P95 latency measurement; CI threshold assertion | `ReaderBenchmarks.cs` with CI gate |
| `/run` + `/verify` | P08-WP9-T5 — drive the app on Windows and macOS; observe open/navigate/search flow | Verified screenshot / session log |
| `comprehensive-review:full-review` | End of phase | Final review report; all findings resolved |
