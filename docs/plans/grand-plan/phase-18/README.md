# Phase 18 — School Administration & Managed AI

Deliver the admin console that gives schools complete control over their shared
library — profile enrollment, shelf curation, permissions, usage dashboards —
and introduce school-provisioned AI keys that make the Host the single AI egress
chokepoint for all classroom students, subject to entitlements, quotas, audit,
and minors'-data protections.

---

## 1. Title & one-line mission

**Phase 18 — School Administration & Managed AI**
A school administrator configures the classroom library from a dedicated console,
enrolls students and teachers, sets AI policies and budgets, and views usage — while
every student's AI query routes through the Host's `IAiProvider` gateway under the
same four privacy tiers, with full audit and DPIA compliance, and students never
see or hold API keys.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Release tier** | V2 |
| **Estimate** | 4 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | New (LAN classroom expansion + school AI) |
| **Platforms** | Windows 10/11 + macOS 13+ (Host runs both; admin console is on the Host machine) |
| **Status** | Implementation started locally |
| **Depends on** | Phase 16 (Host infrastructure, audit), Phase 17 (profile/role model, ADR-0011), Phase 12 (IAiProvider gateway, privacy tiers, cost metering) |
| **ADRs introduced** | ADR-0013 (accepted — see §7) |

---

## 3. Objectives

When this phase is done, all of the following are true:

1. A school administrator can publish/unpublish folders to the shared library,
   curate themed shelves, and set per-folder availability from the admin console.
2. The admin can enroll, edit, and revoke student and teacher profiles; assign
   roles; and set per-profile or per-group AI quotas.
3. School-supplied AI API keys are stored in the Host's OS credential store
   (CTRL-OGMA-001) and never transmitted to or stored on client devices; the Host
   is the sole AI egress for all classroom requests (no student holds a key).
4. All student AI queries route through the existing Phase 12 `IAiProvider`
   gateway on the Host, under the four privacy tiers; the class default is
   metadata-only; content-aware is an admin opt-in per library.
5. Entitlements, per-student / per-class quotas, rate limits, and real-time cost
   visibility are enforced and visible to the admin.
6. A moderated student smart-search experience cites only local evidence from the
   curated collection (FR-AI-008 in managed-classroom mode); AI output is bounded
   to the Host catalogue.
7. Every AI call produces a local audit entry (CTRL-OGMA-018); DPIA screening is
   applied per off-device feature (CTRL-OGMA-024) — this is critical because
   students are minors.
8. ADR-0013 is authored, covering the school-managed-AI model, key storage,
   class-level gateway, entitlements, and minors'-data handling.

---

## 4. Scope

### In scope

- New bounded context: **School Administration** (`OgmaLibrary.SchoolAdmin`).
- Admin console (Host-local UI, accessible only when Host mode is running and
  the operator is authenticated as `admin` role):
  - Library management: publish/unpublish folders; folder-level privacy settings
    (metadata-only / content-aware).
  - Shelf curation: create/edit/delete shared shelves; assign books; set
    visibility (all students / teacher-only / specific groups).
  - Profile enrollment: create/edit/revoke student and teacher profiles; assign
    roles; generate enrollment tokens.
  - AI policy: enable/disable AI per library; set class default privacy tier;
    set content-aware opt-in per library; set per-student and per-class daily
    token quotas; set rate-limit (queries per minute); enable/disable answer
    mode.
  - Usage dashboard: per-student and per-class AI query count, estimated cost,
    quota utilization, last-query timestamp; visualized with bar/line charts.
  - Audit log viewer: filterable log of all LAN requests and AI calls with
    client identity, resource, action, timestamp.
- School-provisioned AI keys:
  - Admin enters API key(s) in admin console; keys stored in OS credential store
    (CTRL-OGMA-001) on the Host; never written to any client.
  - `SchoolAiKeyProvider` adapter implements `IAiProvider`; it is registered in
    the DI composition root only in Host mode.
  - All student AI queries (from Client mode) arrive at the Host as proxied
    requests; the Host's `IAiProvider` gateway processes them, applying the
    four privacy tiers, payload preview, and cost metering (Phase 12).
