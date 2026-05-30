# Phase 17 — Test Plan

Test plan for Client / Classroom Mode & Multi-User. Follows the 9-layer model
from `SOURCE-SUMMARY.md §J`. R1 and R2 defects are unwaivable release blockers.

---

## 1. Applicable test layers

| Layer | Applies? | Notes |
| --- | --- | --- |
| 1. Domain | No | No new domain entities |
| 2. Infrastructure | Yes | TOFU client, profile service, private DB, cache, sync |
| 3. PDF | Partial | Reader integration in Client mode (page-render source path) |
| 4. Search | No | No new search in this phase |
| 5. AI | No | No AI in this phase |
| 6. UI | Yes | Discovery, enrollment, profile, sync, offline chip views |
| 7. 3D | No | No 3D changes |
| 8. Performance | Yes | Catalogue load P95, cached page P95 |
| 9. Packaging | No | No packaging changes |

Additional: **Architecture tests** (ClassroomClient isolation), **Privacy
tests** (cross-profile isolation), **Fault-injection tests** (offline scenario).

---

## 2. Test environment

- **Test fixtures:**
  - `LanHostTestFixture` (from Phase 16): Phase 16 Host HTTPS endpoint, loopback.
  - `ClassroomClientTestFixture`: starts Client mode against `LanHostTestFixture`;
    provides two enrolled student profiles (Alice, Bob) for cross-profile tests.
  - `OfflineSimulationFixture`: wraps `ILibraryHostClient` with a
    `NetworkFaultProxy` that can drop all connections on demand.
- **Golden corpus:** used for reader integration tests (page-render oracle) and
  catalogue projection integrity tests.
- **Platforms:** all tests on Windows CI and macOS CI runners.

---

## 3. Unit tests

### MdnsResolver

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `MdnsResolver_EmitsDiscoveredHost_OnServiceRecord` | Observable emits `DiscoveredHost` with correct addr + port + fp on mock DNS-SD record | R5 |
| `MdnsResolver_RemovesHost_OnServiceGone` | Host removed from observable when service deregisters | R5 |

### TofuClient

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `TofuClient_AcceptsPinnedCert` | Cert with matching fingerprint → connection proceeds | R2 |
| `TofuClient_RejectsMismatchedCert_WithWarning` | Cert with different fingerprint → `CertFingerprintMismatchException` + warning logged | R2 |
| `TofuClient_PinsOnAccept_PersistsToCredentialStore` | After TOFU accept, fingerprint retrievable from `ICredentialStore` | R2 |

### OfflineCacheService

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `Cache_StoresAndRetrievesEntry_ByKey` | Write then read returns same bytes | R4 |
| `Cache_Evicts_LRU_WhenOverSizeLimit` | After filling to 110% of limit, oldest entry is absent | R4 |
| `Cache_HonorsETag_SkipsNetworkFetch_OnMatch` | Conditional GET with matching `eTag` → cache hit, no network call | R5 |
| `Cache_ReturnsNull_OnCacheMiss_WithoutNetwork` | Miss when offline and not cached → `CacheMissException` | R4 |

### SyncService

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `SyncBlob_SerializesAllPrivateDbTables` | Blob contains all annotation/bookmark/progress rows | R1 |
| `SyncBlob_EncryptedWithAES256GCM` | Blob bytes are not plaintext JSON; decryption with correct key succeeds | R2 |
| `SyncBlob_DecryptFailure_OnWrongKey` | Decryption with wrong key throws `AuthenticatedEncryptionException` | R2 |
| `MergeStrategy_LastWriteWins_ByUpdatedAt` | Two versions of same row → higher `UpdatedAt` wins | R1 |
| `MergeStrategy_ConflictDetected_WhenSameUpdatedAt_DifferentContent` | Produces `AnnotationConflict` record | R1 |

---

## 4. Integration tests

All integration tests use `ClassroomClientTestFixture` (two enrolled profiles: Alice = student, Bob = student; teacher profile Carol).

### Host discovery & connection

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `Discovery_MdnsHost_AppearsInList_Within5s` | `IMdnsResolver` observable emits Host within 5 s on loopback subnet | R5 |
| `Discovery_ManualEntry_Connects_Successfully` | Manual IP:port entry → authenticated session | R5 |
| `Discovery_QrJoinUrl_Parsed_And_Connected` | Paste QR join URL → addr + fp extracted → TOFU flow initiated | R5 |

### Profile & enrollment

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `Enrollment_StudentProfile_Created_WithUuidV4` | Profile row in Alice's local DB; `Id` matches UUID v4 pattern | R5 |
| `Enrollment_GuestSession_WritesNoDbRow` | After guest session + logout: no profile row in any DB | R5 |
| `ProfileDeletion_ClearsDbFile_And_CredentialStore` | After delete: private DB file absent; credential store entry absent | R1 |

