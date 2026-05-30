# Phase 22 — Tasks

> Work packages → tasks. Read `README.md` for scope, architecture, and
> the MAS sandbox constraint (ADR-0021) before executing.

---

## Work Package 1: CI Release Pipeline Scaffold

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP1-T1 | Create `.github/workflows/release.yml` with `channel` input parameter (dev/alpha/beta/stable); define three jobs: `build-windows`, `build-macos-direct`, `build-macos-mas`; wire `create-release` job as the aggregator. | 0.5 d | Phase 02 CI baseline | ADR-0009, L.5 |
| P22-WP1-T2 | Implement channel-tagged build: set `AssemblyInformationalVersion` to `<semver>-<channel>` from CI input; ensure `PublishSingleFile` and `SelfContained` are set for distribution targets; confirm both platforms produce a runnable artifact. | 0.5 d | P22-WP1-T1 | ADR-0009 |
| P22-WP1-T3 | Implement promote-not-rebuild: a `promote.yml` workflow that takes an existing signed artifact from one channel, re-signs only the Velopack feed descriptor for the new channel, and uploads without rebuilding the binary. | 0.5 d | P22-WP1-T2 | ADR-0009 channel strategy |

---

## Work Package 2: Windows Authenticode + MSIX

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP2-T1 | Configure `SignTool` step in `build-windows` job: retrieve certificate from GitHub Actions Secret (or HSM API); sign all EXE / DLL artifacts before packaging; verify signature with `signtool verify /pa`. | 0.5 d | Owner EV cert, P22-WP1-T1 | CTRL-OGMA-012 |
| P22-WP2-T2 | Configure `Package.appxmanifest`: Publisher (matching Store certificate), display name "Ogma Library", version (from CI tag), capabilities (`broadFileSystemAccess` or `removableStorage` per review guidance — prefer `userLibraryManager` scoped access if available), `uap:SupportedUsers = single`. | 0.5 d | Owner Partner Center app record | MSIX, L.5 |
| P22-WP2-T3 | Add MSIX packaging step: `dotnet publish` → `makeappx pack` → `signtool sign` (Store certificate); produce `OgmaLibrary-<version>-x64.msix`. | 0.5 d | P22-WP2-T1..T2 | MSIX |
| P22-WP2-T4 | Write `ENTERPRISE-DEPLOY.md`: MSIX sideloading via `AppInstaller` URI (for enterprise without Store); Intune deployment package procedure; group-policy configuration for "trusted app" exemption. | 0.5 d | P22-WP2-T3 | L.5 enterprise |

---

## Work Package 3: macOS Developer-ID Signing + Notarized DMG

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP3-T1 | Configure `codesign` step in `build-macos-direct` job: import Developer ID Application certificate from GitHub Actions Secret; `codesign --deep --options runtime --entitlements entitlements-direct.plist OgmaLibrary.app`; verify with `codesign -v --strict`. | 0.5 d | Owner Developer ID cert, P22-WP1-T1 | CTRL-OGMA-012 |
| P22-WP3-T2 | Create `entitlements-direct.plist`: `com.apple.security.files.user-selected.read-write`, `com.apple.security.network.client` only; confirm no excess entitlements. | 0.25 d | P22-WP3-T1 | Least-privilege (Phase 19) |
| P22-WP3-T3 | Configure `xcrun notarytool submit` → poll until `status = Accepted` → `xcrun stapler staple`; fail the CI job if notarization fails. Max poll time: 4 hours. | 0.25 d | P22-WP3-T1 | macOS Gatekeeper |
| P22-WP3-T4 | Package DMG: `create-dmg` or `hdiutil create`; include `OgmaLibrary.app` and a symlink to `/Applications`; notarize the DMG artifact as well. Produce `OgmaLibrary-<version>-macos.dmg`. | 0.25 d | P22-WP3-T3 | L.5 |
| P22-WP3-T5 | Verify Gatekeeper on reference macOS machine: `spctl -a -t exec -vv OgmaLibrary.app` outputs `accepted`; record pass in `docs/distribution/MAC-APP-STORE.md`. | 0.25 d | P22-WP3-T4 | CTRL-OGMA-012 verification |