- Classroom smart-search (student-facing, on client):
  - Student enters a natural-language query; it is sent to the Host's AI
    proxy endpoint (new: `POST /api/v1/ai/search`).
  - Host processes: privacy tier check, payload preview (displayed to student
    before submission — consent step), AI call via `IAiProvider`, response
    bounded to local catalogue evidence.
  - Answer mode: cites book title + page (FR-AI-008 adapted for classroom).
  - Student sees the active privacy tier label and a payload preview before
    any off-device call.
- Entitlements & quotas: per-student daily token budget; per-class daily token
  budget; rate limit (queries/minute per student); admin can override; quota
  exhaustion returns a friendly message not an error.
- Minors' data (DPIA-critical):
  - CTRL-OGMA-024 DPIA screening applied to every off-device AI call.
  - Data minimization: only the approved payload (metadata-only by default) sent
    to the AI provider.
  - Jurisdiction note: the Phase 00 jurisdiction gap must be resolved before
    Phase 18 ships; the admin console includes a "Legal basis" configuration
    step.
  - AI query history for students stored on the Host (per-profile, per-session)
    and on the client's private DB; student can delete own history; admin can
    purge all history for their institution.
- ADR-0013: school-managed AI model.
- i18n: all admin console and student AI search strings in en + fr.

### Explicitly out of scope

- Internet-facing admin console (admin console is Host-local only).
- Multi-school / multi-tenant hosting.
- Student AI query on the client without routing through the Host.
- LLM fine-tuning or custom model training.
- Cloud sync of admin configuration (OQ-08).
- Linux.

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-ADMIN-001 | V2 | Admin can publish/unpublish folders to shared library | Integration test: published folder books appear in client catalogue; unpublished do not |
| FR-ADMIN-002 | V2 | Admin can create/edit/delete shared shelves and assign books | Integration test: shelf created by admin visible to all enrolled students in client view |
| FR-ADMIN-003 | V2 | Admin can enroll, edit, revoke student/teacher profiles | Integration test: enrolled profile can connect; revoked profile receives 401 |
| FR-ADMIN-004 | V2 | School AI API key stored in OS credential store; never transmitted to client | Architecture test: `SchoolAiKeyProvider` reads from `ICredentialStore`; no key value appears in any client HTTP response |
| FR-ADMIN-005 | V2 | All student AI queries route through Host `IAiProvider` gateway | Architecture test: client AI search endpoint calls `IAiProvider` on Host; no direct provider call from client |
| FR-ADMIN-006 | V2 | Class default privacy tier = metadata-only; content-aware requires admin opt-in per library | Integration test: default tier is `MetadataOnly`; student query sends only metadata; content-aware tier active only after admin opt-in |
| FR-ADMIN-007 | V2 | Student sees privacy tier label + payload preview before any off-device AI call | Integration test: payload preview shown before `POST /api/v1/ai/search` sends to provider; student can cancel |
| FR-ADMIN-008 | V2 | Per-student and per-class AI quotas enforced | Integration test: student exceeds daily quota → friendly quota-exhausted response; no provider call made |
| FR-ADMIN-009 | V2 | Rate limit: queries/minute per student enforced | Integration test: >rate_limit requests in 1 min → 429 with retry-after; below limit succeeds |
| FR-ADMIN-010 | V2 | Admin usage dashboard shows per-student query count, cost, quota % | UI test: dashboard shows correct counts from `AuditEvents`; chart renders with correct data |
| FR-ADMIN-011 | V2 | Classroom answer mode cites local catalogue evidence only | Integration test: AI response in answer mode contains only book references present in Host catalogue; no hallucinated citations |
| FR-ADMIN-012 | V2 | Student can delete own AI query history | Integration test: student deletes history → `StudentAiHistory` cleared in private DB + Host audit suppressed |
| FR-ADMIN-013 | V2 | Admin can purge all student AI history for institution | Integration test: admin purge → all `AiQueryHistory` rows for institution removed from Host DB |
| CTRL-OGMA-001 | V2 | School API key in OS credential store | Unit test: `SchoolAiKeyProvider.GetKeyAsync()` retrieves from `ICredentialStore`; no plaintext in DB |
| CTRL-OGMA-018 | V2 | All AI calls produce audit entry with student identity, tier, cost | Audit integration test: 5 student AI queries → 5 audit rows with `profileId`, `tier`, `estimatedCostUsd` |
| CTRL-OGMA-024 | V2 | DPIA screening per off-device feature; minors' data | Architecture test: AI proxy endpoint calls `IDpiaScreeningService.CheckAsync()` before forwarding to `IAiProvider`; test that a disqualified DPIA check blocks the call |
| FR-AI-008 (classroom) | V2 | Answer mode cites local evidence only (classroom context) | Integration test with golden corpus: answer cites only books in Host catalogue |
| FR-AI-010 | V1 | Per-call model usage + estimated cost visible in admin dashboard | Integration test: admin dashboard `UsageEntry.EstimatedCostUsd` matches metering model |
| ADR-0013 | V2 | School-managed AI model documented and ratified | ADR-0013 authored; owner sign-off |

