# Design System Roadmap

> Part of the canonical [August 39-phase desktop roadmap](../README.md).

## Direction

Ogma should feel literary, calm and crafted without sacrificing information density or platform conventions. The latest v2.1 direction uses Spectral for literary display, Public Sans for interface text and JetBrains Mono for identifiers/paths, subject to bundled-font license verification. Existing Inter/Fluent-only styling, hard-coded colors, emoji/mojibake icons and stale phase labels are migration inputs, not the target.

| Component/system | Phase | Required states and behavior |
| --- | ---: | --- |
| Tokens/typography/color/spacing/elevation/motion | 18 | light/dark/high-contrast, DPI, reduced motion, contrast and packaged-font fallback |
| Shell/navigation/command palette | 18 | first run, normal, scan active, degraded/offline, update and fatal recovery |
| Buttons/forms/dialogs/notifications/icons | 18 | keyboard/focus/error/disabled/progress; licensed SVG resources, no emoji controls |
| Settings/privacy/activity | 17–18, 27 | roots, processing, providers, privacy, storage, themes, diagnostics and classroom |
| Cover/book card/list row/directory node | 16, 19 | loading, missing, corrupt, unavailable, review-needed, selected and processing |
| Search/filter/sort/typeahead | 19, 22–23 | empty/no-result/error/rebuild, exact/fuzzy/full-text/semantic distinction |
| Book detail | 14, 20 | identity, provenance, files, organisation, metadata edit, safe actions, related |
| Metadata editor/review | 14–15 | source/confidence/lock, alternatives, batch preview, consent, undo/restore |
| Reader | 21 | navigation, zoom/layout/fullscreen, split, panels, password, cached/loading/error |
| Advisor request/recommendation/evidence | 27–30 | tier/provider, interpreting/retrieving/generating, trade-offs, citations, abstention, history |
| 3D controls/fallback | 31–33 | load/crash/GPU degraded, camera/keyboard/focus, reduced motion, instant 2D parity |
| Classroom host/client/admin | 34–36 | opt-in disclosure, pairing/trust, offline/sync, roles/quotas/audit/minors |
| Update/release/support | 38–39 | available/download/verify/install/restart/rollback/error and support bundle |

## Accessibility contract

- Every 3D action has an equivalent 2D route and semantic command.
- Full keyboard operation, visible focus, logical reading order and accessible names are automated where possible and physically verified with Narrator and VoiceOver.
- Reduced motion affects camera transitions and animated processing states without hiding information.
- Cover/spine text has accessible metadata alternatives; book identity never depends on color/cover alone.
- Contrast is verified for all state colors; high contrast and 200% scaling remain usable.
- Validation and failure messages identify the field/action and recovery, not just a color.

## Visual QA

Headless screenshots remain useful regression artifacts but do not prove platform fidelity. Phase 18 establishes component snapshots at supported themes, languages and DPI. Phases 19–36 add workflow snapshots. Phase 39 requires physical Windows/macOS review, typography/font packaging, localisation truncation, keyboard/AT and premium-quality owner acceptance.

## Localisation

Extract every user-visible string to resources, add pseudo-localisation and missing-key gates, complete en/fr for the current release surface, then satisfy the v2.1 es/it/de final tier before Phase 39 unless an approved SRS change defers them. Never mix internal job codes with user messages.