---

## Work Package 4: MAS Build Target

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP4-T1 | Add `#if APPSTORE` compilation conditional; add `AppStore` MSBuild property to `Directory.Build.props`; gate the MAS `build-macos-mas` job on `-p:AppStore=true`. | 0.25 d | Phase 02 scaffold | ADR-0021 |
| P22-WP4-T2 | Create `entitlements-mas.plist`: add `com.apple.security.app-sandbox = true` and `com.apple.security.files.user-selected.read-write`; confirm no `com.apple.security.files.all`; confirm no `com.apple.security.network.server` (LAN Host excluded from MAS build). | 0.25 d | ADR-0021 | MAS compliance |
| P22-WP4-T3 | Implement `IBookmarkStore` and `SqliteBookmarkStore`: persist the security-scoped bookmark blob (base64) in a new `AppSettings.LibraryRootBookmarkMas` column; EF Core migration guarded by `#if APPSTORE`. | 0.5 d | Phase 04 data layer | ADR-0021 |
| P22-WP4-T4 | Implement `SandboxedFileSystemService : IFileSystemService`: for each file operation, resolve the bookmark via `IBookmarkStore`, call `StartAccessingSecurityScopedResource`, perform the operation, and call `StopAccessingSecurityScopedResource` in a `using` block. Register in the MAS composition root (`#if APPSTORE`) instead of `DefaultFileSystemService`. | 1 d | P22-WP4-T3 | ADR-0021 |
| P22-WP4-T5 | Add "LAN Host not available in the App Store build" notice to Settings > Library Sharing; localize in all 5 locales; ensure the notice is accessible (keyboard-reachable, screen-reader-announced). | 0.25 d | Phase 16 LAN Host, Phase 21 a11y | ADR-0021 |
| P22-WP4-T6 | Write MAS sandbox integration test: start the app with `App Sandbox` entitlements in a test harness; open a folder via simulated `NSOpenPanel` → bookmark persisted; restart app → bookmark resolved → library root accessible without re-prompting. | 0.5 d | P22-WP4-T4 | ADR-0021 verification |
| P22-WP4-T7 | Package MAS build: `dotnet publish -p:AppStore=true` → `codesign` (MAS Distribution certificate) → `productbuild` → produce `OgmaLibrary-<version>-mas.pkg`. | 0.25 d | P22-WP4-T2, Owner MAS cert | MAS submission |
| P22-WP4-T8 | Validate MAS package: `xcrun altool --validate-app` or App Store Connect Transporter; fix any validation errors. | 0.25 d | P22-WP4-T7 | MAS pre-submission |

---

## Work Package 5: Velopack Feeds + Trust Chain

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP5-T1 | Configure `vpk pack` step on both platforms: produce `<platform>-full.nupkg` and `<platform>-delta.nupkg`; channel tag in the package metadata. | 0.5 d | P22-WP2-T1, P22-WP3-T1 | ADR-0009 |
| P22-WP5-T2 | Implement Ed25519 feed signing: generate `releases-<channel>.json`; sign with the Ed25519 private key (stored in `VELOPACK_SIGNING_PRIVATE_KEY` secret); include the signature as a field in the descriptor. | 0.5 d | P22-WP5-T1 | CTRL-OGMA-013 |
| P22-WP5-T3 | Implement `VelopackUpdateService` client-side descriptor verification: on each `CheckForUpdateAsync` call, verify the Ed25519 signature on `releases-<channel>.json` using the public key embedded in the app binary (not fetched from the network); reject if invalid. | 0.5 d | P22-WP5-T2, Phase 12 update service | CTRL-OGMA-013 |
| P22-WP5-T4 | Write trust-chain tests: (a) tampered descriptor → `UpdateService` rejects; (b) valid descriptor, tampered binary → post-download hash check rejects; (c) valid descriptor + valid binary → update applies. | 0.5 d | P22-WP5-T3 | CTRL-OGMA-012/013 verification |
| P22-WP5-T5 | Test delta update path: install version N binary; apply delta to N+1; assert post-delta SHA-256 matches fresh N+1 binary. | 0.5 d | P22-WP5-T1 | ADR-0009 delta |