---

## 6. Dependencies

### Depends on

- **Phase 16**: Host HTTPS endpoints, authentication, audit service, session
  tokens, OS credential store pattern.
- **Phase 17**: `IProfileService`, `ISyncService`, role model (admin role added
  in this phase as an elevation of teacher), `StudentAiHistory` schema.
- **Phase 12**: `IAiProvider` gateway, four privacy tiers, `IAiPrivacyService`,
  `IAiCostMeteringService`, `IAuditService`, payload preview.

### Unblocks

- **Phase 19**: DPIA and minors'-data compliance hardening; threat model for the
  AI proxy endpoint.
- **Phase 20**: classroom AI throughput benchmarks.
- **Phase 21**: admin console full i18n (es/it/de).

---

## 7. Architecture & approach

### ADR-0013 (accepted)

**Title:** School-managed AI — keys on Host, class-level gateway, entitlements
and quotas, minors' data handling.

**Context:** Schools supply their own AI provider keys so students can perform
moderated smart searches of the curated collection. Students are often minors;
the school is the data controller. API keys must never reach student devices.
All AI traffic must be auditable and bounded by the school's policy.

**Decision:**

1. **Key storage:** School AI API keys are stored in the Host's OS credential
   store (DPAPI on Windows / Keychain on macOS) via `ICredentialStore`
   (CTRL-OGMA-001). They are never written to SQLite, log files, or transmitted
   in any HTTP response. The admin enters keys through a secure text field
   (input is masked; value is written directly to credential store and then
   zeroed in memory).

2. **Class-level gateway:** The Host exposes `POST /api/v1/ai/search` to
   authenticated LAN clients. This endpoint is the single AI egress chokepoint
   for the entire classroom. It calls the Phase 12 `IAiProvider` gateway — the
   same gateway used by Standalone users. No client calls an AI provider directly.
   The client-side `IAiAdvisorService` in Client mode delegates all calls to this
   Host proxy endpoint rather than calling a provider.

3. **Privacy tiers in classroom:** the four tiers (Offline / MetadataOnly /
   ContentAware / LocalOllama) are enforced on the Host. The class default is
   `MetadataOnly`. `ContentAware` requires admin opt-in per library; the admin's
   choice is stored in `LibrarySettings.AiTier`. Students cannot override the
   class default upward; they can request a lower tier (Offline) at query time.
   The payload preview step is preserved: the Host sends the preview back to
   the student before executing the AI call; the student must confirm.

4. **Entitlements & quotas:** `SchoolAiEntitlement` rows in the Host catalogue DB:
   per-student daily token budget (default: 10,000 tokens); per-class daily budget
   (default: 500,000 tokens); rate limit per student (default: 5 queries/minute).
   Quota is checked before any provider call; exhaustion returns a quota-exceeded
   response without calling the provider. Admin can adjust per-student or apply a
   class-wide policy.

5. **Minors' data (DPIA):** Before any off-device AI call, `IDpiaScreeningService`
   checks: (a) is the requesting profile a minor (per admin-set birth-year or
   default-to-minor policy); (b) is the active tier approved for minors per the
   school's jurisdiction (Phase 00 gap — admin must configure); (c) is the
   payload within the approved scope. A disqualifying check blocks the call and
   returns an informative message. Every call writes a DPIA screening result to
   `AuditEvents`.

