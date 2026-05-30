# ADR-0002: Adopt Avalonia as the Cross-Platform Desktop Shell

## Status

Accepted

> Ratified in Phase 00 by the project owner, 2026-05-30.

## Date

2026-05-30

## Context

Ogma Library targets Windows and macOS as first-class desktop platforms and must feel native, calm, and refined on each, in line with the "premium means calm control" product principle. The application is a managed-library experience — shelves, grid, list, directory, reader, metadata editing, search, and an AI advisor — that needs a mature, controllable widget toolkit, virtualised lists for large collections, and full keyboard and screen-reader operability to meet the WCAG 2.2 Level AA release gate. One surface, the immersive 3D shelf, requires a hardware-accelerated rendering path that no general-purpose desktop toolkit provides natively; that surface is addressed separately in ADR-0003. The shell choice must therefore deliver a native-feeling chrome for the bulk of the application while admitting a hosted web surface for the 3D shelf alone.

## Decision Drivers

- **Cross-platform parity** on Windows and macOS from a single C# and .NET 10 codebase.
- **Native-feeling, themeable chrome** and virtualised collection views for large libraries.
- **Keyboard and screen-reader accessibility** sufficient for a WCAG 2.2 AA release gate.
- **Ability to host a web surface** for the 3D shelf without adopting a web stack for the whole shell.
- **Team skill alignment** with C# and .NET rather than a second UI language.

## Considered Options

### Option A — Avalonia

- **Pros:** single C# and XAML codebase across Windows and macOS (and Linux as a bonus); mature theming and control set; supports virtualised lists; integrates a native WebView surface for the 3D shelf; aligns with the .NET 10 runtime decision and the team's C# skills.
- **Cons:** smaller ecosystem than Windows-only stacks; some platform-native conveniences must be themed rather than inherited; macOS WebView behaviour must be validated (carried into ADR-0003).

### Option B — Per-platform native (WinUI on Windows, SwiftUI/AppKit on macOS)

- **Pros:** maximal native fidelity and accessibility per platform.
- **Cons:** two UI codebases in two languages double the build, test, and maintenance cost for a small team; feature parity drifts; shared catalogue, reader, and AI logic must straddle a language boundary.

### Option C — Electron (web stack for the entire shell)

- **Pros:** one web codebase; the 3D shelf is trivial inside a browser engine.
- **Cons:** heavyweight memory and startup footprint conflicts with the cold-start and calm-control budgets; weaker native feel; a JavaScript/TypeScript shell diverges from the C# and .NET 10 core, splitting the codebase and the team's skills.

### Option D — MAUI

- **Pros:** first-party .NET cross-platform framework.
- **Cons:** desktop maturity and control breadth lag Avalonia for a dense desktop library application; macOS desktop story is less proven for this class of UI.

## Decision Outcome

Adopt Avalonia as the desktop shell for Windows and macOS, written in C# on .NET 10 (ADR-0001). The shell renders all native chrome — navigation, shelves, metadata, grid, list, directory, reader controls, search, settings, and the AI advisor — using Avalonia controls. A hosted web surface is used only for the immersive 3D shelf, scoped and spike-gated in ADR-0003. Accessibility is treated as a shell responsibility: keyboard operability and screen-reader semantics are validated against the WCAG 2.2 AA gate across both platforms.

## Consequences

### Positive

- One C# and .NET 10 codebase serves both desktop platforms, matching the team's skills and minimising maintenance for a small team.
- The shell stays native-feeling while still permitting a single embedded web surface for the 3D shelf, avoiding a full web-stack adoption.

### Negative

- macOS accessibility and WebView behaviour must be explicitly validated rather than assumed; this validation is a Phase 0 and Phase 1 task.
- Where a platform-native idiom is expected, Avalonia theming must reproduce it deliberately.

### Affects

- ADR-0003 (the 3D shelf is hosted inside the Avalonia WebView surface); the WCAG 2.2 AA release gate; the packaging pipeline (ADR-0009).