---

## Work Package 6: Migration Rollback Tests

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP6-T1 | Enumerate all EF Core schema migrations from Phase 04 to Phase 22; produce a version matrix for rollback testing: for each consecutive pair (N → N+1), define the test. | 0.25 d | Phase 04 migrations | NFR-PROD-010, NFR-PROD-012 |
| P22-WP6-T2 | Implement `Migrate_RollbackTest_<versionPair>` for each pair in the matrix: (a) start with schema N database seeded with books, annotations, reading progress; (b) run migration N+1; (c) inject `WriteBackBeforeFlush` fault; (d) assert rollback restores the pre-migration backup; (e) assert all user data survives. | 0.5 d | P22-WP6-T1, Phase 20 fault framework | NFR-PROD-010, R1 |
| P22-WP6-T3 | Verify backup-before-apply: assert that `ogma_db_backup_<version>.sqlite` is created and its SHA-256 matches the pre-migration catalogue before any migration runs. | 0.25 d | P22-WP6-T2 | NFR-PROD-012 |

---

## Work Package 7: ADR-0021 + Key Custody + Distribution Docs

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP7-T1 | Write `docs/adrs/ADR-0021.md`: MAS App Sandbox constraint; security-scoped bookmark decision; LAN Host exclusion from MAS build; two-target macOS strategy (direct + MAS). Owner sign-off required. | 0.25 d | ADR template | ADR-0021 |
| P22-WP7-T2 | Write `docs/distribution/KEY-CUSTODY.md`: Ed25519 private key location (GitHub Actions Environment Secret); Authenticode certificate custody; macOS Developer ID custody; key rotation procedure; emergency revocation procedure. | 0.25 d | P22-WP5-T2 | Security |

---

## Work Package 8: Windows Store Submission

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP8-T1 | Prepare store listing metadata for Windows Store: app name, description (short + long), keywords, privacy policy URL, support URL — in all 5 locales. Commit to `docs/distribution/store-listing/windows/`. | 0.5 d | Phase 21 i18n | L.5, I18N |
| P22-WP8-T2 | Upload MSIX to Partner Center; fill store listing metadata; select age rating; set price (free); submit for review. Record submission ID and date in `docs/distribution/WINDOWS-STORE.md`. | 0.5 d | P22-WP2-T3, P22-WP8-T1, Owner Partner Center | L.5 |

---

## Work Package 9: Mac App Store Submission

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP9-T1 | Prepare store listing metadata for Mac App Store: app name, description, keywords, privacy policy URL — in all 5 locales. Commit to `docs/distribution/store-listing/mac/`. | 0.5 d | Phase 21 i18n | L.5, I18N |
| P22-WP9-T2 | Upload MAS build via Transporter; fill App Store Connect listing; complete Privacy Nutrition Label (local file access, optional AI network calls, opt-in telemetry); submit for review. Record submission in `docs/distribution/MAC-APP-STORE.md`. | 0.5 d | P22-WP4-T8, P22-WP9-T1, Owner App Store Connect | L.5 |

---

## Work Package 10: GitHub Releases

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P22-WP10-T1 | Implement `create-release` CI job: create a GitHub Release for the channel; attach MSIX, Velopack Windows exe, macOS DMG, checksums (`sha256sums.txt`) from the build artifacts. | 0.25 d | P22-WP1-T1, P22-WP2-T3, P22-WP3-T4, P22-WP5-T1 | L.5 |
| P22-WP10-T2 | Generate release notes: extract the latest `CHANGELOG.md` section for the version; attach as the GitHub Release body. Use `documentation-generation:changelog-automation` skill output. | 0.25 d | `CHANGELOG.md` current | Open-source readiness |