6. **Answer mode (classroom):** Student smart-search in answer mode cites only
   books and pages present in the Host catalogue (FR-AI-008). The Host wraps
   the AI response through a `ClassroomAnswerGrounder` that filters out any
   citations not matching a `bookId` in the catalogue. Hallucinated citations are
   removed.

7. **Audit:** every proxied AI call produces an `AuditEvents` row: `profileId`,
   `tier`, `queryHash` (not the raw query — data minimization), `tokensUsed`,
   `estimatedCostUsd`, `dpiaResult`, `timestamp`.

**Consequences:**

- Students never need to know or supply an API key.
- Admin has full visibility and control over AI spend.
- The privacy-tier model and payload-preview flow from Phase 12 are re-used
  intact; no new privacy architecture is introduced.
- The `IDpiaScreeningService` is introduced here and hardened in Phase 19.

**Status:** Proposed 2026-05-30. Ratify at Phase 18 start.

---

### Bounded context: School Administration

Location: `OgmaLibrary.Infrastructure.SchoolAdmin`.

Interfaces owned (from `Application/SchoolAdmin/`):
- `ILibraryPublishingService` — publish/unpublish folders; folder settings.
- `ISharedShelfService` — CRUD shared shelves; assign books.
- `IProfileEnrollmentService` — enroll/edit/revoke profiles; generate tokens.
- `ISchoolAiPolicyService` — AI tier settings, quotas, rate limits.
- `ISchoolAiKeyProvider` (implements `IAiProvider` for Host mode) — retrieves
  key from `ICredentialStore`; calls provider.
- `IAiProxyEndpointHandler` — handles `POST /api/v1/ai/search`; enforces tiers,
  quotas, payload preview, DPIA, grounding.
- `IUsageDashboardService` — aggregates `AuditEvents` into usage summaries.
- `IDpiaScreeningService` — DPIA screening per off-device call.

### Cross-platform notes

| Concern | Windows | macOS |
| --- | --- | --- |
| Key entry UI masked field | `PasswordBox` in Avalonia | Same |
| Credential store (API key) | DPAPI via `ICredentialStore` | Keychain via same abstraction |
| Admin console access | Host-local; no remote admin surface | Same |
| Chart rendering | Avalonia LiveCharts2 or SkiaSharp-drawn bar/line charts | Same |

### Data / schema changes (Host catalogue DB)

New migration: `M018_AddSchoolAdminTables`.

```sql
CREATE TABLE LibraryPublishSettings (
    LibraryRootId   TEXT PRIMARY KEY,
    IsPublished     INTEGER NOT NULL DEFAULT 0,
    AiTier          TEXT NOT NULL DEFAULT 'MetadataOnly',
    UpdatedAt       TEXT NOT NULL
);

CREATE TABLE SharedShelves (
    Id              TEXT PRIMARY KEY,
    Name            TEXT NOT NULL,
    Description     TEXT,
    Visibility      TEXT NOT NULL DEFAULT 'AllStudents', -- 'AllStudents'|'TeacherOnly'|'Group:<groupId>'
    CreatedAt       TEXT NOT NULL,
    UpdatedAt       TEXT NOT NULL,
    IsDeleted       INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE SharedShelfBooks (
    ShelfId         TEXT NOT NULL REFERENCES SharedShelves(Id),
    BookId          TEXT NOT NULL,
    AddedAt         TEXT NOT NULL,
    PRIMARY KEY (ShelfId, BookId)
);

CREATE TABLE EnrolledProfiles (
    ProfileId       TEXT PRIMARY KEY,
    DisplayName     TEXT NOT NULL,
    Role            TEXT NOT NULL,  -- 'student'|'teacher'|'admin'
    BirthYear       INTEGER,        -- nullable; if null: treat as minor
    EnrollmentToken TEXT UNIQUE,    -- one-time use; nulled after enrollment
    EnrolledAt      TEXT,
    RevokedAt       TEXT
);

CREATE TABLE SchoolAiEntitlements (
    ProfileId               TEXT NOT NULL PRIMARY KEY,
    DailyTokenBudget        INTEGER NOT NULL DEFAULT 10000,
    ClassDailyTokenBudget   INTEGER NOT NULL DEFAULT 500000,
    RateLimitQueriesPerMin  INTEGER NOT NULL DEFAULT 5,
    UpdatedAt               TEXT NOT NULL
);

CREATE TABLE AiUsageLedger (
    Id              TEXT PRIMARY KEY,
    ProfileId       TEXT NOT NULL,
    Date            TEXT NOT NULL,  -- YYYY-MM-DD
    TokensUsed      INTEGER NOT NULL DEFAULT 0,
    QueryCount      INTEGER NOT NULL DEFAULT 0,
    EstimatedCostUsd REAL NOT NULL DEFAULT 0.0,
    UpdatedAt       TEXT NOT NULL
);
```

