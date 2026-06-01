# Phase 17 â€” Tasks

Work packages and granular tasks for Client / Classroom Mode & Multi-User.
Task IDs: `P17-WP{n}-T{m}`.

---

## Work Package 1 â€” ADR-0012 & Architecture Scaffold

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP1-T1 | Author `docs/adrs/0012-classroom-identity-roles-private-state.md` covering: ProfileId UUID strategy, role taxonomy, private-state storage path, sync blob format, forward-compatibility with OQ-08 | Phase 16 DoD | 1 d | ADR-0012 |
| P17-WP1-T2 | Owner sign-off on ADR-0012 (Owner ask Â§14.1) | P17-WP1-T1 | 0 d (gate) | ADR-0012 |
| P17-WP1-T3 | Create `Application/ClassroomClient/` interfaces: `IClassroomModeService`, `IProfileService`, `ISyncService`, `IOfflineCacheService`, `IStudentPrivateRepository`, `ILibraryHostClient` | P17-WP1-T2 | 0.5 d | FR-CLIENT-001..013 |
| P17-WP1-T4 | Create `Infrastructure/ClassroomClient/` namespace; stub implementations; register in DI (mode-gated: only wired when mode = Client) | P17-WP1-T3 | 0.5 d | FR-CLIENT-001 |
| P17-WP1-T5 | Architecture tests: `ArchTests_ClassroomClient_HasNoLanHostServerDependency`, `ArchTests_ClassroomClient_HasNoDirectCatalogueWriteDependency`, `ArchTests_StandaloneMode_HasNoClassroomClientActive` | P17-WP1-T4 | 0.5 d | FR-CLIENT-013, bounded-context discipline |
| P17-WP1-T6 | Update `SOURCE-SUMMARY.md` Â§F and Â§C (new personas: student, teacher, guest, admin) | P17-WP1-T2 | 0.25 d | Documentation |

---

## Work Package 2 â€” Host Discovery & Certificate TOFU Client

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP2-T1 | Implement `IMdnsResolver`: listen for `_ogma-library._tcp.local` records; expose `IObservable<DiscoveredHost>` | P17-WP1-T4 | 0.75 d | FR-CLIENT-002 |
| P17-WP2-T2 | QR join URL parser: parse `ogma://host?addr=<ip>:<port>&fp=<sha256>`; extract and validate components | P17-WP1-T4 | 0.25 d | FR-CLIENT-002 |
| P17-WP2-T3 | Implement certificate TOFU client: on first connection, fetch Host cert; present `addr + fp` to user for confirmation; on accept, pin cert fingerprint in `ICredentialStore`; on subsequent connections, reject certs not matching pinned fingerprint | P17-WP2-T1 | 1 d | FR-CLIENT-003, CTRL-OGMA-001 |
| P17-WP2-T4 | Unit tests: `MdnsResolver_EmitsDiscoveredHost_OnServiceRecord`, `QrParser_ParsesValidJoinUrl`, `TofuClient_AcceptsPinnedCert`, `TofuClient_RejectsMismatchedCert_WithWarning` | P17-WP2-T3 | 0.5 d | FR-CLIENT-002..003 |
| P17-WP2-T5 | Integration test: end-to-end discovery + TOFU against a Phase 16 test Host fixture | P17-WP2-T4 | 0.5 d | FR-CLIENT-002..003 |

---

## Work Package 3 â€” Profile Management

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP3-T1 | Implement `IProfileService`: create profile (UUID v4, display name, role); select active profile; list profiles; delete profile (with confirmation) | P17-WP1-T4 | 0.75 d | FR-CLIENT-004 |
| P17-WP3-T2 | Store session token for active profile in `ICredentialStore` (key: `ogma.classroom.session.<profileId>`) | P17-WP3-T1 | 0.25 d | FR-CLIENT-004, CTRL-OGMA-001, NFR-CLIENT-003 |
| P17-WP3-T3 | Guest mode: `IProfileService.CreateGuestSession()` returns a transient profile with no DB writes; `ClearGuestSession()` removes all in-memory state | P17-WP3-T2 | 0.25 d | FR-CLIENT-012 |
| P17-WP3-T4 | Profile deletion: deletes private DB file; clears session token from credential store; confirmation dialog required | P17-WP3-T3 | 0.25 d | FR-CLIENT-005, reversibility |
| P17-WP3-T5 | Unit tests: `ProfileService_CreatesProfile_WithUuidV4`, `ProfileService_GuestSession_WritesNoDbRow`, `ProfileService_DeleteProfile_ClearsCredentialStore_And_DbFile` | P17-WP3-T4 | 0.5 d | FR-CLIENT-004, FR-CLIENT-012 |

