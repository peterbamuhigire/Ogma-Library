# Phase 22 — Test Plan

> Which test layers apply, fixtures, oracles, and the Phase 22 slice of the
> packaging, signing, migration, and trust-chain gates.

---

## 1. Test layers in scope

| Layer | Applied | Notes |
| --- | --- | --- |
| 1. Domain | No | No domain changes |
| 2. Infrastructure | Yes | `SandboxedFileSystemService`, `IBookmarkStore`, `VelopackUpdateService` |
| 3. PDF | No | Reader unchanged |
| 4. Search | No | Search unchanged |
| 5. AI | No | AI gateway unchanged |
| 6. UI | Partial | MAS "LAN Host not available" notice; accessibility of the notice |
| 7. 3D | No | 3D unchanged |
| 8. Performance | Partial | Release-build benchmark re-run (regression check) |
| 9. Packaging | Primary | Signing verification, Velopack trust chain, delta update, MSIX/DMG/MAS, migration rollback |

---

## 2. Packaging tests (Layer 9 — primary)

### 2a. Signing verification tests

| Test | Platform | Command | Oracle |
| --- | --- | --- | --- |
| Authenticode signature | Windows | `signtool verify /pa /v OgmaLibrary.exe` | Exit 0; certificate CN matches expected subject |
| MSIX signature | Windows | `signtool verify /pa /v OgmaLibrary.msix` | Exit 0 |
| Developer-ID signature | macOS | `codesign -v --strict OgmaLibrary.app` | Exit 0; no `modified` warnings |
| Developer-ID deep verify | macOS | `codesign -v --deep --strict OgmaLibrary.app` | Exit 0 |
| Gatekeeper | macOS | `spctl -a -t exec -vv OgmaLibrary.app` | Output contains `accepted`; source = `Developer ID` |
| Notarization staple | macOS | `xcrun stapler validate OgmaLibrary.app` | `The validate action worked!` |
| MAS signature | macOS | `codesign -v --strict OgmaLibrary.app` (MAS build) | Exit 0; `authority=Apple Mac OS Application Signing` |

### 2b. Velopack trust-chain tests

**Project:** `OgmaLibrary.Tests.Packaging`

| Test method | Description | Oracle |
| --- | --- | --- |
| `UpdateService_ValidDescriptor_AcceptsUpdate` | Fetch a correctly signed `releases-channel.json`; call `CheckForUpdateAsync` | Returns a non-null `UpdateInfo`; no exception |
| `UpdateService_TamperedDescriptor_Rejects` | Modify one byte of the descriptor body after signing; call `CheckForUpdateAsync` | Throws `UpdateSignatureException`; no update applied |
| `UpdateService_TamperedBinary_Rejects` | Provide a valid descriptor pointing to a binary with a corrupted byte; call `DownloadUpdateAsync` | Throws `UpdateHashMismatchException`; corrupted file deleted |
| `UpdateService_ValidDescriptorAndBinary_Applies` | Full update path end-to-end with test artifacts | Update applied; new version number reported by `IApplicationVersion` |
| `DeltaUpdate_ProducesIdenticalBinary` | Install version N; apply delta to N+1; compute SHA-256 | SHA-256 of updated binary == SHA-256 of fresh N+1 binary |

### 2c. MAS sandbox tests

| Test method | Description | Oracle |
| --- | --- | --- |
| `SandboxedFileSystem_BookmarkPersisted` | Simulate `NSOpenPanel` grant → `IBookmarkStore.PersistAsync(path)` | `SqliteBookmarkStore` contains a non-null bookmark blob for the path |
| `SandboxedFileSystem_BookmarkResolved_AfterRestart` | Persist bookmark; restart app context; call `IFileSystemService.AcquireAccessAsync(path)` | Returns a valid `IDisposable` scope; file read succeeds |
| `SandboxedFileSystem_BookmarkResolved_NonSandboxBuild` | Same call on the non-MAS build | `DefaultFileSystemService` is registered (not `SandboxedFileSystemService`); no bookmark required |
| `SandboxedFileSystem_InvalidBookmark_ThrowsDescriptive` | Pass an expired/revoked bookmark blob | Throws `SecurityScopedBookmarkException` with a localized message |
| `LanHostNotAvailable_Notice_Localized` | Open Settings in MAS build; navigate to Library Sharing | UI element with key `Settings.LibrarySharing.MasNotAvailable` is visible in all 5 locales |

### 2d. MSIX installation test

Manual test (recorded in `docs/distribution/WINDOWS-STORE.md`):
1. Clean Windows 10 VM (no developer mode).
2. Install `OgmaLibrary-<version>-x64.msix` via double-click.
3. App launches; no SmartScreen warning (with EV cert) or SmartScreen accepted.
4. Select a library root; scan 100 PDFs; navigate; open a book; search.
5. Uninstall via Add/Remove Programs; confirm no residual files in user profile
   (catalogue and sidecar are in `%LocalAppData%\OgmaLibrary` — these persist
   intentionally after uninstall; documented).