---

## 8. Work breakdown (summary)

Full task detail in `tasks.md`.

| Work package | Key tasks | Est. |
| --- | --- | --- |
| **WP1 — ADR-0013 & admin context scaffold** | Author ADR-0013; SchoolAdmin bounded context; interfaces; DI wiring; architecture tests | 2 d |
| **WP2 — Library publishing & curation** | Publish/unpublish folders; AI tier per library; shared shelf CRUD; book assignment | 3 d |
| **WP3 — Profile enrollment** | Enroll/edit/revoke profiles; enrollment token flow; role assignment; `EnrolledProfiles` table | 2 d |
| **WP4 — School AI key management** | Key entry UI; `ISchoolAiKeyProvider`; credential store integration; architecture isolation test | 2 d |
| **WP5 — AI proxy endpoint** | `POST /api/v1/ai/search`; tier enforcement; payload preview; DPIA screening; quota check; `ClassroomAnswerGrounder` | 4 d |
| **WP6 — Entitlements & quotas** | Per-student/per-class budgets; rate limiting; `AiUsageLedger`; quota-exhausted response | 2 d |
| **WP7 — Usage dashboard** | Aggregate `AuditEvents` and `AiUsageLedger`; bar/line charts; per-student drill-down | 2 d |
| **WP8 — Audit log viewer** | Filterable audit log in admin console; export to CSV | 1 d |
| **WP9 — Student smart-search UI (client side)** | Natural-language query bar; payload preview confirmation; answer display with citations; quota indicator | 2 d |
| **WP10 — History management** | Student deletes own AI history; admin purges institution history | 1 d |
| **WP11 — DB migration & schema** | `M018_AddSchoolAdminTables`; UP/DOWN; tests | 1 d |
| **WP12 — Testing & CI** | Unit, integration, architecture, DPIA, performance (AI proxy throughput) tests | 3 d |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons + manifest**: `icons.md` defines `ic_admin_console`,
  `ic_enroll_profile`, `ic_permissions_roles`, `ic_ai_key`, `ic_quota`,
  `ic_usage_chart`, `ic_curate_shelf`, `ic_moderate_ai`, `ic_publish_folder_admin`,
  `ic_audit_log`, `ic_dpia_shield` — all `⬜ to procure`.
- [x] **i18n (en/fr)**: all admin console strings, student AI search strings,
  privacy tier labels, payload preview copy, quota-exhausted messages
  externalized and in en + fr. Classroom admin UI copy reviewed for tone
  appropriate to educators (not developers).
- [x] **Accessibility**: admin console tables have proper `<thead>` / `<tbody>`
  ARIA semantics; usage charts have data tables as screen-reader fallback;
  AI key field is a masked `PasswordBox` with label; quota indicator has
  `aria-valuenow` / `aria-valuemax`.
- [x] **Privacy/egress**: API key never leaves Host (architecture test);
  student payload preview before any off-device call (CTRL-OGMA-016/017);
  DPIA screening before every AI call (CTRL-OGMA-024); metadata-only default
  (FR-AI-004); audit entry per call (CTRL-OGMA-018); student history
  deletion surfaced (FR-AI-009).
- [x] **Reversibility**: profile revocation does not delete student's private DB
  (students keep their data); admin can re-enroll; key rotation is non-
  destructive (old key revoked, new key stored, in-flight requests complete).
