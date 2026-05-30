# Phase 19 — Test Plan

Test plan for Security Hardening & Privacy / Compliance. This phase does not
introduce new user features — it verifies that every CTRL-OGMA security and
privacy control is enforceable by an automated test or inspection record.

R1 (data-loss) and R2 (privacy-breach) defects are unwaivable release blockers.
A CTRL-OGMA control with no test evidence is treated as an R2 gap.

---

## 1. Applicable test layers

| Layer | Applies? | Notes |
| --- | --- | --- |
| 1. Domain | No | No domain changes |
| 2. Infrastructure | Yes | PathGuard, AtRestEncryptionService, IUpdateVerifier, hardened DpiaScreeningService |
| 3. PDF | Yes | PDF worker isolation fault injection |
| 4. Search | No | No search changes |
| 5. AI | Yes | Privacy tier completeness, payload-preview completeness, audit completeness, AI history erasure |
| 6. UI | Yes | Privacy settings view (keyboard + SR) |
| 7. 3D | No | No 3D changes |
| 8. Performance | Minimal | PathGuard benchmark; at-rest encryption open latency |
| 9. Packaging | Partial | Update-verification tests (stub; full in Phase 22) |

Additional: **Architecture tests** (no direct HTTP in Domain/Application),
**Fuzz tests** (path traversal), **Fault-injection tests** (worker isolation),
**DPIA coverage tests**, **SAST** (CI gate).

---

## 2. CTRL-OGMA control verification matrix

The primary output of Phase 19 testing is a complete verification record.
Every row in the table below must have `Verified` status by Phase 19 DoD.

| Control | Area | Evidence type | Test / record name |
| --- | --- | --- | --- |
| CTRL-OGMA-001 | OS credential store | Unit test | `CredentialStore_KeyStoredAndRetrieved_ViaAbstraction` (per phase) |
| CTRL-OGMA-002 | No plain-text secret | Static + runtime | `NoPlainTextSecret_InSqliteFile`; secret-scan CI pass |
| CTRL-OGMA-003 | Key rotation | Unit test | `CredentialStore_KeyRotation_OldKeyInaccessible` |
| CTRL-OGMA-004 | Worker isolation (no escape) | Fault injection | `Worker_MalformedPdf_DoesNotEscape_WorkerBoundary` |
| CTRL-OGMA-005 | Worker no network | Fault injection | `Worker_HttpAttempt_Blocked` |
| CTRL-OGMA-006 | Worker writes temp only | Fault injection | `Worker_WriteOutsideTemp_Blocked` |
| CTRL-OGMA-007 | Worker no child process | Fault injection | `Worker_ChildProcessAttempt_Blocked` |
| CTRL-OGMA-008 | Path traversal prevention | Fuzz test | `PathGuard_RejectsTraversal_50Patterns` |
| CTRL-OGMA-009 | Library root canonicalized | Unit test | `LibraryRoot_SymlinkResolved_ToAbsolute` |
| CTRL-OGMA-010 | LAN asset traversal prevention | Fuzz test | `LanAssetEndpoint_PathTraversal_50Patterns` |
| CTRL-OGMA-011 | Sidecar writes bounded | Integration test | `SidecarWrite_OutsideSidecarFolder_ThrowsException` |
| CTRL-OGMA-012 | Update descriptor signed | Stub test | `UpdateVerifier_RejectsAlteredDescriptor` |
| CTRL-OGMA-013 | Update package signed | Stub test | `UpdateVerifier_RejectsAlteredPackage` |
| CTRL-OGMA-014 | Student DB at-rest encrypted | Integration test | `StudentDb_RawBytes_ContainNoAnnotationText` |
| CTRL-OGMA-015 | Catalogue at-rest encryption (opt-in) | Integration test | `CatalogueEncryption_ToggleOn_EncryptedBackupPreserved` |
| CTRL-OGMA-016 | No direct HTTP in Domain/Application | Architecture test | `ArchTests_NoDirectHttpClientInDomainOrApplication` |
| CTRL-OGMA-017 | Payload preview on every AI call | Integration test | `PayloadPreview_InvokedOnEveryAiProviderCall` |
| CTRL-OGMA-018 | All off-device calls audited | Integration test | `AuditCompleteness_20AiCalls_20AuditRows` |
| CTRL-OGMA-019 | AI history erasure | Integration test | `AiHistoryErasure_NoRemnants_InDbFile` |
| CTRL-OGMA-020 | LAN listener RFC-1918 only | Integration test | `SubnetValidation_BlocksNonRfc1918` |
| CTRL-OGMA-021 | Student tier bounded by school policy | Integration test | `StudentAi_ContentAwareBlocked_ForMinor` |
| CTRL-OGMA-022 | Answer mode bounded to catalogue | Integration test | `ClassroomAnswerGrounder_FabricatedCitation_Stripped` |
| CTRL-OGMA-023 | Telemetry no personal data | Architecture test | `TelemetryEvent_ContainsNoPersonalData` |
| CTRL-OGMA-024 | DPIA per feature; minors protected | DPIA register + unit tests | `DpiaRegister_EveryOffDeviceFeature_HasRecord`; minor-profile test matrix |

