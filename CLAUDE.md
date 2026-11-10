# CLAUDE.md

<!-- MODEL-ROUTING-BLOCK v2 2026-11-10 -->
## Model classes and token discipline (Claude Code, Codex and any runner; mandatory)
Name a class here, never a model version. The owner's routing file maps classes to current models (Knowledge Bank, Development, Anthropic Models: `model-routing.json`; handoff AM-01), so a model upgrade needs no edit in this repository.
- **Classes:** **M Mundane** (fastest and cheapest; mechanical work with a clear right answer); **N Normal** (default; executes an approved plan); **X Expert** (strongest standard class; plans, judges, hard debugging, final review); **S Special occasion** (premium class above X; rare; only through the seven-test gate, AM-01 s5, including approved billing). A new model is classified by relative price and vendor positioning (AM-01 s1.1). With no S class available, X is the ceiling.
- **Escalate one class, on a trigger only:** two failed acceptance checks; an undecidable choice; unreviewed money, tenant, security or legal work. Drop back after the hard step.
- **Tokens, both runners:** start-up files at most 8,000 tokens and `AGENTS.md` at most 32 KiB (Codex stops at its byte cap); one task per session, clear or new thread between tasks, compact near 80%; plan to a file, execute in a fresh session (resume a plan from its small `RESUME.md` only); read the router section then one `SKILL.md`, search instead of loading; consult only the engine whose domain matches the task; subagents and tools return summaries, cap tool output (`tool_output_token_limit`), disable unused MCP tools; stable prefix first, no instruction edits mid-session; parallel agents only with a written budget; non-interactive runs for bulk (Batch API only with an approved API budget); effort medium by default, high needs a reason, the maximum needs approval.
- **Class S case for this repository (A-12, AM-02):** none; X decides architecture, N builds. Everything else: X plans, N executes, M for mechanical work.
- **Monthly Kaizen, first step:** Anthropic currency check (AM-01 s8) and the token-budget lint; record `Anthropic currency check YYYY-MM-DD: no change|changed`.
<!-- /MODEL-ROUTING-BLOCK -->


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
| **Execution sequence** (Sept-23 Kaizen, 29 phases; supersedes Aug-39 phase order, astra 21-phase and Sept-10 Kaizen as execution plans) | `docs/plans/sept-23-kaizen/README.md` |
| Approved 39-phase roadmap (requirement accountability) | `docs/plans/aug-39/README.md` |
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
- Optional capabilities (online metadata providers, 3D shelf, classroom Host)
  are user choices in Settings. `OGMA_ENABLE_*` environment variables are
  administrator/test overrides only (see `docs/developer-guide/README.md`).

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

Work proceeds in the order of the Sept-23 Kaizen plan
(`docs/plans/sept-23-kaizen/README.md` status register and `EXECUTION-LOG.md`).
The Aug-39 matrix remains the requirement accountability authority. Phase
claims are valid only when their completion record and executable evidence
exist under `docs/implementation/execution/`, and behaviour changes need
real-window proof (`docs/plans/sept-23-kaizen/AGENT_BRIEF.md`). Consult
`00-execution-status.md` before making or repeating work.

## Design standard (binding)

- **Standard:** Chwezi Product Excellence Standard. Adopted by Lead Developer Peter Bamuhigire and the Chwezi Core Systems design team, effective 01 November 2026 until further communication. It applies to all UI work in this repository (every screen, flow, component and release) and supersedes conflicting design guidance here from 01 November 2026.
- **Read first** (resolve engine locations through the global engine-routing table; do not hard-code paths):
  - design engine `chwezi-design-engine`: `doctrine/product-excellence-standard.md` (binding text) and the skill `chwezi-product-excellence-standard` (`skills/00-cross-cutting-ops-qa-a11y/`: rules catalogue, product profiles, screen blueprints, scorecard); visual tokens from `chwezi-business-app-house-style`.
  - dev engine `chwezi-dev-engine`: `frontend-architecture/references/product-excellence-engineering-gates.md`.
- **Product profile:** **Education/LMS** (personal PDF library with classroom Host/Client modes), adapted to desktop.
- **Most relevant screen blueprints:** S14 education course and exam hub; S11 document and contract management; S01 role-aware business dashboard.
- **Release gate:** 13 hard gates (PX-GATE-01 to PX-GATE-13). PASS needs every applicable gate clear with MEASURED evidence, a weighted score >= 85/100 and no dimension below 2. An applicable gate left NOT_ASSESSED counts as open (BLOCKED); 70-84 with owned, dated remediation is CONDITIONAL (pilot only).
- **Evidence required:** screenshots at compact and expanded widths, light and dark, 200% text, Arabic RTL and the longest declared language; a screen-reader and keyboard accessibility walkthrough; profiling on the declared reference hardware (`docs/governance/REFERENCE-HARDWARE.md`); Android and Core Web Vitals gates are N/A for this desktop product. An independent verifier signs every release; a human domain reviewer signs licensing or payment changes.
- **Money:** stored as integer minor units (or exact decimal strings), never floats; displayed decimals follow the currency's ISO 4217 minor unit, so UGX and RWF show 0 decimals (`USh 45,000`, never `USh 45,000.00`).
- **Document numbers (hard gate PX-GATE-13, rule PX-DATA-013):** contiguous per tenant per document type, allocated at issue inside the save transaction, sent as strings, provisional while offline, voided never deleted.
- **Existing guidance in conflict:** The approved Ogma design system stays the visual layer; the standard supersedes it where it conflicts.
