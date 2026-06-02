# Phase 18 — Tasks

Work packages and granular tasks for School Administration & Managed AI.
Task IDs: `P18-WP{n}-T{m}`.

---

## Work Package 1 — ADR-0013 & Admin Context Scaffold

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP1-T1 | Author `docs/adrs/0013-school-managed-ai-host-gateway.md` covering: key storage model, class-level gateway, privacy-tier enforcement, entitlements, DPIA service, minors' data boundary | Phase 17 DoD | 1 d | ADR-0013 |
| P18-WP1-T2 | Owner sign-off on ADR-0013 (Owner ask §14.1) | P18-WP1-T1 | 0 d (gate) | ADR-0013 |
| P18-WP1-T3 | Create `Application/SchoolAdmin/` interface files: `ILibraryPublishingService`, `ISharedShelfService`, `IProfileEnrollmentService`, `ISchoolAiPolicyService`, `ISchoolAiKeyProvider`, `IAiProxyEndpointHandler`, `IUsageDashboardService`, `IDpiaScreeningService` — all with XML doc comments | P18-WP1-T2 | 0.5 d | All FR-ADMIN-* |
| P18-WP1-T4 | Create `Infrastructure/SchoolAdmin/` namespace; stub implementations; DI registration (active only in Host mode + admin role) | P18-WP1-T3 | 0.5 d | FR-ADMIN-001..013 |
| P18-WP1-T5 | Architecture tests: `ArchTests_SchoolAdmin_HasNoClassroomClientInternalDependency`, `ArchTests_SchoolAiKeyProvider_HasNoDirectProviderDependency_ExceptViaIAiProvider`, `ArchTests_AdminRoutes_Require_AdminRole` | P18-WP1-T4 | 0.5 d | FR-ADMIN-004..005, bounded-context |
| P18-WP1-T6 | Update `SOURCE-SUMMARY.md` §D with FR-ADMIN-001..013 and §C with admin persona | P18-WP1-T2 | 0.25 d | Documentation |

---

## Work Package 2 — Library Publishing & Curation

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP2-T1 | Implement `ILibraryPublishingService`: publish folder (creates/updates `LibraryPublishSettings` row); unpublish; list published libraries with AI tier | P18-WP1-T4 | 0.5 d | FR-ADMIN-001 |
| P18-WP2-T2 | Integrate with Phase 16 catalogue projection endpoint: published libraries only included in `GET /api/v1/catalogue`; unpublished excluded | P18-WP2-T1, Phase 16 DoD | 0.5 d | FR-ADMIN-001 |
| P18-WP2-T3 | AI tier per library: admin sets `AiTier` in `LibraryPublishSettings`; default `MetadataOnly`; `ContentAware` requires confirmation dialog with privacy notice | P18-WP2-T2 | 0.25 d | FR-ADMIN-006 |
| P18-WP2-T4 | Implement `ISharedShelfService`: create/edit/delete shared shelf; assign/remove books; set visibility | P18-WP1-T4 | 0.75 d | FR-ADMIN-002 |
| P18-WP2-T5 | Integrate shared shelves with Phase 16 catalogue projection: `SharedShelves` returned in shelf list for enrolled students with matching visibility | P18-WP2-T4 | 0.5 d | FR-ADMIN-002 |
| P18-WP2-T6 | Integration tests: `PublishFolder_BooksAppearInClientCatalogue`, `UnpublishFolder_BooksAbsentFromClientCatalogue`, `SharedShelf_VisibleToEnrolledStudents`, `ContentAwareTier_RequiresAdminOptIn` | P18-WP2-T5 | 0.5 d | FR-ADMIN-001..002..006 |

---