---

## Work Package 4 â€” Catalogue Client View

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP4-T1 | Implement `ILibraryHostClient.GetCataloguePageAsync(page, pageSize, filter, sort)` â€” typed HTTP client wrapping Phase 16 `/api/v1/catalogue` | P17-WP2-T5 | 0.5 d | FR-CLIENT-006 |
| P17-WP4-T2 | Implement `ILibraryHostClient.GetBookAsync(bookId)` â€” single book projection | P17-WP4-T1 | 0.25 d | FR-CLIENT-006 |
| P17-WP4-T3 | Adapt existing `CatalogueGridViewModel` and `CatalogueListViewModel` to accept an `ICatalogueSource` abstraction (either local standalone catalogue or Host client) â€” this is the seam that keeps Standalone unaffected | P17-WP4-T2 | 0.75 d | FR-CLIENT-006, FR-CLIENT-013 |
| P17-WP4-T4 | Availability status: books that are unavailable on Host (file missing on server) shown with `ic_unavailable` chip â€” use existing availability model from Phase 05 adapted for remote | P17-WP4-T3 | 0.25 d | FR-CLIENT-006 |
| P17-WP4-T5 | Integration test: `CatalogueView_ShowsHostBooks_MatchingProjection`, `CatalogueView_Filter_AppliedCorrectly`, `CatalogueView_StandaloneSource_Unaffected` | P17-WP4-T4 | 0.5 d | FR-CLIENT-006, FR-CLIENT-013 |

---

## Work Package 5 â€” Reader Integration

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP5-T1 | Implement `ILibraryHostClient.GetPageRenderAsync(bookId, pageNumber, resolution)` â€” wraps Phase 16 page-render endpoint; caches response in `IOfflineCacheService` | P17-WP4-T1 | 0.5 d | FR-CLIENT-007 |
| P17-WP5-T2 | Implement `ILibraryHostClient.GetFileStreamAsync(bookId)` â€” wraps Phase 16 file-stream endpoint (fallback when Host is in file-stream mode) | P17-WP5-T1 | 0.25 d | FR-CLIENT-007 |
| P17-WP5-T3 | Adapt `IPageSource` abstraction in Reader context: local PDFium path (Standalone) vs Host page-render path (Client); reader surfaces unchanged | P17-WP5-T2 | 0.5 d | FR-CLIENT-007, FR-CLIENT-013 |
| P17-WP5-T4 | Resume position: read `StudentReadingProgress` for the active profileId + bookId + hostId on book open; write on page change (debounced 2 s) | P17-WP5-T3 | 0.25 d | FR-CLIENT-009 |
| P17-WP5-T5 | Integration test: `Reader_OpensBookFromHost_PageRenderMode`, `Reader_ResumesLastPage_FromStudentPrivateDb`, `Reader_NoPdfBytesStored_InCache_UnlessFileStreamMode` | P17-WP5-T4 | 0.5 d | FR-CLIENT-007, FR-CLIENT-009 |

---