---

## 3. Fuzz tests

### PathGuard

Test name: `PathGuard_RejectsTraversal_50Patterns`

Patterns to test (applied to both `path` argument and segment within path):
```
../        ..\\       ..\\..\    %2e%2e%2f   %2e%2e/
..%2f      %2e%2e%5c  ..%5c      .%2e/        %252e%252e/
null byte (\0) mid-path
Absolute path outside root: /etc/passwd  C:\Windows\System32
UNC path: \\server\share\file
Symlink: link inside root → target outside root (create real symlink in temp)
Overlong path: "a" × 260 chars (Windows MAX_PATH edge)
Double-slash: //evil
Mixed separators: ..\\/..\/
URL-encoded variants of all of the above
```

Oracle for each pattern: `PathTraversalException` thrown OR the resolved path
starts with `root + DirectorySeparatorChar`. Zero false-negatives permitted.

### LAN asset endpoint

Test name: `LanAssetEndpoint_PathTraversal_50Patterns`
Apply the same 50 patterns to the `bookId` URL path segment in `GET /api/v1/assets/cover/{bookId}`.
Oracle: HTTP 400 for all traversal patterns; no file read outside sidecar folder.

---

## 4. Fault injection tests

### PDF worker isolation

File: `src/OgmaLibrary.Tests/Security/WorkerIsolationTests.cs`

Each test supplies a crafted "PDF" whose byte content triggers an action when processed:

| Test | Worker action attempted | Oracle | Risk tier |
| --- | --- | --- | --- |
| `Worker_HttpAttempt_Blocked` | `new HttpClient().GetAsync("http://127.0.0.1:8080")` | `WebException` or `SocketException` thrown; main process unaffected | R2 |
| `Worker_WriteOutsideTemp_Blocked` | `File.WriteAllText("../../evil.txt", "x")` | `UnauthorizedAccessException` or `SecurityException`; file does not exist | R2 |
| `Worker_ChildProcessAttempt_Blocked` | `Process.Start("cmd")` or `Process.Start("sh", "-c echo pwned")` | `InvalidOperationException` or `Win32Exception`; no child process created | R2 |
| `Worker_MalformedPdf_DoesNotCrashMainProcess` | Crafted PDF with malformed XREF table | Worker terminates cleanly; main process continues; `Jobs` row marked `Failed` | R4 |

Note: these tests require platform-specific setup (Windows Job Objects / macOS
sandbox). Mark with `[SkipOnCI]` if the CI runner does not support the required
OS-level isolation; include a manual verification checklist in that case.

---

## 5. At-rest encryption tests

