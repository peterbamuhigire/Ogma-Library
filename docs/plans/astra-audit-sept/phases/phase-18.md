# Phase 18: school administration and managed AI

Status: planned. Owner: school product/security lead; reviewers: school controller, admin and cost reviewer.
Dependencies:13/14/16/17. Estimate:6-10 person-days. Findings:F06/F17/F20/F30.
Requirements:ADMIN001-013; old phase36. Skills:E3/E4/E5/F1/D2; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

A school operates the e-library with understandable publication, enrollment, privacy and AI-spending controls. Teachers see only approved aggregate or assigned information; personal student notes remain private.

## Work slices

1. Trace SchoolAdmin services, HostSharingViewModel, policy storage, enrollment, usage dashboards, AI proxy and history management to real role-gated UI. Reconcile old HLD disabled/partial claims with actual registration and behavior.
2. Design separate admin tasks: publish collection, enroll/revoke profile, assign role/scope, configure managed provider, set quotas, inspect usage, erase/export and recover. Hide administrative complexity from students.
3. Keep school provider credentials on the host in OS-backed custody. Validate provider/tier/scope and publication boundaries at the host gateway, not only the student UI.
4. Apply phase13's trusted price and durable-budget model to school quotas, concurrent students, retries and missing provider usage. Distinguish token allowance, estimated monetary cost and actual provider billing.
5. Reconcile aggregate usage/reading dashboards to permitted source records. Prevent dashboards from exposing individual private notes, sensitive reading content or small-group inferences beyond approved policy.
6. Complete deployment-specific privacy/controller documentation using the supplied DPIA as a design input. Assign retention, erasure, incident handling and minor/student-data review owners; verify current jurisdictional claims before sign-off.

## Acceptance

- Student/teacher/admin permission matrix passes through actual endpoints and UI; changing a role affects subsequent authorization safely.
- School key never reaches a client; clients cannot evade quota/tier/publication rules by sending alternate request fields.
- Usage dashboards reconcile to gateway fixtures, including canceled/retried/unknown-price calls; no unsupported zero-cost claim.
- Admin erasure/export operates on the intended profile/scope, with content minimization and auditable outcome.
- A school administrator completes representative operations without developer tooling; warning/confirmation copy states effect and recovery.
- Signed deployment-specific controller/privacy review is attached before school pilot; source-only DPIA language remains insufficient.

## Recovery

Disable managed AI independently of local reading and classroom catalogue use. Preserve host key rotation/recovery and policy rollback. Revert a dashboard/policy change if it exposes unauthorized detail or misstates usage. Re-measure administrator task errors and student privacy boundaries during the beta window.
