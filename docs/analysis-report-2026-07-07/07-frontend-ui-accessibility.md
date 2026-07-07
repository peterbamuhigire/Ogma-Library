# Frontend UI, UX, and Accessibility

Score: **50 / 100**. Weight: 12%.

Coverage reviewed: Avalonia shell, catalogue, reader, search, AI, classroom, settings, 3D views, theme tokens, icon catalog, localization resources, UI render tests, and design skills.

| ID | Location | Rule violated | Severity | Evidence | Consequence |
| --- | --- | --- | --- | --- | --- |
| F-UI-001 | `docs/plans/grand-plan/phase-10/icons.md:96`, `docs/plans/grand-plan/phase-21/icons.md:83` | Premium UI release gates cannot ship placeholder icons. | High | Multiple docs state placeholder icons are release blockers. | UI does not meet premium/product-quality bar. |
| F-UI-002 | `src/OgmaLibrary.App/Views/Classroom/StudentSmartSearchView.axaml:60`, `:107`, `:121`, `:249`; `src/OgmaLibrary.App/Views/Catalogue/BookDetailView.axaml:250`, `:269`, `:337` | All user-facing copy must be localized and resource-keyed. | Medium | Hard-coded English strings remain in XAML. | Localization and pseudolocale guarantees are incomplete. |
| F-UI-003 | `src/OgmaLibrary.App/OgmaLibrary.App.csproj:27`, `src/OgmaLibrary.App/Program.cs:23`, `src/OgmaLibrary.App/App.axaml:7` | Premium typography and visual language must be intentional, not default toolkit feel. | Medium | FluentTheme + Inter are in use; design tokens exist, but premium audit evidence is incomplete. | The application risks looking like a themed default desktop app rather than a refined library tool. |
| F-UI-004 | `docs/qa/PHASE-09-A11Y-SIGNOFF.md:75`, `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | WCAG 2.2 AA requires formal manual/platform evidence. | High | Manual visual accessibility exists for Phase 09, but release docs keep full WCAG/localization gates open for Phase 21. | Keyboard/screen-reader users cannot rely on release compliance. |
| F-UI-005 | `src/OgmaLibrary.App/Views/Reader/ReaderView.axaml:39`, `:42`, `:51`, `:315` | Familiar icon controls should use proper icon assets with accessible names, not text glyph fallbacks. | Medium | Reader still contains text glyphs such as `<`, `>`, `x` alongside labels. | Toolbar polish and assistive clarity are inconsistent. |

Strengths: many views bind `AutomationProperties.Name`, theme tokens exist, and reader UI has broad render-test coverage.

90%+ means all visible copy is localized in release languages, placeholder icons are gone or out of scope, WCAG 2.2 AA passes on Windows/macOS with Narrator/VoiceOver evidence, and premium UI screenshots pass typography/spacing/state review.
