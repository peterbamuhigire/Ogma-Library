# CLAUDE.md

Guidance for AI coding agents working in this repository.

## Product and release scope

**Ogma Library** is a local-first Windows and macOS desktop PDF library built
with .NET 10 LTS and Avalonia. It manages, enriches and searches a user's own
PDF collection and provides an optional, grounded Reading Advisor. The approved
release is one C# desktop application with standalone, classroom Host and
classroom Client modes. Mobile, PWA and public-website delivery are excluded.

Owner: Chwezi Core Systems / Peter Bamuhigire.

## Authoritative sources

Read these in order when sources disagree:

| Authority | Path |
| --- | --- |
| Approved 39-phase execution roadmap | `docs/plans/aug-39/README.md` |
| Requirement-to-phase accountability | `docs/plans/aug-39/appendices/01-requirement-phase-matrix.md` |
| Canonical v2.1 requirements | `docs/references/Ogma-Library_SRS_v2.1_2026-08-13.docx` |
| Other approved SDLC references | `docs/references/` |
| Current implementation audit | `docs/audit/` |
| Execution status and phase evidence | `docs/implementation/execution/` |
| Architecture decisions | `docs/adrs/` |
| Developer guide | `docs/developer-guide/README.md` |
| Contribution and governance rules | `CONTRIBUTING.md`, `docs/governance/` |

Historical plans and phase labels are evidence of prior work, not current
completion authority. Do not infer release readiness from a class name, an old
phase comment, a historical test report or a document's internal v2.0 label.

## Build and verification

```powershell
./scripts/Test-RequirementAccountability.ps1
dotnet restore OgmaLibrary.sln --locked-mode
dotnet format OgmaLibrary.sln --verify-no-changes --no-restore
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet list OgmaLibrary.sln package --vulnerable --include-transitive
dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn --verbosity minimal
dotnet test OgmaLibrary.sln --configuration Release --no-build --verbosity normal -m:1
```

The 3D source also has its own locked Node.js gates in `src/shelf3d`:

```powershell
npm ci
npm run typecheck
npm run build
npm run perf:budget
```

Unavailable platform, signing, provider or physical-hardware gates must be
recorded as `NOT ASSESSED`; they are never silently treated as passing.

## Architecture rules

- Dependencies point inward; `OgmaLibrary.Domain` has no outward dependency.
- Only the app composition root binds runtime implementations.
- HTTP and off-device access stays behind Infrastructure adapters and approved
  gateways; Domain and Application do not initiate HTTP.
- The SQLite catalogue is authoritative for library identity. Search, reader,
  AI, classroom and 3D features consume contracts or projections and do not own
  competing book identity.
- Core catalogue, metadata, reader and ordinary search remain useful without AI
  or internet access.
- PDFs, filenames and paths are untrusted. Use the approved path and isolated
  processing boundaries; do not bypass them for convenience.
- Architecture tests in `OgmaLibrary.Tests.Architecture` are release gates.

## Engineering conventions

- C#: nullable enabled; async APIs accept `CancellationToken`; library awaits
  use `ConfigureAwait(false)`; private fields use `_camelCase`; public library
  members have XML documentation.
- Do not hard-code user-facing strings. Use localization resources and preserve
  the approved language and fallback behavior.
- Do not commit secrets or private book content. Credentials belong in OS-backed
  stores; evidence records identifiers and results, not extracted documents.
- Apply the approved Ogma design system and licensed asset policy. Do not add
  arbitrary one-off controls, placeholder icons or untraceable visual assets.
- Use Conventional Commits and DCO sign-off. Preserve user work and do not
  rewrite shared history.

## Current execution status

The authoritative roadmap contains exactly 39 phases. Phase claims are valid
only when their completion record and executable evidence exist under
`docs/implementation/execution/`. Consult `00-execution-status.md` before making
or repeating work.