## Work Package 3 — Profile Enrollment

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP3-T1 | Implement `IProfileEnrollmentService`: enroll profile (create `EnrolledProfiles` row; generate one-time enrollment token); edit display name and role; revoke (set `RevokedAt`) | P18-WP1-T4 | 0.75 d | FR-ADMIN-003 |
| P18-WP3-T2 | Enrollment token flow: token stored in `EnrolledProfiles.EnrollmentToken`; Phase 17 client exchanges token for session (extend `POST /api/v1/auth/session` to accept enrollment token + profileId); token nulled after first use | P18-WP3-T1, Phase 16 WP7 | 0.5 d | FR-ADMIN-003 |
| P18-WP3-T3 | Admin-facing enrollment UI: table of enrolled profiles; "Enroll" button; edit role; "Revoke" with confirmation; generate enrollment token (display + QR) | P18-WP3-T2 | 0.5 d | FR-ADMIN-003 |
| P18-WP3-T4 | Birth year field: optional per profile; used by `IDpiaScreeningService` to determine minor status; if absent, default to "treat as minor" (conservative) | P18-WP3-T3 | 0.25 d | CTRL-OGMA-024 |
| P18-WP3-T5 | Integration tests: `EnrolledProfile_CanConnect_WithToken`, `RevokedProfile_Returns401`, `EnrollmentToken_NulledAfterFirstUse`, `NoBirthYear_TreatedAsMinor` | P18-WP3-T4 | 0.5 d | FR-ADMIN-003, CTRL-OGMA-024 |

---

## Work Package 4 — School AI Key Management

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP4-T1 | Admin console AI key panel: masked `PasswordBox` for key entry; "Save key" button; "Rotate key" button; "Test connection" button; key status indicator (set / not set) | P18-WP1-T4 | 0.5 d | FR-ADMIN-004, CTRL-OGMA-001 |
| P18-WP4-T2 | Implement `ISchoolAiKeyProvider` (implements `IAiProvider`): `GetKeyAsync()` reads from `ICredentialStore`; key never exposed in any public method return; key stored at `ICredentialStore` key `ogma.school.ai.key.<providerId>` | P18-WP4-T1 | 0.5 d | FR-ADMIN-004, CTRL-OGMA-001 |
| P18-WP4-T3 | Key write path: admin UI input → zero-copy write to `ICredentialStore` → memory zeroed immediately; no intermediate string variable persists | P18-WP4-T2 | 0.25 d | CTRL-OGMA-001, R2 |
| P18-WP4-T4 | Architecture test: `SchoolAiKeyProvider_NeverReturnsKeyInPlainText` (assert no property or method return value contains the stored key string) | P18-WP4-T3 | 0.25 d | FR-ADMIN-004, R2 |
| P18-WP4-T5 | Secret-scan CI step: add `truffleHog` or `gitleaks` scan to CI pipeline; assert no API key patterns appear in any source file, log output, or HTTP response recorded in tests | P18-WP4-T4 | 0.25 d | FR-ADMIN-004, R2 |
| P18-WP4-T6 | "Test connection" endpoint: `POST /admin/ai/test-connection` — uses `ISchoolAiKeyProvider` to send a minimal probe request; returns success/failure without echoing the key | P18-WP4-T5 | 0.25 d | FR-ADMIN-004 |
| P18-WP4-T7 | Unit tests: `SchoolAiKeyProvider_ReadFromCredentialStore`, `KeyEntry_MemoryZeroed_AfterSave`, `TestConnection_UsesKeyProvider_NotRawKey` | P18-WP4-T6 | 0.25 d | FR-ADMIN-004, CTRL-OGMA-001 |

---