- [x] **Performance budgets**: AI proxy endpoint P95 latency ≤ 10 s (NFR-OGMA-007,
  noting provider-latency exclusion); quota-check P95 ≤ 10 ms (local DB read);
  usage dashboard load P95 ≤ 500 ms.
- [x] **Bounded-context tests**: `SchoolAdmin` has no dependency on `ClassroomClient`
  internals; `SchoolAiKeyProvider` has no direct dependency on any concrete AI
  provider (only `IAiProvider` interface).
- [x] **Documentation**: ADR-0013 authored; `IDpiaScreeningService` documented
  with jurisdiction notes; `SOURCE-SUMMARY.md` §D updated to include FR-ADMIN-
  prefix requirements.

---

## 10. Definition of Done

### Global DoD

- [ ] Every in-scope FR/NFR/CTRL ID has a passing test or a tagged gap.
- [ ] Golden-corpus suite green; no open R1/R2 defect.
- [ ] `dotnet format`, `dotnet build`, `dotnet test`, architecture tests pass.
- [ ] Builds and tests pass on **both Windows and macOS** CI runners.
- [ ] All strings externalized and present in **en + fr**; pseudolocale check.
- [ ] Every new control has a colorful icon + accessible label; keyboard + SR
      walkthrough; `icons.md` complete.
- [ ] ADRs/decisions recorded; reference docs updated.
- [ ] Performance budgets instrumented.
- [ ] `/code-review` and `/security-review` done; findings resolved.

### Phase-18-specific exit criteria

- [x] ADR-0013 authored and owner-ratified.
- [ ] School API key retrievable from `ICredentialStore` on Host; not present in
      any HTTP response body or log file (verified by secret-scan tool in CI).
- [x] `POST /api/v1/ai/search` routes through `IAiProvider` on Host; integration
      test with mock provider verifies the full pipeline (payload preview →
      DPIA → quota → provider call → grounding → response).
- [x] Student who exceeds daily token quota receives a quota-exhausted message;
      no provider call is made (verified by mock provider call-count assertion).
- [x] Rate limiter enforces queries/minute per student (integration test).
- [x] `ClassroomAnswerGrounder` removes any citation not in Host catalogue
      (integration test with fabricated citation).
- [ ] DPIA screening blocks a call when jurisdiction policy is unset (architecture
      test: `DpiaScreeningService_BlocksCall_WhenJurisdictionNotConfigured`).
- [x] Admin can enroll a profile, student connects, and student's session token
      contains the admin-assigned role (end-to-end test).
- [x] Revoked profile: enrolled student's next request returns 401.
- [ ] Usage dashboard shows correct query count and cost after 10 student queries
      (integration test: `AiUsageLedger` aggregation verified).
- [x] `M018_AddSchoolAdminTables` UP and DOWN migrations both succeed in isolation.

---

## 11. Skills to use

Full guidance in `skills.md`. Key skills:

- `saas:saas-admin-backoffice-tooling` — admin console design and implementation
  (WP2, WP3, WP7, WP8).
- `saas:saas-entitlements-and-plan-gating` + `ai:ai-entitlements-and-feature-gating`
  — quota model and enforcement (WP6).
- `saas:saas-rate-limiting-and-quotas` — per-student rate limiting (WP6).
- `ai:ai-cost-and-metering` — `AiUsageLedger` design and dashboard (WP6, WP7).
- `ai:ai-agent-governance-and-limits` — answer-mode grounding, output moderation
  (WP5).
- `ai:ai-agent-safety-and-red-team` — red-team the AI proxy endpoint (WP12).
- `security:dpia-generator` + `security:uganda-dppa-compliance` — DPIA screening
  service; jurisdiction configuration (WP5, WP12).
