# ADR-0015: Documentation Baseline v2.0 Supersedes the v1.0 Baseline

## Status

Accepted

> Ratified in remediation Phase 02, 2026-07-07.

## Date

2026-07-07

## Context

The original v1.0 documentation baseline was signed on 2026-05-30, before the
classroom/LAN-host track, local OCR, school-managed AI gateway, and later
implementation evidence existed. The extracted reference set for the
2026-07-07 audit records a v2.0 baseline that distinguishes implemented
behaviour from design-to-be phases, but ADR-0015 itself was not present in the
live ADR folder and remained Proposed in the finding register.

Remediation work depends on one binding baseline so implementation agents can
separate accepted decisions, open release blockers, and future scope without
re-litigating the plan.

## Decision Drivers

- Make the live repository docs the authoritative baseline for remediation.
- Preserve traceability from audit findings to phase completion records.
- Distinguish implemented, partial, blocked, and design-to-be work.
- Prevent older v1.0 language from overriding accepted ADRs and phase evidence.

## Considered Options

### Option A - Adopt the v2.0 documentation baseline

- **Pros:** matches the current product scope; makes remediation findings,
  ADRs, phase plans, and completion records binding; supports score trajectory
  reporting.
- **Cons:** requires docs to be updated whenever code, verification evidence,
  or release state changes.

### Option B - Amend the v1.0 baseline incrementally

- **Pros:** smaller formal change.
- **Cons:** keeps old scope and current evidence interleaved, making release
  state harder to audit.

### Option C - Leave v1.0 as the only baseline

- **Pros:** no document change.
- **Cons:** conflicts with the implemented codebase and the remediation plan.

## Decision Outcome

Adopt Option A. The 2026-07-07 remediation documentation set is the binding
v2.0 baseline for the current programme. Phase plans define scope, acceptance
criteria define done, verification documents define proof, and `COMPLETED.md`
records are the dated evidence for each closed phase.

ADR-0014 and ADR-0015 are accepted as part of Phase 02. Earlier accepted ADRs
remain binding unless a later ADR supersedes them. Open findings stay open until
their assigned remediation phase passes verification and updates the findings
register.

## Consequences

### Positive

- The repository has one authoritative baseline for remediation execution.
- Documentation, findings, and completion evidence can be audited together.
- Later phases inherit accepted architecture and runtime decisions instead of
  rediscovering them.

### Negative

- Stale docs are now a phase failure and must be corrected before commit.
- Draft or future-scope material must be labelled explicitly.

### Affects

- `docs/analysis-report-2026-07-07/99-findings-register.md`.
- `docs/plans/analysis-report-2026-07-07/00-master-plan.md`.
- All phase `COMPLETED.md` records for the 2026-07-07 remediation programme.
- The ADR index in `docs/adrs/README.md`.