## Work Package 5 — AI Proxy Endpoint

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP5-T1 | Add `POST /api/v1/ai/search` to Phase 16 Host listener: authenticated (session token), student role required | P18-WP4-T2, Phase 16 WP7 | 0.25 d | FR-ADMIN-005 |
| P18-WP5-T2 | Implement `IAiProxyEndpointHandler.HandleAsync()` pipeline: (1) resolve active library AI tier; (2) build payload per tier; (3) generate payload preview; (4) send preview to client (SSE or JSON response with `requireConfirmation: true`); (5) await student confirmation | P18-WP5-T1 | 1 d | FR-ADMIN-006, FR-ADMIN-007 |
| P18-WP5-T3 | DPIA screening: call `IDpiaScreeningService.CheckAsync(profileId, tier, payloadScope)` before forwarding to provider; on disqualification: return 451 (Unavailable for Legal Reasons) with human-readable message | P18-WP5-T2 | 0.5 d | CTRL-OGMA-024, R2 |
| P18-WP5-T4 | Quota check: before DPIA, call `ISchoolAiPolicyService.CheckAndDecrementQuota(profileId, estimatedTokens)` atomically; on quota exhaustion return quota-exceeded response (HTTP 429 with body `reason: QuotaExhausted`) without calling provider | P18-WP5-T3 | 0.5 d | FR-ADMIN-008 |
| P18-WP5-T5 | Rate limit: sliding-window per `profileId` (5 queries/min default); enforce before quota check; return `429 TooManyRequests` with `Retry-After` on limit breach | P18-WP5-T4 | 0.5 d | FR-ADMIN-009 |
| P18-WP5-T6 | Provider call: forward approved payload to `IAiProvider.QueryAsync()`; capture `tokensUsed`, `estimatedCostUsd` from response metadata | P18-WP5-T5 | 0.25 d | FR-ADMIN-005, FR-AI-010 |
| P18-WP5-T7 | `ClassroomAnswerGrounder`: post-process AI response; extract citation references; verify each `bookId` exists in Host catalogue; remove non-existent citations; if all citations removed, add a note "No local evidence found" | P18-WP5-T6 | 0.75 d | FR-ADMIN-011, FR-AI-008 |
| P18-WP5-T8 | Audit: write `AuditEvents` row: `profileId`, `tier`, `queryHash` (SHA-256 of query, not raw text), `tokensUsed`, `estimatedCostUsd`, `dpiaResult`, `timestamp`; write to `AiUsageLedger` atomically | P18-WP5-T7 | 0.5 d | CTRL-OGMA-018, FR-AI-010 |
| P18-WP5-T9 | Integration test: end-to-end `POST /api/v1/ai/search` with mock provider — verify pipeline: payload preview sent → student confirms → DPIA pass → quota check → mock provider called once → response grounded → audit written | P18-WP5-T8 | 0.75 d | FR-ADMIN-005..011, CTRL-OGMA-018/024 |
| P18-WP5-T10 | Integration test: `AiProxy_QuotaExhausted_NoProviderCall` — assert mock provider call count = 0 when quota = 0 | P18-WP5-T9 | 0.25 d | FR-ADMIN-008, R3 |
| P18-WP5-T11 | Integration test: `AiProxy_DpiaNotConfigured_BlocksCall` — assert 451 when `IDpiaScreeningService` returns `Disqualified` | P18-WP5-T10 | 0.25 d | CTRL-OGMA-024, R2 |
| P18-WP5-T12 | Integration test: `ClassroomAnswerGrounder_RemovesFabricatedCitation` — AI response with non-existent bookId → citation stripped from output | P18-WP5-T11 | 0.25 d | FR-ADMIN-011, R5 |

---

## Work Package 6 — Entitlements & Quotas

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP6-T1 | Implement `ISchoolAiPolicyService`: get/set per-student entitlement; get/set class-wide entitlement; atomic decrement `AiUsageLedger` with SQLite transaction (no race on concurrent requests) | P18-WP1-T4 | 0.75 d | FR-ADMIN-008, FR-ADMIN-009 |
| P18-WP6-T2 | Rate-limit sliding window: in-memory `ConcurrentDictionary<profileId, TokenBucket>` (resets on Host restart; sufficient for per-session rate limiting) | P18-WP6-T1 | 0.25 d | FR-ADMIN-009 |
| P18-WP6-T3 | Admin quota management UI: table of per-student entitlements; class-wide policy row; edit daily budget, rate limit; reset daily usage | P18-WP6-T2 | 0.5 d | FR-ADMIN-008 |
| P18-WP6-T4 | Quota exhaustion response: HTTP 429 body `{ "reason": "QuotaExhausted", "resetAt": "<UTC date>" }`; student-facing: friendly message "You've reached today's AI search limit. Try again tomorrow." | P18-WP6-T3 | 0.25 d | FR-ADMIN-008 |
| P18-WP6-T5 | Unit test: `QuotaDecrement_Atomic_NoConcurrentOverrun` — 20 concurrent requests against quota of 15 → exactly 15 succeed, 5 get 429 | P18-WP6-T4 | 0.25 d | FR-ADMIN-008, R3 |

