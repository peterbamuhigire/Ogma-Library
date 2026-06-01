# Phase 16 — Test Plan

Test plan for LAN Library Server (Host Mode). Follows the 9-layer model from
`SOURCE-SUMMARY.md §J`. R1 (data-loss) and R2 (privacy-breach) defects are
unwaivable release blockers.

---

## 1. Applicable test layers

| Layer | Applies? | Notes |
| --- | --- | --- |
| 1. Domain | No | No new domain entities in this phase |
| 2. Infrastructure | Yes | Certificate provisioner, mDNS advertiser, session repository, settings repository |
| 3. PDF | Yes | Page-render mode uses `IPageRenderer` — oracle tests against golden corpus |
| 4. Search | Partial | Catalogue search projection endpoint (metadata only) |
| 5. AI | No | No AI in this phase |
| 6. UI | Yes | Sharing settings view (Avalonia); ARIA/keyboard walkthrough |
| 7. 3D | No | No 3D surface changes |
| 8. Performance | Yes | Load smoke tests (NFR-LAN-001/002); render throughput |
| 9. Packaging | No | No packaging changes |

Additional: **Architecture tests** (bounded-context isolation) and **Security
tests** (path traversal, subnet validation, auth boundary).

---

## 2. Test environment

- **Test project:** `src/OgmaLibrary.Tests/` (existing), plus a new
  `src/OgmaLibrary.Tests.Integration.LanHost/` assembly for integration and load
  tests that require a live HTTPS listener.
- **Golden corpus:** version-pinned, hash-oracle corpus from `SOURCE-SUMMARY.md §J`.
  Used in: page-render oracle tests (WP5); catalogue projection integrity.
- **Synthetic perf corpus:** 500-book and 2,000-book synthetic corpora (from Phase
  02 harness) for catalogue endpoint pagination and load tests.
- **Test Host:** `LanHostTestFixture` — starts a `ILibraryHostService` on a
  loopback HTTPS port with a test CA; provides `HttpClient` pre-configured with
  the test CA; tears down cleanly after each test class.
- **Platforms:** all tests run on **both Windows (CI) and macOS (CI)** runners.
  Platform-specific assertions (certificate store provider, mDNS advertisement
  interface) use `[SkipOnPlatform]` annotations only where the underlying OS API
  differs and the behavior is identical at the contract level.

---

## 3. Unit tests

### CertificateProvisioner

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `CertificateProvisioner_GeneratesValidX509Root` | `cert.Version == 3`, `cert.IssuerName == SubjectName`, `cert.NotAfter > now + 2y` | R2 |
| `CertificateProvisioner_FingerprintIsStableAcrossLoads` | fingerprint hex identical after reload from credential store | R2 |
| `CertificateProvisioner_PersistsToCredentialStore_Windows` | DPAPI-backed key round-trips without loss | R1 |
| `CertificateProvisioner_PersistsToKeychain_macOS` | Keychain item present after provisioning | R1 |

### MdnsAdvertiser

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `MdnsAdvertiser_StartRegisters_StopDeregisters` | service name present in mock DNS-SD registry after start; absent after stop | R5 |
| `MdnsAdvertiser_ServiceRecord_ContainsFingerprintTxt` | TXT record includes `fp=<sha256>` | R2 |

### SubnetValidator

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `SubnetValidator_AcceptsRfc1918_10Range` | `10.0.0.1` → accepted | R2 |
| `SubnetValidator_AcceptsRfc1918_192Range` | `192.168.1.100` → accepted | R2 |
| `SubnetValidator_RejectsPublicIp` | `8.8.8.8` → rejected | R2 |
| `SubnetValidator_RejectsLoopback` | `127.0.0.1` → rejected (not a LAN client) | R2 |

### SessionTokenService

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `SessionToken_Contains_ProfileId_And_Role` | JWT claims include `profileId`, `role`, `exp` | R2 |
| `SessionToken_Expired_ReturnsUnauthorized` | token with `exp = now - 1s` → middleware returns 401 | R2 |
| `SessionToken_Revoked_ReturnsUnauthorized` | `RevokeAllAsync()` called → previously valid token → 401 | R2 |

### HostModeSettings

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `HostModeSettings_Default_ContentMode_IsPageRender` | seed row `ContentMode == "PageRender"` | R2 |
| `HostModeSettings_ToggleOff_NoListenerBound` | `ILibraryHostService.IsRunning == false` → no TCP port open on configured port | R2 |