### Student private DB (CTRL-OGMA-014)

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `StudentDb_RawBytes_ContainNoAnnotationText` | Open `private.db` as raw bytes; assert no occurrence of known annotation body string | R2 |
| `StudentDb_CorrectlyDecrypted_OnRead` | Write annotation → close DB → reopen → read annotation → body matches | R1 |
| `StudentDb_WrongKey_CannotOpen` | Open with wrong derived key → `SQLiteException` or `CryptographicException` | R2 |

### Main catalogue (CTRL-OGMA-015)

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `CatalogueEncryption_ToggleOn_BackupCreated` | Toggle on → backup `.bak` file created before encryption | R1 |
| `CatalogueEncryption_ToggleOn_CatalogueReadable` | After encryption, all catalogue queries return correct results | R1 |
| `CatalogueEncryption_ToggleOff_CatalogueDecrypted` | Toggle off → catalogue readable by SQLite without key | R1 |

---

## 6. Architecture tests (Phase 19 additions)

File: `src/OgmaLibrary.Tests/Architecture/SecurityIsolationTests.cs`

```csharp
[Fact] ArchTests_NoDirectHttpClientInDomainOrApplication()
    // No HttpClient instantiation in Domain or Application layer assemblies.

[Fact] ArchTests_NoHardcodedSecretPatterns()
    // No variable declaration matching *key*|*secret*|*password*|*token*
    // is assigned a string literal in any source file.
    // (Enforced by Roslyn lint rule P19-WP2-T2; this test verifies the rule fires.)

[Fact] ArchTests_TelemetryEvent_ContainsNoPersonalDataTypes()
    // No TelemetryEvent DTO has a property of type BookId, ProfileId,
    // or string named Body, Query, or Annotation.
```

---

## 7. DPIA coverage tests

File: `src/OgmaLibrary.Tests/Security/DpiaCoverageTests.cs`

| Test | Oracle | Risk tier |
| --- | --- | --- |
| `DpiaRegister_EveryOffDeviceFeature_HasRecord` | Parse `docs/security/dpia-register.md`; assert every off-device HTTP call path has a corresponding DPIA register entry | R2 |
| `DpiaScreening_UnknownJurisdiction_Disqualified` | Call `IDpiaScreeningService.CheckAsync` with a jurisdiction code not in the matrix → `Disqualified` | R2 |
| `DpiaScreening_MinorNoJurisdiction_Disqualified` | Minor profile + jurisdiction not configured → `Disqualified` (fail-safe) | R2 |
| `DpiaScreening_FullJurisdictionMatrix` | For each {jurisdiction, tier, isMinor} triple in the matrix: assert `Approved` or `Disqualified` matches the DPIA register policy | R2 |

---

## 8. Performance tests

| Test | Threshold | Risk tier |
| --- | --- | --- |
| `PathGuard_P99_LessThan1ms` | 1,000 `EnsureWithinRoot()` calls, P99 ≤ 1 ms | R3 |
| `StudentDb_EncryptedOpen_P95_LessThan5ms` | 10 encrypted DB opens, P95 ≤ 5 ms (key derivation included) | R3 |

---

## 9. SAST CI gate

Added in P19-WP9-T5:
- `dotnet build /p:TreatWarningsAsErrors=true` must pass with zero high-severity
  analyzer diagnostics before any merge to `main`.
- The SARIF report is uploaded as a CI artifact on every run.
- Secret-scan (`truffleHog` / `gitleaks`) runs in parallel; must pass.

---

## 10. Privacy settings UI tests

- Keyboard-only navigation of entire `PrivacySettingsView`: all controls
  reachable via Tab/Shift-Tab; data-erase confirmation reachable and cancellable.
- Screen-reader: Narrator / VoiceOver reads DPIA status column (`"Pass"` /
  `"Disqualified"` / `"Not configured"`), the encryption toggle state, and
  the confirmation dialog consequence text.
- Pseudolocale: all Privacy settings strings render without overflow.
