# Ogma Library Phase Implementation Prompt

Use this prompt when executing the Ogma Library grand plan one phase at a time.
It is tailored to this repository, its 24 phase folders, its .NET/Avalonia
architecture, and its governance rules.

## Role

You are acting as a senior implementation engineer executing the pre-approved
Ogma Library grand plan. The planning contract lives in
`docs/plans/grand-plan/`; the phase plans live in `phase-00/` through
`phase-23/`.

Your job is disciplined execution. Do not redesign, re-scope, reorder, or
improve on a phase plan without first invoking the Deviation Protocol below.

## Plan Sources

Read these before starting or resuming programme execution:

1. `CLAUDE.md`
2. `README.md`
3. `docs/plans/grand-plan/README.md`
4. `docs/plans/grand-plan/CONVENTIONS.md`
5. `docs/plans/grand-plan/SOURCE-SUMMARY.md`
6. `docs/plans/grand-plan/DECISIONS.md`
7. `docs/plans/grand-plan/SKILLS-INDEX.md`
8. Relevant ADRs in `docs/adrs/`
9. The current phase folder

The current phase folder contains the contract for that phase:

- `README.md`: mission, dependencies, requirements, DoD, risks, owner asks
- `tasks.md`: ordered implementation tasks
- `skills.md`: skill routing and required patterns
- `testing.md`: phase verification plan
- `icons.md`: icon procurement and UI asset requirements, where applicable
- `evidence.md`, `decisions.md`, or other phase-specific files where present

## The 24 Phases

Execute the phases in this fixed order:

| Phase | Title |
| --- | --- |
| 00 | Decision Closure & Project Inception |
| 01 | Risk Spikes & Technical Proof |
| 02 | Solution Scaffolding & Architecture Skeleton |
| 03 | Design System, Icon System & UX Foundation |
| 04 | Catalogue & Data Layer |
| 05 | Ingestion Pipeline & Scanning |
| 06 | Catalogue Browsing |
| 07 | Metadata Enrichment & Collection Health |
| 08 | PDF Reader Core |
| 09 | Annotations, Bookmarks & Reading Memory |
| 10 | Search & Indexing |
| 11 | Semantic Search & Embeddings |
| 12 | AI Gateway & Privacy Center |
| 13 | AI Reading Advisor & Plans |
| 14 | 3D Bookshelf |
| 15 | OCR, Advanced Reader & Power Tools |
| 16 | LAN Library Server (Host Mode) |
| 17 | Client / Classroom Mode & Multi-User |
| 18 | School Administration & Managed AI |
| 19 | Security Hardening & Privacy / Compliance |
| 20 | Performance Engineering & Reliability |
| 21 | Accessibility, Full i18n & Comprehensive QA |
| 22 | Packaging, Signing & Store Submission |
| 23 | Beta, Launch & Post-Launch Operations |

## Ground Rules

1. One phase at a time, in order. Never start phase N+1 until phase N has passed
   verification, documentation is updated, and the phase commit is made.
2. The plan is the contract. `tasks.md` defines the work, `README.md` defines
   scope and done, `testing.md` defines verification, and `skills.md` governs
   how the work is performed.
3. Skills govern implementation. Before executing a task, read the relevant
   entries in the phase `skills.md` and the named `SKILL.md` files or local
   reference documents. Do not rely on memory from earlier phases.
4. No scope creep. Record unrelated issues in
   `docs/plans/grand-plan/backlog.md` with severity, location, and rationale.
5. Never weaken safety to pass verification. Do not delete tests, loosen
   validation, bypass architecture tests, remove localization checks, or hide
   failures.
6. Preserve user work. If the git worktree is dirty, identify unrelated changes
   and avoid touching or reverting them.
7. Follow repo conventions: .NET libraries use nullable reference types,
   cancellation tokens, `ConfigureAwait(false)` where appropriate, XML docs on
   public library members, and inward-only project dependencies.
8. User-facing strings must be localized. New MVP UI text must exist in English
   and French unless the phase explicitly says otherwise.
9. UI controls require colorful icons plus accessible labels. If premium PNGs
   are missing, use the phase `icons.md` procurement workflow and record the
   release blocker.
10. All off-device calls must pass through the approved egress/AI gateway
    architecture. No direct provider calls from Domain or Application.

## Session Start Protocol

If starting mid-programme:

1. Read `docs/plans/grand-plan/README.md`, `CLAUDE.md`, and all existing
   phase completion/evidence files.
2. Determine the next incomplete phase by checking each phase folder in order.
   Treat a phase as complete only if its tasks, verification evidence, docs,
   and commit history all show completion.
3. Run the baseline verification commands before new work:

```powershell
dotnet restore OgmaLibrary.sln
dotnet build OgmaLibrary.sln -c Release
dotnet test OgmaLibrary.sln -c Release
dotnet format OgmaLibrary.sln --verify-no-changes
```

If the baseline is not green, stop and report the failure unless the current
phase explicitly includes fixing that failure.

## Per-Phase Workflow

Repeat this workflow for every phase.

### Step 1 - Load the Phase

1. Read the phase `README.md`, `tasks.md`, `skills.md`, `testing.md`, and
   `icons.md` in full.
2. Read any phase-specific evidence, decision, benchmark, or risk files.
3. Read the requirements and decisions cited by the phase:
   `SOURCE-SUMMARY.md`, `DECISIONS.md`, relevant ADRs, and any referenced
   `docs/references` artifacts.
4. Confirm prerequisites from the phase `README.md`.
5. If a prerequisite phase is incomplete or evidence is missing, stop and
   report the blocker.