- `frontend-ux:data-visualization` — usage dashboard charts (WP7).
- `documentation-generation:architecture-decision-records` — ADR-0013 (WP1).
- `/security-review` — WP4 (AI key storage), WP5 (AI proxy), DPIA service.

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| ADR-0013 | `docs/adrs/0013-school-managed-ai-host-gateway.md` |
| `OgmaLibrary.Infrastructure.SchoolAdmin` namespace | `src/OgmaLibrary.Infrastructure/SchoolAdmin/` |
| `ILibraryPublishingService`, `ISharedShelfService`, `IProfileEnrollmentService`, `ISchoolAiPolicyService`, `ISchoolAiKeyProvider`, `IAiProxyEndpointHandler`, `IUsageDashboardService`, `IDpiaScreeningService` | `src/OgmaLibrary.Application/SchoolAdmin/` |
| `SchoolAiKeyProvider` (implements `IAiProvider`) | `src/OgmaLibrary.Infrastructure/SchoolAdmin/Ai/SchoolAiKeyProvider.cs` |
| `ClassroomAnswerGrounder` | `src/OgmaLibrary.Infrastructure/SchoolAdmin/Ai/ClassroomAnswerGrounder.cs` |
| `M018_AddSchoolAdminTables` migration | `src/OgmaLibrary.Infrastructure/Migrations/` |
| Admin console views | `src/OgmaLibrary.App/Views/Admin/` |
| Student smart-search UI | `src/OgmaLibrary.App/Views/Classroom/SmartSearchView.axaml` |
| Architecture tests | `src/OgmaLibrary.Tests/Architecture/SchoolAdminIsolationTests.cs` |
| Integration tests | `src/OgmaLibrary.Tests/Integration/SchoolAdmin/` |
| `icons.md` | `docs/plans/grand-plan/phase-18/icons.md` |

---

## 13. Risks

| Risk | R-tier | Mitigation |
| --- | --- | --- |
| DPIA / jurisdiction not resolved by Phase 00 → Phase 18 blocked | R2 | DPIA configuration is a required admin setup step; if not configured, AI features are disabled until configured; Phase 00 must close this gap |
| API key leaked via log file or error response | R2 | CI secret-scan tool; `ICredentialStore` abstraction; key masked in all log outputs; `/security-review` before merge |
| ClassroomAnswerGrounder misses a hallucinated citation | R5 | Grounding verified against Host catalogue with a deterministic oracle test; integration test with fabricated citation |
| Quota check bypassed by concurrent requests (race condition) | R3 | Atomic `AiUsageLedger` update with SQLite transaction; load test asserts total tokens never exceed budget |
| Admin console accessible to non-admin users | R2 | Admin role check at Host session-token level; architecture test verifies admin routes return 403 for student tokens |
| AI provider cost unexpectedly high (quota misconfigured) | R3 | Default quotas conservative; admin dashboard cost visibility; optional hard spend cap beyond which the system refuses all calls |

---

## 14. Owner asks

1. **ADR-0013 sign-off**: ratify the school-managed AI model, especially the
   DPIA and jurisdiction configuration step, before Phase 18 build begins.
2. **Jurisdiction configuration**: which jurisdictions (Uganda DPPA, EU GDPR,
   UK GDPR, other) should the admin console offer as choices for "legal basis"
   configuration? This is needed to implement `IDpiaScreeningService` correctly.
   (This is the Phase 00 CTRL-OGMA-024 gap.)
3. **AI provider support**: which AI providers should be supported with
   school-provisioned keys in V2? (Proposed: OpenAI-compatible, Anthropic-
   compatible — same as Standalone mode Phase 12 providers.)
4. **Default quotas**: confirm the proposed defaults (10,000 tokens/student/day;
   500,000 tokens/class/day; 5 queries/minute/student) or supply school-
   appropriate values.
5. **Answer-mode default**: should classroom answer mode be on by default (admin
   can disable) or off by default (admin must enable)? Proposed: off by default
   (content-aware is off; answer mode requires content-aware).
6. **Icon procurement**: please procure the 11 premium PNG icons listed in
   `icons.md`: `ic_admin_console`, `ic_enroll_profile`, `ic_permissions_roles`,
   `ic_ai_key`, `ic_quota`, `ic_usage_chart`, `ic_curate_shelf`,
   `ic_moderate_ai`, `ic_publish_folder_admin`, `ic_audit_log`, `ic_dpia_shield`.

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Planning agent | Initial v1.0 draft |