### 2e. DMG installation test

Manual test (recorded in `docs/distribution/MAC-APP-STORE.md`):
1. Clean macOS 13 VM.
2. Mount `OgmaLibrary-<version>-macos.dmg`; drag to `/Applications`.
3. Open app; Gatekeeper passes silently (no "unidentified developer" dialog).
4. Select library root; scan; open book; search.

---

## 3. Migration rollback tests (Layer 9 / Infrastructure)

**Project:** `OgmaLibrary.Tests.Packaging` (shared with packaging tests)

For each consecutive schema-version pair (N, N+1):

| Test method | Steps | Oracle |
| --- | --- | --- |
| `Migrate_RollbackTest_V<N>_To_V<N+1>_BackupCreated` | Run migration N→N+1; check sidecar for backup | `ogma_db_backup_v<N>.sqlite` exists; SHA-256 matches pre-migration catalogue |
| `Migrate_RollbackTest_V<N>_To_V<N+1>_RollbackRestores` | Inject `WriteBackBeforeFlush` fault at migration apply; trigger rollback; re-open catalogue | All books, annotations, reading progress, shelf memberships intact; schema version = N |
| `Migrate_RollbackTest_V<N>_To_V<N+1>_DataSurvives` | Run migration N→N+1 successfully; verify data | All pre-migration user data accessible in the N+1 schema |

All rollback tests are classified R1. They run in the `OgmaLibrary.Tests.Packaging`
project with `DisableTestParallelization = true`.

---

## 4. Infrastructure tests (Layer 2)

| Test class | What it tests |
| --- | --- |
| `VelopackUpdateServiceTests` | Trust-chain verification (see §2b above) |
| `BookmarkStoreTests` | SQLite round-trip: persist bookmark → restart context → resolve bookmark |
| `SandboxedFileSystemServiceTests` | See §2c above |

---

## 5. UI tests (Layer 6 — partial)

| Test | Platform | Oracle |
| --- | --- | --- |
| `MasLanHostNotice_IsVisible_MasBuild` | macOS (MAS build) | `Settings.LibrarySharing.MasNotAvailable` label is present and visible |
| `MasLanHostNotice_IsAbsent_DirectBuild` | macOS (direct build) | The "not available" notice is absent; LAN Host toggle is visible |
| `MasLanHostNotice_IsAccessible` | macOS (MAS build) | Notice has accessible name; keyboard-reachable (Tab reaches it); VoiceOver announces it |

---

## 6. Performance regression check (release build)

After the release build is produced (`PublishSingleFile`, `SelfContained`), run
the Phase 20 benchmark suite in `ShortRun` configuration against the release
artifact (not the debug build). Oracle: all NFR-OGMA-001..009 budgets remain
within 10% of the Phase 20 baseline.

This confirms that AOT compilation, tree-shaking, or packaging changes did not
introduce unexpected performance regressions.

---

## 7. Open-source readiness check

Before Phase 22 closes, verify:

| Check | Oracle |
| --- | --- |
| `LICENSE` file at repo root | Exists; contains the owner-selected license text (MIT or Apache 2.0); matches the license declared in `Package.appxmanifest` and `Info.plist` |
| `CONTRIBUTING.md` at repo root | Exists; references the CLA/DCO mechanism; up to date with the Phase 22 contribution process |
| `CODE_OF_CONDUCT.md` at repo root | Exists; Contributor Covenant 2.1 or equivalent |
| No secrets in repository | `git log --all --oneline -- '*.pem' '*.p12' '*.pfx'` returns empty; `gitleaks` scan passes |
| `CHANGELOG.md` is current | Latest version block exists and matches the release tag |

---

## 8. Test artifacts committed by Phase 22

| Artifact | Location |
| --- | --- |
| `OgmaLibrary.Tests.Packaging` project | `tests/OgmaLibrary.Tests.Packaging/` |
| Migration rollback test classes | `tests/OgmaLibrary.Tests.Packaging/MigrationRollbackTests.cs` |
| Trust-chain test classes | `tests/OgmaLibrary.Tests.Packaging/UpdateTrustChainTests.cs` |
| MAS sandbox test classes | `tests/OgmaLibrary.Tests.Packaging/MasSandboxTests.cs` |
| MSIX installation test record | `docs/distribution/WINDOWS-STORE.md` |
| DMG installation test record | `docs/distribution/MAC-APP-STORE.md` |
| Gatekeeper verification record | `docs/distribution/MAC-APP-STORE.md` (screenshot + spctl output) |
