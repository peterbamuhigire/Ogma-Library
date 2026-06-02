# ADR-0013: School-Managed AI Through the Host Gateway

## Status

Accepted

> Ratified by owner direction during Phase 18 completion work on 2026-06-02.

## Date

2026-06-02

## Context

Phase 16 added an opt-in LAN Library Host. Phase 17 added Client/Classroom mode,
local profile identity, private per-student state, and optional encrypted sync.
Phase 18 adds school administration and managed AI: the school administrator
supplies provider keys, controls library publication, enrolls students and
teachers, and sets AI policy and quotas.

Students are often minors. The school is the data controller for classroom AI
use, and student devices must never receive provider API keys. ADR-0007 already
establishes a provider-neutral AI gateway with four privacy tiers for the
standalone product. Phase 18 must extend that gateway to the classroom Host
without creating a second egress path or weakening payload preview, audit,
quota, and DPIA controls.

## Decision Drivers

- Keep provider keys on the Host and out of every student client.
- Preserve ADR-0007's single AI egress chokepoint and privacy tiers.
- Default classroom AI to metadata-only and require admin opt-in for
  content-aware payloads.
- Enforce per-student and per-class quotas before provider calls.
- Record audit and usage evidence for every AI call without storing raw student
  queries unnecessarily.
- Fail closed for minors' data when DPIA jurisdiction or legal basis is missing.
- Keep school administration Host-local; no internet-facing admin console.

## Considered Options

### Option A - Host-owned AI gateway and admin policy

The Host stores school provider keys in the OS credential store. Student clients
send classroom smart-search requests to the Host. The Host builds the payload
preview, checks DPIA and quota policy, calls the existing AI gateway, grounds
citations against the Host catalogue, writes audit/usage records, and returns the
bounded response.

- **Pros:** keys never leave the Host; ADR-0007 remains enforceable; one place
  applies policy, quotas, DPIA, audit, and grounding; client devices remain
  simpler and safer.
- **Cons:** the Host becomes a higher-value system and needs stronger admin
  authentication, credential storage, and audit integrity controls.

### Option B - School key distributed to clients

The administrator stores a provider key on each student client, and clients call
the provider directly under local settings.

- **Pros:** less Host complexity and lower Host runtime load.
- **Cons:** violates key secrecy, fragments audit and quota enforcement, creates
  many egress paths, and makes minors' data controls unverifiable.

### Option C - Hosted cloud admin service

Ogma runs an internet-facing school admin and AI proxy service that stores keys
and policies centrally.

- **Pros:** easier cross-device administration and centralized observability.
- **Cons:** out of scope for the local-first product, introduces multi-tenant
  cloud operations, and expands compliance and hosting obligations.

## Decision Outcome

Choose **Option A: Host-owned AI gateway and admin policy**.

The School Administration bounded context owns:

- Host-local admin policy for published libraries, shared shelves, enrolled
  profiles, AI tier defaults, quotas, rate limits, and answer mode.
- School AI provider key status and rotation through the Host OS credential
  store. Public APIs expose only key status, never key material.
- A classroom AI proxy endpoint that requires an authenticated classroom
  session, builds a payload preview, blocks until confirmation, then runs the
  approved request through the Host AI gateway.
- DPIA screening before any off-device AI provider call. Missing jurisdiction,
  missing legal basis, or unapproved minor/content-aware combinations return a
  blocked result, not a best-effort provider call.
- Atomic quota reservation before provider calls and usage ledger/audit writes
  after successful calls.
- Grounding that removes citations not present in the Host catalogue before a
  response is returned to a student.

The context starts as a disabled scaffold. Activation requires Host mode,
administrator role enforcement, owner-ratified policy, and passing Phase 18
security/DPIA tests.

## Consequences

### Positive

- Student clients never hold provider keys and cannot bypass Host policy.
- Classroom AI governance is testable at one boundary.
- Metadata-only remains the default and content-aware use becomes a deliberate
  school decision.
- Quota and cost controls can be enforced per student and per class.
- Audit and DPIA evidence can be produced from Host-side records.

### Negative

- The Host needs admin authentication and stronger operational guidance.
- Offline client AI is not available in managed classroom mode unless it uses a
  lower local/offline tier.
- A Host outage pauses classroom AI, even if cached reading still works.
- The implementation must avoid retaining raw student query text in audit rows
  unless a future owner-ratified policy explicitly allows it.

### Affects

- ADR-0007 provider-neutral AI gateway and privacy tiers.
- ADR-0012 classroom identity, roles, and private state.
- Phase 18 School Administration and Managed AI.
- Phase 19 DPIA, threat model, and control hardening.
- CTRL-OGMA-001, CTRL-OGMA-016, CTRL-OGMA-018, CTRL-OGMA-020, CTRL-OGMA-022,
  and CTRL-OGMA-024.

## Amendment Log

| Date | Change |
| --- | --- |
| 2026-06-02 | Initial Phase 18 draft. |
| 2026-06-02 | Accepted by owner direction during Phase 18 completion work. |
