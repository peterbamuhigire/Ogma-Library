# Plan template for the unattended driver

The driver can only execute what a plan states clearly. Put each plan in its own folder under `docs/plans/<plan-name>/` with a `PLAN.md` like this and a small `RESUME.md` (under 6 KB) the agent updates after each phase.

```markdown
# <Plan name>
Owner: <name>   Risk class: low | medium | high (auth, money, ledger, tenancy = high)

## Goal
One paragraph: the outcome and who benefits.

## Invariants (must hold after every phase)
- Tenant isolation: every query is scoped to the tenant.
- Money: integer minor units or exact decimals; no floats.
- Numbering: contiguous per tenant per document type, allocated inside the save transaction.
- <add the plan's own rules>

## Open decisions (each with a default the agent applies if nobody answers)
- D1: <question> -> default: <answer>, reason: <why>

## Phases (smallest deliverable first; one phase = one session)
### P1 <name>
- depends_on: none
- change: <files, tables, endpoints>
- acceptance (testable, one line each):
  - [ ] <observable behaviour and the test that proves it>
- test command: <targeted command>
- not in scope: <what to leave alone>
### P2 <name>
- depends_on: P1
...

## Definition of done
All phases accepted, handover file updated, ledger row set to done.
```

Rules of thumb: phases small enough for one session (about one to three hours of work); every acceptance line names its test; dependencies explicit so the order is a valid topological order; open decisions always carry a default; high-risk phases name their negative tests (wrong tenant, wrong role, replayed request, rounding).
