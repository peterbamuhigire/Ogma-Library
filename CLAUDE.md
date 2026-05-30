# CLAUDE.md

Guidance for AI coding agents working in this repository.

## What this is

**Ogma Library** — a local-first, cross-platform (Windows + macOS) desktop PDF
library application on **.NET 10 LTS + Avalonia**. It turns a folder of PDFs into
a managed, searchable, beautiful library, with an optional explainable AI advisor
and (post-MVP) a LAN/classroom mode. Owner: Chwezi Core Systems / Peter
Bamuhigire.

## Read first

| Need | Path |
| --- | --- |
| The full 24-phase plan | `docs/plans/grand-plan/README.md` |
| Canonical requirements digest | `docs/plans/grand-plan/SOURCE-SUMMARY.md` |
| Owner decisions (binding) | `docs/plans/grand-plan/DECISIONS.md` |
| Architecture decisions | `docs/adrs/` |
| Architecture (signed) | `docs/references` (HLD), summarized in SOURCE-SUMMARY |
| Avalonia standards | `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` |
| Developer guide | `docs/developer-guide/README.md` |
| Contributing & governance | `CONTRIBUTING.md`, `docs/governance/` |

## Build / test / format

```bash
dotnet restore OgmaLibrary.sln
dotnet build   OgmaLibrary.sln -c Release      # warnings are errors
dotnet test    OgmaLibrary.sln -c Release
dotnet format  OgmaLibrary.sln --verify-no-changes
dotnet run     --project src/OgmaLibrary.App   # the running skeleton window
```

## Architecture rules (enforced by tests — do not break)

- 9 projects; dependencies point **inward**. `Domain` depends on nothing.
- Only `OgmaLibrary.App` (the `CompositionRoot`) binds implementations to
  interfaces.
- All off-device/HTTP access goes through `Infrastructure` adapters — the single
  egress chokepoint (the AI gateway). `Domain`/`Application` make no HTTP calls.
- The SQLite catalogue is the single source of truth for book identity; other
  contexts read projections, never own identity.
- `OgmaLibrary.Tests.Architecture` enforces the above; it runs in CI.

## Conventions

- C#: Nullable on, async + `CancellationToken` + `ConfigureAwait(false)` in
  libraries, `_camelCase` private fields, XML docs on public library members.
- **No hard-coded UI strings** — use `ILocalizationService` (en/fr now;
  es/it/de later). **No secrets** in the repo (OS credential store only).
- Every button/menu gets a **flat full-color icon** (Flaticon, SVG+PNG) — see
  `docs/plans/grand-plan/ICON-SYSTEM.md`. Always ask the owner to procure icons.
- Commits: Conventional Commits + DCO sign-off (`git commit -s`). Branch from
  `develop` as `feature/<id>-<slug>`. CI must be green on Windows **and** macOS.
- Throwaway experiments live in `spikes/` (excluded from the product solution).

## Current status

Phases 00–02 implemented (inception, spikes, scaffolding + running localized
skeleton). Next: finish Phase 02 polish (OGMA0001 analyzer — `TRACK-P02-ANALYZER`)
then Phase 03 (design system + real Flaticon icons). See the grand-plan phase
folders for per-phase READMEs, tasks, skills, and tests.