### Cross-profile isolation (R2 — privacy-critical)

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `Alice_WritesAnnotation_Bob_CannotRead_It` | Annotation written in Alice's session → IStudentPrivateRepository opened with Bob's profileId → returns empty | R2 |
| `PrivateDb_FilePermissions_DenyOtherUsers_Windows` | Alice's `private.db` NTFS DACL denies read to test user "Bob-OS" | R2 |
| `PrivateDb_FilePermissions_chmod600_macOS` | Alice's `private.db` mode bits = `0600` | R2 |

### Reader integration

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `Reader_OpensBook_FromHost_PageRenderMode_MatchesOracle` | Page 1 PNG matches golden oracle hash | R5 |
| `Reader_ResumesLastPage_FromStudentPrivateDb` | Close book at page 7 → reopen → starts at page 7 | R5 |
| `Reader_NoPdfBytesInCache_WhenPageRenderMode` | Cache directory contains no `.pdf` files; only `.png`/`.webp` | R2 |

### Offline fault injection

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `OfflineTest_LanDropMidSession_ReaderContinuesCachedPages` | `NetworkFaultProxy.DropAll()` → reader continues without error for pages already cached | R4 |
| `OfflineTest_OfflineChip_ShowsWhenDisconnected` | `IsOnline = false` → `ic_offline` chip visible; `aria-live` announces "Offline" | R4 |
| `OfflineTest_NewBook_NotInCache_ShowsUnavailable` | Offline + book not in cache → book card shows "Unavailable offline" | R5 |
| `OfflineTest_Reconnect_SyncPrompt_Shown` | Reconnect after offline → sync prompt appears (if sync opted in) | R5 |

### Sync

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `SyncTest_UploadBlob_HostStoresOpaquely` | `PUT /api/v1/profile/sync` returns 204; subsequent download returns same blob hash | R1 |
| `SyncTest_DownloadMerge_LastWriteWins` | Local annotation `UpdatedAt = T+1`, server `UpdatedAt = T` → local wins after merge | R1 |
| `SyncTest_Conflict_Surfaces_StudentChoice` | Same row, same `UpdatedAt`, different body → conflict dialog rendered | R2 |
| `SyncTest_ConflictResolution_LocalWins_Persists` | Student chooses "Keep local" → local body persists after next sync | R1 |

### Standalone regression

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `StandaloneMode_FullGoldenCorpus_PassesUnchanged` | All Phase 00–15 golden-corpus tests pass with mode = Standalone | R1 |
| `StandaloneMode_MainCatalogueDb_NotModified_AfterClientModeSwitch` | Catalogue DB byte-for-byte identical before and after Client mode activation and deactivation | R1 |

---

## 5. Architecture tests

File: `src/OgmaLibrary.Tests/Architecture/ClassroomClientIsolationTests.cs`

```csharp
[Fact] ArchTests_ClassroomClient_HasNoLanHostServerDependency()
    // ClassroomClient types do not reference LanHost server internals.

[Fact] ArchTests_ClassroomClient_HasNoDirectCatalogueWriteDependency()
    // ClassroomClient types do not write to the main ICatalogueRepository.

[Fact] ArchTests_StandaloneMode_HasNoClassroomClientActive()
    // When IClassroomModeService.Mode = Standalone, no ClassroomClient
    // infrastructure is instantiated (DI scope not activated).

[Fact] ArchTests_StudentDb_PathContains_ProfileId()
    // StudentDbContext connection string includes the active profileId segment.
```

---

## 6. Performance tests

| Test | Fixture | Threshold | Risk tier |
| --- | --- | --- | --- |
| `CatalogueLoad_2000Books_P95LessThan2s` | 2,000-book Host corpus; single client; 50 paginated requests | P95 ≤ 2 s | R3 |
| `CachedPageRender_P95LessThan100ms` | 10 cache-hit page requests (pages pre-warmed in cache) | P95 ≤ 100 ms | R3 (NFR-OGMA-005) |

---

## 7. UI / accessibility tests

- Keyboard-only navigation: discovery list → select host → TOFU dialog →
  accept → profile creation → catalogue view. All steps reachable without mouse.
- Screen-reader: VoiceOver + Narrator announce: discovered host names, TOFU
  fingerprint hex (chunked), profile role chip, "Offline" chip (`aria-live`),
  sync badge count.
- Pseudolocale: all Phase 17 strings render without overflow in `qps-ploc` locale.

---

## 8. CI integration

Same dual-runner (Windows + macOS) model as Phase 16. The
`ClassroomClientTestFixture` starts a Phase 16 `LanHostTestFixture` in-process
on a loopback address to avoid real network dependency in CI.
