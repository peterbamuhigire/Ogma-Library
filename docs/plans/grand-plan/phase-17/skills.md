# Phase 17 â€” Skills & Slash Commands

Phase-scoped guidance. Every entry states which task it informs and what
artifact it must produce.

---

## Always-on

| Skill / command | Used in | Produces |
| --- | --- | --- |
| `superpowers:brainstorming` | Before WP1 (ADR-0012 design) and WP9 (multi-user UI flows) | Structured options for identity model; enrollment UX options |
| `superpowers:writing-plans` | Phase start | Executable plan from `tasks.md` |
| `superpowers:test-driven-development` | Every WP â€” especially WP6 (private DB isolation) and WP8 (sync) | Tests written before implementation |
| `superpowers:verification-before-completion` | Phase DoD | Standalone regression + Client mode integration verification on Win + macOS |
| `superpowers:requesting-code-review` + `/code-review` | P17-WP10-T7 | Resolved findings |
| `superpowers:systematic-debugging` | Any failing test | Diagnosis before fix |
| `superpowers:using-git-worktrees` | Phase 17 branch | `feature/P17-classroom-client` |
| `documentation-generation:docs-architect` | P17-WP1-T6 | Updated `SOURCE-SUMMARY.md` |

---

## Phase-17-specific skills

### `mobile-cross:pwa-offline-first`

- **When:** WP7 (offline cache design and implementation), WP8 (sync strategy).
- **Produces:** `IOfflineCacheService` design and implementation; sync blob
  format specification; LRU eviction policy; conflict-resolution algorithm.
- **Guidance:** Apply cache-first with stale-while-revalidate for catalogue
  projections. For page renders, apply cache-then-network (serve cache
  immediately; refresh in background if online). The cache key must include
  `eTag` so a Host-side book update invalidates cached renders. Do not cache
  the student's private DB in the same store â€” keep them strictly separate.

### `security:dual-auth-rbac` / `mobile-rbac`

- **When:** WP3 (profile role assignment, session token role embedding).
- **Produces:** Role-enforcement middleware on Host-client calls; role checks
  in `IProfileService`; role-based view visibility in the Client mode UI.
- **Guidance:** Roles are: `student` (read-only on shared catalogue, full on
  own private state), `teacher` (read + initiate curation), `guest` (read-only,
  no persistence). The role is embedded in the session token by the Host; the
  client must not self-elevate. Teacher enrollment requires Phase 18 admin
  approval (stub the check in Phase 17: teacher role only granted if the Phase
  18 admin console has issued the token).

### `architecture:event-driven-architecture`

- **When:** WP8 (sync event design, conflict notification).
- **Produces:** `ISyncService` event model; `ConflictDetectedEvent` type; the
  observable `SyncState` stream that drives the settings badge.
- **Guidance:** Sync is a pull-on-trigger model (not push from Host). Design
  the sync state machine: `Idle â†’ Syncing â†’ Conflict â†’ Resolved â†’ Idle`.
  `ConflictDetectedEvent` carries a list of `AnnotationConflict` records for
  the UI to render.

### `documentation-generation:architecture-decision-records`

- **When:** P17-WP1-T1 (ADR-0012).
- **Produces:** `docs/adrs/0012-classroom-identity-roles-private-state.md`.
- **Guidance:** The ADR must be precise on the student-data boundary: what the
  Host knows (profileId, display name, role, opaque sync blob) vs. what the
  Host never sees (annotation content, AI history, reading progress â€” unless
  sync opted in). This boundary is the anchor for the Phase 18 DPIA.

### `frontend-ux:enterprise-ux-process`

- **When:** WP9 (discovery, enrollment, profile switching flows).
- **Produces:** `DiscoveryView.axaml`, `EnrollmentView.axaml`, profile switcher
  component, sync settings panel.
- **Guidance:** The enrollment flow must be expressible by a 12-year-old student
  without IT support. The TOFU fingerprint comparison step must have a clear
  visual (fingerprint bytes displayed in chunked format, color-coded by byte
  group). Provide a "I'll do this later" escape path that places the client in
  guest mode.

### `saas:saas-tenant-onboarding-automation`

- **When:** WP3 (profile creation flow), WP9 (enrollment UI).
- **Produces:** `StudentOnboardingFlow` â€” the sequence from TOFU accept to
  first catalogue view; wizard-style or single-screen (brainstorm in WP1).
- **Guidance:** Apply onboarding principles: minimal friction, one decision per
  screen, recoverable choices. Profile creation is one decision (name + role);
  role defaults to `student`.

### `/security-review`

- **When:** P17-WP10-T6, after WP2, WP3, WP6, WP8 complete.
- **Produces:** Security review findings; resolved issues before merge.
- **Focus areas:** TOFU MITM surface (WP2-T3); session token in credential
  store not plain file (WP3-T2); private DB file permissions (WP6-T2);
  sync blob AES-256-GCM correctness (WP8-T1/T7); cross-profile isolation
  (WP6-T6).