## Work Package 6 â€” Per-Student Private Database

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP6-T1 | Implement `StudentDbContext` (EF Core Sqlite; separate from main catalogue DB); tables: `StudentReadingProgress`, `StudentAnnotations`, `StudentBookmarks`, `StudentAiHistory`, `StudentSyncState` | P17-WP1-T4 | 0.5 d | FR-CLIENT-005, FR-CLIENT-009 |
| P17-WP6-T2 | Database file path: `<sidecar>/classroom/profiles/<profileId>/private.db`; create directory tree on first open; set file permissions (NTFS ACL deny others / chmod 600) | P17-WP6-T1 | 0.25 d | FR-CLIENT-005, CTRL-OGMA-016 |
| P17-WP6-T3 | Implement `IStudentPrivateRepository`: CRUD for annotations, bookmarks, reading progress; soft-delete (`IsDeleted = 1`) for sync tombstones | P17-WP6-T2 | 0.75 d | FR-CLIENT-009 |
| P17-WP6-T4 | Schema migration within the private DB (managed in-code, not EF Core migration runner â€” the private DB is student-owned and potentially absent): `StudentDbInitializer.EnsureLatestSchema()` | P17-WP6-T3 | 0.25 d | FR-CLIENT-005, R1 |
| P17-WP6-T5 | Architecture test: `ArchTest_StudentDb_IsSeparateFile_PerProfile`, `ArchTest_MainCatalogueDb_NotModified_InClientMode` | P17-WP6-T4 | 0.25 d | FR-CLIENT-005, FR-CLIENT-013 |
| P17-WP6-T6 | Privacy test: spin two student profiles on same machine; write annotation as profile A; assert profile B's `IStudentPrivateRepository` cannot read profile A's annotation | P17-WP6-T5 | 0.25 d | FR-CLIENT-009, CTRL-OGMA-016, R2 |

---

## Work Package 7 â€” Offline Cache

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP7-T1 | Implement `IOfflineCacheService`: LRU on-disk cache at `<sidecar>/classroom/cache/`; keyed by `(hostId, resourceKey, eTag)`; configurable size limit (default 500 MB) | P17-WP1-T4 | 1 d | FR-CLIENT-008, NFR-CLIENT-002 |
| P17-WP7-T2 | Cache-miss path: on LAN connection, fetch from Host, store in cache; cache-hit path: serve from disk; conditional GET with `If-None-Match: <eTag>` for freshness | P17-WP7-T1 | 0.5 d | FR-CLIENT-008 |
| P17-WP7-T3 | Offline detection: `IClassroomModeService.IsOnline` observable; when offline, `IOfflineCacheService.GetAsync` returns cached entry or `CacheMissException`; `ILibraryHostClient` wraps all calls with offline fallback | P17-WP7-T2 | 0.5 d | FR-CLIENT-008 |
| P17-WP7-T4 | "Offline" status chip in Client mode toolbar: shown when `IsOnline = false`; `aria-live="polite"`; text + `ic_offline` icon | P17-WP7-T3 | 0.25 d | FR-CLIENT-008 |
| P17-WP7-T5 | Cache eviction: LRU eviction when cache > size limit; warning notification when cache > 80% of limit | P17-WP7-T4 | 0.25 d | FR-CLIENT-008, R4 |
| P17-WP7-T6 | Fault-injection test: `OfflineTest_LanDropMidSession_ReaderContinuesFromCache`; `OfflineTest_CatalogueAvailable_FromCache_WhenOffline`; `OfflineTest_NewBook_NotInCache_Returns_UnavailableState` | P17-WP7-T5 | 0.5 d | FR-CLIENT-008, R4 |

---

## Work Package 8 â€” Sync

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP8-T1 | Serialize private DB to sync blob: JSON snapshot of `StudentAnnotations`, `StudentBookmarks`, `StudentReadingProgress`, `StudentAiHistory` (with `IsDeleted` tombstones); compress with Brotli; encrypt AES-256-GCM with key derived from session token (HKDF-SHA256) | P17-WP6-T3, P17-WP7-T3 | 1 d | FR-CLIENT-010, CTRL-OGMA-016 |
| P17-WP8-T2 | Upload: `PUT /api/v1/profile/sync` on Host (Phase 16 endpoint stub â€” add to Phase 16 Host if not present, or note as a Host endpoint extension in this phase); Host stores blob opaquely by profileId | P17-WP8-T1 | 0.5 d | FR-CLIENT-010 |
| P17-WP8-T3 | Download + decrypt + merge: fetch blob from Host; decrypt; deserialize; merge with local DB using last-write-wins by `UpdatedAt`; detect conflicts (same row, different `Body`/content, same `UpdatedAt` within 1 s tolerance) | P17-WP8-T2 | 0.75 d | FR-CLIENT-010, FR-CLIENT-011 |
| P17-WP8-T4 | Conflict surfacing UI: dialog listing conflicting annotations with "Keep local" / "Keep server" choice per item; `StudentSyncState.ConflictCount` updated | P17-WP8-T3 | 0.5 d | FR-CLIENT-011 |
| P17-WP8-T5 | Sync settings: toggle (opt-in off by default); "Sync now" button; last-synced timestamp; `ic_sync` icon; "Sync on reconnect" option | P17-WP8-T4 | 0.25 d | FR-CLIENT-010 |
| P17-WP8-T6 | Integration test: `SyncTest_UploadBlob_HostStoresOpaquely`, `SyncTest_DownloadMerge_LastWriteWins`, `SyncTest_Conflict_Surfaces_StudentChoice`, `SyncTest_ConflictResolution_LocalWins_Persists` | P17-WP8-T5 | 0.75 d | FR-CLIENT-010..011, R2 |
| P17-WP8-T7 | Encryption unit test: `SyncBlob_EncryptedWithAES256GCM`, `SyncBlob_DecryptFailure_OnWrongKey` | P17-WP8-T6 | 0.25 d | FR-CLIENT-010, R2 |