---

## 4. Integration tests

All integration tests use `LanHostTestFixture` (live HTTPS listener on loopback,
test CA, pre-enrolled test session token).

### Catalogue projection endpoint

| Test | Method + URL | Oracle | Risk tier |
| --- | --- | --- | --- |
| `CatalogueEndpoint_ReturnsPaginatedBooks` | `GET /api/v1/catalogue?page=1&pageSize=10` | 10 books; `totalCount` matches 2,000-book corpus count | R5 |
| `CatalogueEndpoint_Returns401_WithoutToken` | `GET /api/v1/catalogue` (no Bearer) | HTTP 401 | R2 |
| `CatalogueEndpoint_SingleBook_MatchesCatalogueState` | `GET /api/v1/catalogue/{knownBookId}` | DTO fields match catalogue DB row | R5 |
| `CatalogueEndpoint_Search_ReturnsFilteredResults` | `GET /api/v1/catalogue/search?q=testterm` | Only books containing `testterm` in metadata returned | R5 |

### Asset serving endpoint

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `AssetEndpoint_ServesCoverMatchingSidecarHash` | Response body SHA-256 == pre-computed hash of sidecar cover file | R5 |
| `AssetEndpoint_Returns404_ForUnknownBookId` | HTTP 404, no exception leakage | R5 |
| `AssetEndpoint_RejectsPathTraversal_DotDot` | URL with `../` in bookId → HTTP 400 | R2 |
| `AssetEndpoint_CacheControlHeader_Present` | `Cache-Control: max-age=86400` in response | R5 |

### Page-render endpoint

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `PageRenderEndpoint_StreamsPng_MatchingGoldenOracle` | Response Content-Type `image/png`; body SHA-256 matches oracle for golden-corpus PDF page 1 | R5 |
| `PageRenderEndpoint_NoPdfBytesInResponse` | Response body does not begin with `%PDF`; Content-Type is not `application/pdf` | R2 |
| `PageRenderEndpoint_ResolutionCapped_At150dpi` | Request with `resolution=300` returns `150dpi`-resolution image | R5 |
| `PageRenderEndpoint_ConcurrencyLimiter_Returns202_OnQueueFull` | 11 simultaneous requests → at least 1 `202 Accepted` | R5 |

### File-stream endpoint

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `FileStreamEndpoint_Returns403_WhenPageRenderMode` | HTTP 403 with descriptive error body | R2 |
| `FileStreamEndpoint_StreamsPdfBytes_WhenFileStreamEnabled` | Response begins `%PDF`; Content-Type `application/pdf`; Content-Length matches file size | R5 |
| `FileStreamEndpoint_WritesAuditEntry_ContentMode_FileStream` | `AuditEvents` row exists with `action = "StreamFile"`, `resourceType = "BookFile"`, and `contentMode = "FileStream"` | R2 |

### Authentication

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `AuthFlow_ValidEnrollmentCode_IssuesSessionToken` | `POST /auth/session` with valid code → 200 + JWT | R2 |
| `AuthFlow_InvalidCode_Returns401` | HTTP 401; no token in body | R2 |
| `AuthFlow_ExpiredToken_AllEndpointsReturn401` | All protected routes → 401 after token expiry | R2 |

### Audit trail

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `AuditMiddleware_WritesRowForEvery_AuthenticatedRequest` | 10 catalogue requests → 10 `AuditEvents` rows; timestamps monotonically increasing | R2 |
| `AuditMiddleware_WritesRow_ForRejectedUnauthenticated` | Unauthenticated request → 1 reduced audit row (statusCode=401, no profileId) | R2 |
| `AuditMiddleware_RowContains_ClientIdentity_And_Resource` | Row fields: `clientId`, `role`, `action`, `resourceType`, `resourceId`, `method`, `statusCode`, `durationMs`, and timestamp all present when applicable | R2 |

---

## 5. Architecture tests

File: `src/OgmaLibrary.Tests/Architecture/LanHostIsolationTests.cs`