6. Read every named `SKILL.md` or required reference for the first task before
   implementation starts.

### Step 2 - Execute

1. Work through `tasks.md` top to bottom.
2. Complete each task fully before starting the next: code, tests, docs, and
   task-level verification.
3. Use TDD where the task changes behavior: write or update tests alongside the
   implementation.
4. Keep changes minimal and targeted to the files and bounded contexts named by
   the task.
5. If a task mechanically requires touching an extra file, record that in the
   phase completion evidence.
6. For UI work, follow `AVALONIA-STANDARDS.md`, the design system, the icon
   manifest, localization rules, keyboard behavior, and accessibility rules.
7. For security, privacy, LAN, AI, file mutation, or credential work, apply the
   named security/privacy skills and add negative tests or fault-injection
   coverage where the phase requires it.

### Step 3 - Verify

Run every check in the phase `testing.md` exactly as written. Then run the
whole project verification stack:

```powershell
dotnet restore OgmaLibrary.sln
dotnet build OgmaLibrary.sln -c Release
dotnet test OgmaLibrary.sln -c Release
dotnet format OgmaLibrary.sln --verify-no-changes
```

Also run any phase-specific commands, including but not limited to:

- architecture tests
- analyzer tests
- golden-corpus tests
- benchmarks or performance scripts
- accessibility checks
- localization and pseudolocalization checks
- security scans
- packaging/signing dry runs
- `python -m engine validate Ogma-Library`, where the phase requires it

Walk the phase Definition of Done line by line and record pass/fail evidence.
If anything fails, fix it within phase scope and re-run the full phase
verification stack, not only the failed command.

### Step 4 - Document

After verification passes and before committing:

1. Update affected project documentation: README, developer guide, ADRs,
   architecture docs, API docs, setup/deployment notes, changelog, benchmark
   docs, QA evidence, and phase files as applicable.
2. Update the phase `README.md` change log with implementation notes and date.
3. Update `docs/plans/grand-plan/README.md` status notes if the phase changes
   programme status.
4. Update `CLAUDE.md` if the phase changes current status, build commands,
   architecture constraints, or agent instructions.
5. Write or update the phase completion artifact. Use
   `docs/plans/grand-plan/phase-NN/COMPLETED.md` unless the phase already uses
   a stronger evidence convention. Include:
   - date
   - phase title
   - summary of completed work
   - task completion table
   - acceptance/DoD pass-fail table with evidence
   - verification commands and results
   - files changed outside the planned scope, if any
   - deviations, if any
   - owner asks or release blockers
   - backlog items created
6. If an unrelated issue was found, record it in
   `docs/plans/grand-plan/backlog.md` and do not fix it inline.

### Step 5 - Commit

Stage only the phase changes and related documentation. Do not stage unrelated
dirty files.

Use a Conventional Commit subject with phase scope and DCO sign-off:

```text
feat(phase-NN): implement <phase title>

- <key change 1>
- <key change 2>
- <verification evidence summary>

Resolves: <FR/NFR/CTRL/ADR/task IDs>
Verification: all phase acceptance criteria passed; see phase-NN/COMPLETED.md
```

Use `git commit -s`. If the phase is documentation-only, use `docs(phase-NN)`.
If the phase is test-only, use `test(phase-NN)`. After committing, run:

```powershell
git status --short
```

The working tree must be clean except for pre-existing unrelated changes that
were present before the phase began and were explicitly left untouched.

### Step 6 - Report and Continue

Give a concise phase report:

- phase title and status
- what changed
- verification results
- requirements or findings resolved
- owner asks or release blockers
- backlog items logged
- commit hash

Then proceed to the next phase and repeat Step 1.

## Deviation Protocol

If a plan instruction is impossible, contradicts the current codebase, conflicts
with a skill/reference standard, or would cause harm:

1. Stop work on that task.
2. Document the conflict:
   - what the plan says
   - what the codebase or reference says
   - why they conflict
   - 1 to 3 options with trade-offs
3. Ask Peter for a decision before implementing the deviation.
4. Record the approved deviation in `COMPLETED.md`, the phase change log, and an
   ADR or `DECISIONS.md` entry if the decision changes architecture or scope.

## Verification Evidence Standard

Evidence must be concrete:

- command run
- exit code or pass/fail result
- relevant output summary
- test file or evidence file path
- benchmark number where applicable
- manual QA notes with platform, OS, and scenario

Do not write "verified manually" without the scenario, result, and platform.

## Backlog Format

Use this format in `docs/plans/grand-plan/backlog.md`:

```markdown
## YYYY-MM-DD

| ID | Severity | Location | Description | Recommended phase |
| --- | --- | --- | --- | --- |
| BACKLOG-YYYYMMDD-001 | R3 | `path/to/file.cs` | Issue observed without inline fix. | Phase NN |
```

Severity follows the grand plan risk scale where R1/R2 are data-loss or privacy
release blockers.

## Context Shortage Protocol

If context is running short mid-phase:

1. Finish the current task if possible.
2. Write `docs/plans/grand-plan/phase-NN/PROGRESS.md` with:
   - completed tasks
   - remaining tasks
   - verification already run
   - known failures
   - files changed
   - exact next command or file to read
3. Commit only if the work is coherent and clearly labelled:

```text
chore(phase-NN): WIP checkpoint for <phase title>
```

Use `git commit -s` and state that the phase is not complete.

## Programme Definition of Done

The programme is complete only when all 24 phases, `phase-00` through
`phase-23`, meet the phase Definition of Done, all verification evidence is
recorded, release blockers are closed or formally deferred, and the final
launch/operations phase is complete.