---

## Work Package 9 â€” Client Mode UI

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP9-T1 | Mode switcher in Settings: `Radio` group `Standalone` / `Connect to Host`; mode change requires app restart confirmation | P17-WP1-T4 | 0.25 d | FR-CLIENT-001 |
| P17-WP9-T2 | Discovery screen (`DiscoveryView.axaml`): mDNS-discovered host list (auto-refreshing); manual entry field; QR-paste field; "Connect" button | P17-WP2-T1 | 0.75 d | FR-CLIENT-002 |
| P17-WP9-T3 | Enrollment / TOFU flow (`EnrollmentView.axaml`): fingerprint display; accept/reject; profile selection or creation | P17-WP2-T3, P17-WP3-T1 | 0.5 d | FR-CLIENT-003..004 |
| P17-WP9-T4 | Profile switcher (header bar): active profile badge (avatar initial + name + role chip); dropdown to switch profile | P17-WP3-T1 | 0.25 d | FR-CLIENT-004 |
| P17-WP9-T5 | Sync settings panel (in Settings > Classroom): sync toggle, "Sync now" button, last-synced time, conflict count badge | P17-WP8-T5 | 0.25 d | FR-CLIENT-010 |
| P17-WP9-T6 | i18n: all strings for views WP9-T1..T5 in `Strings.en.resx` + `Strings.fr.resx`; pseudolocale check | P17-WP9-T5 | 0.25 d | I18N-STRATEGY |
| P17-WP9-T7 | Accessibility walkthrough: keyboard-only navigation of discovery â†’ enrollment â†’ profile selection flow; SR audit | P17-WP9-T6 | 0.25 d | WCAG 2.2 AA, NFR-PROD-008 |

---

## Work Package 10 â€” Testing & CI

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P17-WP10-T1 | Standalone regression: run full golden-corpus suite in Standalone mode; assert no changes to main catalogue DB | All WPs | 0.5 d | FR-CLIENT-013, R1 |
| P17-WP10-T2 | Performance test: `CatalogueLoad_2000Books_P95LessThan2s` against Phase 16 test Host (warm LAN on loopback) | P17-WP4-T5 | 0.25 d | NFR-CLIENT-001 |
| P17-WP10-T3 | Performance test: `CachedPageRender_P95LessThan100ms` (10 cache-hit page requests) | P17-WP7-T6 | 0.25 d | NFR-CLIENT-002, NFR-OGMA-005 |
| P17-WP10-T4 | macOS CI: all tests green; file permissions test (`chmod 600` on private DB) | All WPs | 0.25 d | Cross-platform |
| P17-WP10-T5 | Windows CI: all tests green; NTFS ACL test on private DB | All WPs | 0.25 d | Cross-platform |
| P17-WP10-T6 | `/security-review` on WP2 (TOFU), WP3 (credential store), WP6 (private DB permissions), WP8 (sync blob encryption) | P17-WP8-T7 | 0.5 d | Phase DoD |
| P17-WP10-T7 | `/code-review` on all WPs; resolve findings | All WPs | 0.5 d | Phase DoD |