```csharp
// CONVENTIONS: these tests run on every PR — no exceptions.

[Fact]
public void ArchTests_LanHost_HasNoCredentialStoreOrWorkerDependency()
    // LanHost namespace types do not reference CredentialStore or UntrustedPdfWorker types.

[Fact]
public void ArchTests_LanHost_HasNoAiProviderDependency()
    // LanHost namespace types do not reference IAiProvider or any AI adapter type.

[Fact]
public void ArchTests_StandaloneMode_HasNoOpenListener()
    // When Host mode is off (HostModeSettings.IsEnabled = false), no TCP socket
    // bound on the configured port exists after cold start.

[Fact]
public void ArchTests_LanHost_OnlyDependsOn_ApplicationInterfaces()
    // LanHost concrete types depend only on Application-layer interfaces
    // (ICatalogueProjectionService, IPageRenderer, IAuditService,
    //  IHostModeSettingsRepository, ICredentialStore).
```

---

## 6. Performance tests

### Smoke tests (CI, reference hardware or CI runner)

| Test | Fixture | Threshold | Risk tier |
| --- | --- | --- | --- |
| `LanHostSmokeTest_20ConcurrentCatalogueClients` | 20 `HttpClient` threads, each making 100 `GET /api/v1/catalogue?pageSize=10` over 60 s | P95 response ≤ 800 ms | R3 |
| `LanHostSmokeTest_10ConcurrentPageRenders` | 10 threads, each requesting page 1 of a 200-page golden-corpus PDF 20 times | P95 first-byte ≤ 2 s | R3 |
| `LanHostSmokeTest_AssetThroughput` | 20 threads requesting covers for 100 distinct books | P95 ≤ 300 ms | R3 |

Full 40-client benchmark is deferred to Phase 20 (`NFR-LAN-001/002` definitive
gate), where reference hardware is fixed and instrumented.

---

## 7. UI / accessibility tests

- Keyboard-only navigation of the entire `SharingSettingsView` panel (verified
  manually by a contributor following the keyboard walkthrough checklist from
  Phase 03).
- Screen-reader audit: VoiceOver (macOS) and Narrator (Windows) must correctly
  announce: Host mode toggle state, status chip text, connected-client count,
  QR-code panel instructions.
- Automated: Avalonia UI test (using `Avalonia.Headless.XUnit`) for
  `SharingSettingsViewModel`: toggle fires `ILibraryHostService.StartAsync()`;
  status property updates; client count updates.
- Pseudolocale check: all Sharing settings strings render without overflow in
  `qps-ploc` pseudolocale (CI gate).

---

## 8. Security tests

Executed as part of `/security-review` (P16-WP11-T7):

- Path traversal: fuzz test `bookId` parameter with `../../etc/passwd`,
  `%2e%2e%2f`, null bytes — all return HTTP 400; no file-system access outside
  library root.
- Subnet bypass: test client from a routable (non-RFC-1918) IP address is
  rejected at the listener level.
- MITM simulation: self-signed certificate with a different fingerprint presented
  to the client TOFU mechanism — client refuses and logs a warning.
- Session forgery: tampered JWT (invalid signature) → 401.
- Replay: session token used after `RevokeAllAsync()` → 401.

---

## 9. Golden-corpus coverage

| Corpus fixture | Test(s) that use it |
| --- | --- |
| Simple text PDF | `PageRenderEndpoint_StreamsPng_MatchingGoldenOracle`, catalogue projection |
| Scanned image-only PDF | Page render oracle (render must produce non-blank PNG) |
| Very large PDF (1,000+ pages) | `PageRenderEndpoint_ConcurrencyLimiter` (multiple pages requested) |
| Two-column PDF | Page render oracle |
| Non-English PDF | Catalogue projection (title/author encoding correct in DTO) |
| Bad-metadata PDF | Catalogue projection (graceful missing-field handling) |

---

## 10. CI integration

```yaml
# .github/workflows/phase-16.yml (conceptual — CI system TBD in Phase 02)
jobs:
  test-windows:
    runs-on: windows-latest
    steps:
      - dotnet test --filter "Category=Unit|Category=Integration|Category=Architecture"
      - dotnet test --filter "Category=Performance" --timeout 120
  test-macos:
    runs-on: macos-latest
    steps:
      - dotnet test --filter "Category=Unit|Category=Integration|Category=Architecture"
      - dotnet test --filter "Category=Performance" --timeout 120
```

All test categories must pass on **both** runners before Phase 16 DoD is
declared. No platform-only failures are acceptable.
