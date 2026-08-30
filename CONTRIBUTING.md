
# Contributing to Ogma Library

This guide defines how work enters the Ogma Library codebase. It applies to every
contributor — core team and external alike. A change that does not follow this
guide is not ready to merge.

Ogma Library is delivered under a **Hybrid (Water-Scrum-Fall)** methodology:
requirements are baselined in Phase 02, and delivery proceeds across the Phase 07
agile build phases. Every code change traces back to a baselined requirement.

---

## Table of Contents

1. [Branch Strategy](#1-branch-strategy)
2. [Conventional Commits](#2-conventional-commits)
3. [Traceability to Requirement Identifiers](#3-traceability-to-requirement-identifiers)
4. [Definition of Ready and Definition of Done](#4-definition-of-ready-and-definition-of-done)
5. [Code-Review Expectations](#5-code-review-expectations)
6. [Security Expectations](#6-security-expectations)
7. [Pre-PR Validation](#7-pre-pr-validation)
8. [DCO Sign-Off](#8-dco-sign-off)
9. [Reporting Issues](#9-reporting-issues)

---

## 1. Branch Strategy

`main` is always releasable. No one commits directly to `main`.

### Branch naming

| Purpose | Pattern | Example |
| --- | --- | --- |
| Feature / new capability | `feature/<phase-ID>-<slug>` | `feature/LIB-012-scan-folder` |
| Defect fix | `fix/<req-ID>-<slug>` | `fix/READ-004-page-turn-cache` |
| Integration branch | `develop` | — |
| Versioned release prep | `release/<semver>` | `release/1.0.0` |
| Production hot-fix | `hotfix/<semver>` | `hotfix/1.0.1` |
| Tooling / build / chore | `chore/<slug>` | `chore/build-props-net10` |

### Merge rules

- `main` ← `release/<semver>` only, via a signed merge commit. Fast-forward merges
  to `main` are **forbidden**. A release commit must be GPG-signed.
- `develop` ← `feature/*` / `fix/*` via a **squash or merge commit** after PR review.
  No direct pushes to `develop`.
- `release/<semver>` branches from `develop`; only release-prep and hot-fix commits
  are permitted on a release branch.
- `hotfix/<semver>` branches from `main`; merges back into both `main` and `develop`.

### Housekeeping

- Keep a branch focused on one story or one defect.
- Rebase on the target integration branch before opening a pull request so the
  history is linear and the CIA checklist is accurate.
- Delete the branch after the pull request merges.

---

## 2. Conventional Commits

All commits **must** conform to [Conventional Commits 1.0](https://www.conventionalcommits.org/en/v1.0.0/).
The `commit-msg` hook (see [§7](#7-pre-pr-validation)) rejects non-conforming messages at
commit time.

### Format

```
<type>[(<scope>)][!]: <subject>

[optional body]

[optional footer(s)]
```

- The **subject** line must be 72 characters or fewer, in the **imperative mood**.
  Example: `feat(workers): isolate per-file scan failures`
- A **breaking change** is signalled with `!` after the type/scope, or with a
  `BREAKING CHANGE:` footer token, or both.
- The subject line must not end with a period.

### Allowed types

| Type | When to use |
| --- | --- |
| `feat` | A new capability that satisfies a baselined FR |
| `fix` | A defect correction |
| `docs` | Documentation changes only |
| `test` | Adding or correcting tests; no production code changes |
| `refactor` | Code restructuring with no behaviour change |
| `chore` | Tooling, build scripts, dependency bumps, CI config with no src changes |
| `perf` | A change that improves performance with no behaviour change |
| `ci` | Changes to CI/CD pipeline definitions |
| `build` | Changes to the build system (MSBuild props, NuGet central PM, etc.) |

### Allowed scopes (bounded contexts and projects)

| Scope | Bounded context / project |
| --- | --- |
| `domain` | `OgmaLibrary.Domain` |
| `application` | `OgmaLibrary.Application` |
| `infrastructure` | `OgmaLibrary.Infrastructure` |
| `reader` | `OgmaLibrary.Reader` |
| `workers` | `OgmaLibrary.Workers` |
| `bookshelf3d` | `OgmaLibrary.Bookshelf3D` |
| `app` | `OgmaLibrary.App` (composition root / shell) |

Scope may be omitted for cross-cutting changes that do not map to a single
bounded context (e.g., `chore: bump .NET SDK to 10.0.x`).

### Footer tokens

| Token | Purpose | Example |
| --- | --- | --- |
| `Closes #NNN` | Links and closes a GitHub issue | `Closes #42` |
| `Implements LIB-012` | Traces to a baselined functional requirement | `Implements LIB-012` |
| `Fixes READ-004` | Traces to a baselined defect ID | `Fixes READ-004` |
| `NFR-OGMA-003` | States the NFR budget this change preserves | see body text |
| `BREAKING CHANGE:` | Describes the breaking change | `BREAKING CHANGE: renames IScanService` |
| `Signed-off-by:` | DCO sign-off (required — see §8) | `Signed-off-by: Name <email>` |

### Examples

```
feat(workers): isolate per-file scan failures

Each PDF file in a batch scan is wrapped in its own try/catch.
A failure is recorded as a structured BookFileFailure and the
scan continues to the next file.

Implements LIB-005
Closes #17
Signed-off-by: Peter Bamuhigire <peter@techguypeter.com>
```

```
fix(reader): correct page-turn cache invalidation on zoom change

A zoom change flushed only the visible page; adjacent pages
retained stale render bitmaps. Now flushes the whole viewport.

Fixes READ-004
Closes #23
Signed-off-by: Jane Contributor <jane@example.com>
```

```
feat(infrastructure)!: rename IScanService to ILibraryScanService

Aligns the interface name with the bounded-context vocabulary
introduced in ADR-0002.

BREAKING CHANGE: IScanService has been renamed ILibraryScanService.
Update all injection registrations in OgmaLibrary.App.

Implements LIB-001
Closes #30
Signed-off-by: Peter Bamuhigire <peter@techguypeter.com>
```

---

## 3. Traceability to Requirement Identifiers

Every story and every pull request links to one or more **baselined identifiers**
so the Hybrid traceability chain stays intact.

### Identifier groups

| Prefix | Domain |
| --- | --- |
| `LIB-NNN` | Library Setup and Scanning |
| `CAT-NNN` | Catalogue Browsing |
| `META-NNN` | Metadata Enrichment |
| `READ-NNN` | PDF Reader |
| `SEARCH-NNN` | Search and Indexing |
| `AI-NNN` | AI Advisor |
| `FR-NNN` | Generic functional requirement |
| `NFR-NNN` | Non-functional requirement |
| `ADR-NNNN` | Architecture Decision Record |

### Rules

1. Every commit that changes production code names at least one requirement ID in
   the commit footer.
2. A change that touches an NFR names the governing `NFR-` identifier and states
   how the change keeps the measurable threshold (e.g., "cold-start budget ≤ 3 s
   at P95").
3. A change that **alters a baselined FR, NFR, or security control** requires a
   Change Impact Analysis (CIA) entry, owner sign-off, and an ADR amendment before
   merging. See `docs/governance/CIA-WORKFLOW.md`.

---

## 4. Definition of Ready and Definition of Done

The authoritative DoR and DoD checklists live in the Phase 07 agile artifacts.
The summaries below are provided for quick reference.

### Definition of Ready (DoR)

A story is ready to enter a sprint when it:

- Traces to a baselined requirement (LIB/CAT/META/READ/SEARCH/AI/FR/NFR).
- Has acceptance criteria with a **deterministic pass-or-fail oracle** (not
  subjective judgement).
- Names its target privacy tier wherever AI or off-device behaviour is involved.
- Has a CIA entry if it alters a baselined requirement.

### Definition of Done (DoD)

A story is done when:

- The code is merged to the integration branch (`develop` or `release/*`).
- All new and existing tests pass, including the **golden corpus suite**.
- The coding standards (`docs/references/Ogma-Library_DevelopmentStandards.docx`)
  are satisfied (nullable enabled, TreatWarningsAsErrors, bounded-context
  dependency rule, per-file scan isolation).
- `dotnet format --verify-no-changes` reports a clean tree.
- The validation gate passes (see §7).
- The PR description lists all FR/NFR/ADR IDs implemented.
- DCO sign-off is present on every commit (see §8).

---

## 5. Code-Review Expectations

Every change merges through a **pull request with at least one approving review**.
The author does not approve their own pull request.

A reviewer confirms:

1. **Bounded-context dependency rule:** the domain project has no outward
   dependencies; the UI reaches AI/metadata/HTTP only through adapter interfaces
   registered in the composition root (`OgmaLibrary.App`).
2. **Per-file scan isolation:** a single corrupt PDF records a structured failure
   and the batch continues.
3. **Requirement traceability:** the PR lists the FR/NFR/ADR IDs it implements and
   the acceptance criteria are verifiable.
4. **No secrets:** no API key, token, password, or connection string with
   credentials appears in source, configuration files, or committed `.env` files.
5. **Privacy compliance:** changes affecting off-device data transmission are
   flagged for the privacy review defined in the CIA workflow.
6. **CIA checklist complete:** the PR template CIA checklist is filled out honestly.

Keep a pull request small enough to review in one sitting. Split a large change
into reviewable increments.

---

## 6. Security Expectations

### No secrets in source

No secret of any kind is committed to the repository: no API key, token,
password, or connection string with credentials. The build and review reject a
change that introduces a secret. Provider credentials (e.g., a cloud AI key)
are stored in the **OS credential store** (DPAPI on Windows, Keychain on macOS),
never in source, repository config files, or committed environment files.

### No inbound network listener

The application opens **no inbound network listener** by default. A change must
not introduce a server socket or HTTP endpoint in the desktop client. The
optional Library Host mode (Phases 16-18) is an explicit opt-in governed by its
own ADR and security model; its LAN transport surface is out of scope until that
ADR is ratified.

### Single AI egress chokepoint

Every off-device AI call routes through the `IAiProvider` gateway — the single
egress chokepoint. A change must not add a direct provider call that bypasses the
gateway or the active privacy tier and payload preview.

### Vulnerability reports

Report security vulnerabilities **privately** via `SECURITY.md` before opening a
public GitHub issue. Do not disclose a vulnerability publicly until a fix is
staged.

---

## 7. Pre-PR Validation

Run the following commands from the repository root before requesting review.
A PR that fails any of these is **not ready**.

```sh
# 1. Check formatting — must report no changes
dotnet format OgmaLibrary.sln --verify-no-changes

# 2. Full solution build — must complete with zero errors and zero warnings
dotnet build OgmaLibrary.sln --configuration Debug

# 3. Full test suite including golden corpus — must all pass
dotnet test OgmaLibrary.sln --configuration Debug -m:1

# 4. Hybrid validation gate — must exit 0
python -m engine validate Ogma-Library
```

See `docs/governance/HYBRID-GATE.md` for what the validation gate checks and
when it blocks.

### Install the commit-msg hook

Install the Conventional Commits enforcement hook so it rejects bad messages
locally before they reach CI:

**macOS / Linux:**
```sh
bash .github/hooks/install-hooks.sh
```

**Windows (PowerShell):**
```powershell
.\.github\hooks\install-hooks.ps1
```

---

## 8. DCO Sign-Off

Ogma Library uses the **Developer Certificate of Origin (DCO) 1.1** in place of a
contributor licence agreement. See `DCO.md` for the full DCO text.

Every commit must carry a `Signed-off-by` footer with your real name and email:

```
Signed-off-by: Your Name <you@example.com>
```

Add it automatically with:

```sh
git commit -s -m "feat(reader): your message here"
```

Or configure Git to add it to every commit:

```sh
git config --global format.signoff true
```

By signing off you certify, under the terms of the DCO, that you have the right
to submit the contribution under the project's MIT licence.

Pull requests where one or more commits lack `Signed-off-by` will not be merged.

---

## 9. Reporting Issues

- **Bugs and feature requests:** open a GitHub issue using the appropriate template.
- **Security vulnerabilities:** follow the responsible-disclosure process in
  `SECURITY.md`. Do not open a public issue for a security vulnerability.
- **Questions and discussions:** use GitHub Discussions.

---

*Ogma Library is a project of Chwezi Core Systems / Peter Bamuhigire.*
*© 2026 Peter Bamuhigire / Chwezi Core Systems. MIT Licence.*