---

## Work Package 7 — Usage Dashboard

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP7-T1 | Implement `IUsageDashboardService.GetSummaryAsync(dateRange)`: aggregate `AiUsageLedger` by profileId; return list of `UsageSummary { profileId, displayName, queryCount, tokensUsed, estimatedCostUsd, quotaPercent }` | P18-WP5-T8 | 0.5 d | FR-ADMIN-010 |
| P18-WP7-T2 | Dashboard view (`AdminUsageDashboardView.axaml`): bar chart (queries by student); line chart (daily spend over last 30 days); per-student drill-down; `ic_usage_chart` icon | P18-WP7-T1 | 0.75 d | FR-ADMIN-010 |
| P18-WP7-T3 | Screen-reader fallback for charts: each chart has a `DataGrid` alternative (hidden by default; exposed via "Show as table" button with `aria-controls`) | P18-WP7-T2 | 0.25 d | WCAG 2.2 AA, NFR-PROD-008 |
| P18-WP7-T4 | Integration test: `UsageDashboard_ReturnsCorrectCounts_After10StudentQueries` — 10 AI queries from 2 students → dashboard shows correct per-student counts and aggregate cost | P18-WP7-T3 | 0.25 d | FR-ADMIN-010 |

---

## Work Package 8 — Audit Log Viewer

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP8-T1 | Audit log viewer (`AdminAuditLogView.axaml`): filterable table of `AuditEvents` by date range, profileId, action type; paginated (50 rows/page) | P18-WP5-T8 | 0.5 d | FR-ADMIN-010, CTRL-OGMA-018 |
| P18-WP8-T2 | CSV export: "Export" button writes filtered `AuditEvents` to a CSV file chosen via OS file-save dialog; fields: timestamp, profileId, action, resource, status, estimatedCostUsd | P18-WP8-T1 | 0.25 d | FR-ADMIN-010 |
| P18-WP8-T3 | Integration test: `AuditLogViewer_FilterByProfileId_ShowsOnlyMatchingRows` | P18-WP8-T2 | 0.25 d | CTRL-OGMA-018 |

---

## Work Package 9 — Student Smart-Search UI (Client Side)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP9-T1 | Student smart-search view (`SmartSearchView.axaml`): natural-language query bar; `ic_moderate_ai` icon; "Search" button; privacy tier label (read from Host session context) | P18-WP5-T2 | 0.5 d | FR-ADMIN-005, FR-ADMIN-007 |
| P18-WP9-T2 | Payload preview step: before submitting to Host, display preview panel (matching Phase 12 privacy-tier UX); student can cancel; localized in en/fr | P18-WP9-T1 | 0.5 d | FR-ADMIN-007, CTRL-OGMA-016 |
| P18-WP9-T3 | Answer display: render AI response with grounded citations (book title + page); non-cited response shows "No local evidence found" | P18-WP9-T2 | 0.5 d | FR-ADMIN-011, FR-AI-008 |
| P18-WP9-T4 | Quota indicator: progress bar + `{tokensUsed}/{dailyBudget}` label; `aria-valuenow`, `aria-valuemax`; updates after each query | P18-WP9-T3 | 0.25 d | FR-ADMIN-008 |
| P18-WP9-T5 | i18n: all student AI search view strings in en + fr | P18-WP9-T4 | 0.25 d | I18N-STRATEGY |
| P18-WP9-T6 | Accessibility walkthrough: keyboard-only flow (query bar → preview → confirm → results); SR announces tier label, quota indicator, citation list | P18-WP9-T5 | 0.25 d | WCAG 2.2 AA |

---

## Work Package 10 — History Management

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP10-T1 | Student history deletion: Settings > Privacy > AI History; "Delete all" button (confirmation required); clears `StudentAiHistory` in student's private DB | P18-WP5-T8 | 0.25 d | FR-ADMIN-012, FR-AI-009 |
| P18-WP10-T2 | Admin institution-wide purge: admin console "Purge all AI history" action (confirmation + name-entry confirmation); deletes all `AiQueryHistory` rows and clears all `AiUsageLedger` for the institution | P18-WP10-T1 | 0.25 d | FR-ADMIN-013 |
| P18-WP10-T3 | Integration tests: `StudentHistoryDeletion_ClearsPrivateDb_PreservesOtherStudentHistory`, `AdminPurge_ClearsAllInstitutionHistory` | P18-WP10-T2 | 0.25 d | FR-ADMIN-012..013 |

---

## Work Package 11 — DB Migration & Schema

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP11-T1 | Add EF Core entity models for `M018_AddSchoolAdminTables` tables | P18-WP1-T4 | 0.25 d | FR-ADMIN-001..003 |
| P18-WP11-T2 | Generate migration `M018_AddSchoolAdminTables`; UP creates 6 tables; DOWN drops them in reverse-FK order | P18-WP11-T1 | 0.25 d | NFR-PROD-012, R1 |
| P18-WP11-T3 | Migration isolation test: UP on clean DB → verify schema; DOWN → clean; re-UP → re-verify | P18-WP11-T2 | 0.25 d | R1 |
| P18-WP11-T4 | Seed default `SchoolAiEntitlements` rows for existing `EnrolledProfiles` on migration UP | P18-WP11-T3 | 0.25 d | FR-ADMIN-008 |

---

## Work Package 12 — Testing & CI

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P18-WP12-T1 | Red-team AI proxy endpoint: `ai:ai-agent-safety-and-red-team` — attempt prompt injection via query; verify `ClassroomAnswerGrounder` rejects fabricated citations; verify DPIA cannot be bypassed | All WP5 | 0.5 d | FR-ADMIN-011, R2 |
| P18-WP12-T2 | DPIA screening service unit tests: `DpiaScreening_MinorProfile_MetadataOnlyApproved`, `DpiaScreening_MinorProfile_ContentAware_RequiresJurisdictionConfig`, `DpiaScreening_NoBirthYear_TreatedAsMinor` | P18-WP5-T3 | 0.5 d | CTRL-OGMA-024, R2 |
| P18-WP12-T3 | Architecture test: `AdminRoutes_Return403_ForStudentToken` — student session token against `POST /admin/*` endpoint → 403 | P18-WP1-T5 | 0.25 d | FR-ADMIN-003, R2 |
| P18-WP12-T4 | Secret scan CI: `truffleHog`/`gitleaks` scan passes with no AI key pattern detected in source or test logs | P18-WP4-T5 | 0.25 d | FR-ADMIN-004, R2 |
| P18-WP12-T5 | Performance test: `AiProxy_P95_Latency_LessThan10s_ExcludingProviderLatency` (mock provider returns instantly; overhead ≤ 1 s P95) | P18-WP5-T9 | 0.25 d | NFR-OGMA-007 |
| P18-WP12-T6 | `/security-review` on WP4 (key storage), WP5 (AI proxy, DPIA), WP6 (quota race) | P18-WP12-T1 | 0.5 d | Phase DoD |
| P18-WP12-T7 | `/code-review` on all WPs | All WPs | 0.5 d | Phase DoD |
