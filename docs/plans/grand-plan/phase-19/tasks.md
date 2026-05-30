# Phase 19 — Tasks

Work packages and granular tasks for Security Hardening & Privacy / Compliance.
Task IDs: `P19-WP{n}-T{m}`.

---

## Work Package 1 — Threat Model

**Goal:** Full STRIDE threat model + attack trees for the complete application
surface, with the LAN Host inbound surface as the primary new threat boundary.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP1-T1 | List all components and trust boundaries in the current architecture (standalone + networked); draw a Data Flow Diagram (DFD) for each bounded context and cross-boundary data flow | Phase 18 DoD | 0.5 d | SRS §7, threat model |
| P19-WP1-T2 | Apply STRIDE (`security-scanning:stride-analysis-patterns`) to each trust boundary; produce a threat table: component × STRIDE category × threat description × initial severity | P19-WP1-T1 | 1 d | SRS §7 |
| P19-WP1-T3 | Prioritize top-5 threats by severity × exploitability; construct attack trees for T1..T5 (`security-scanning:attack-tree-construction`) | P19-WP1-T2 | 0.5 d | SRS §7 |
| P19-WP1-T4 | Map every threat to CTRL-OGMA control(s) or an accepted residual risk (`security-scanning:threat-mitigation-mapping`); record in `docs/security/threat-model-phase-19.md` and `docs/security/attack-trees-phase-19.md` | P19-WP1-T3 | 0.5 d | SRS §7 |
| P19-WP1-T5 | Build `docs/security/ctrl-ogma-matrix.md`: table of all 24 controls × evidence (test name / inspection record / gap note) | P19-WP1-T4 | 0.5 d | All CTRL-OGMA |

---

## Work Package 2 — CTRL-OGMA-001..003 (Credential Store)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP2-T1 | Audit all call sites of `ICredentialStore`: enumerate every location where a secret is written or read; verify each call is behind the abstraction (not direct DPAPI/Keychain) | P19-WP1-T5 | 0.25 d | CTRL-OGMA-001..002 |
| P19-WP2-T2 | Add Roslyn lint rule: flag any variable declaration with name matching `*key*`, `*secret*`, `*password*`, `*token*` assigned a string literal (case-insensitive) | P19-WP2-T1 | 0.25 d | CTRL-OGMA-002 |
| P19-WP2-T3 | Verify no plain-text secret in SQLite: run `strings` equivalent on the database files in CI test output; assert no match for known key patterns | P19-WP2-T2 | 0.25 d | CTRL-OGMA-002, R2 |
| P19-WP2-T4 | Key rotation test (CTRL-OGMA-003): `CredentialStore_KeyRotation_OldKeyInaccessible_NewKeyRetrievable` | P19-WP2-T3 | 0.25 d | CTRL-OGMA-003 |
| P19-WP2-T5 | Update `ctrl-ogma-matrix.md` rows 001..003 with evidence | P19-WP2-T4 | 0.1 d | CTRL-OGMA-001..003 |

---

## Work Package 3 — CTRL-OGMA-004..007 (PDF Worker Isolation)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP3-T1 | Review Phase 05/08/15 worker isolation implementation against the isolation spec: (a) no network access from worker; (b) writes only to designated temp dir; (c) cannot spawn child processes | Phase 15 DoD | 0.5 d | CTRL-OGMA-004..007 |
| P19-WP3-T2 | Windows: verify Job Object policy applied to the PDF worker process (`JOBOBJECT_BASIC_UI_RESTRICTIONS`, `JOBOBJECT_EXTENDED_LIMIT_INFORMATION` with no child processes); add test | P19-WP3-T1 | 0.5 d | CTRL-OGMA-005..007 |
| P19-WP3-T3 | macOS: verify sandbox entitlements (or `posix_spawn` flags) prevent network and out-of-temp writes; add test | P19-WP3-T1 | 0.5 d | CTRL-OGMA-005..007 |
| P19-WP3-T4 | Fault injection: supply a crafted PDF that (a) attempts HTTP to `127.0.0.1:8080`; (b) attempts `File.WriteAllText("../../evil.txt", "x")`; (c) attempts `Process.Start("cmd")`; assert all three blocked; main process unaffected | P19-WP3-T2, P19-WP3-T3 | 0.5 d | CTRL-OGMA-004..007, R2 |
| P19-WP3-T5 | Update `ctrl-ogma-matrix.md` rows 004..007 | P19-WP3-T4 | 0.1 d | CTRL-OGMA-004..007 |

---

## Work Package 4 — CTRL-OGMA-008..011 (Path Validation)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP4-T1 | Implement `PathGuard` in `Infrastructure.Security`: `EnsureWithinRoot(string path, string root)` — canonicalizes both paths using `Path.GetFullPath()`; throws `PathTraversalException` if `path` does not start with `root`; O(1) per call | P19-WP1-T5 | 0.5 d | CTRL-OGMA-008..011 |
| P19-WP4-T2 | Audit all file I/O operations in the codebase that accept external input (bookId, sidecar path, library root, LAN asset endpoint `bookId`); replace ad-hoc path checks with `PathGuard.EnsureWithinRoot()` | P19-WP4-T1 | 0.75 d | CTRL-OGMA-008..011 |
| P19-WP4-T3 | Fuzz test: `PathGuard_RejectsTraversal_50Patterns` — test with: `../`, `..\\`, `%2e%2e%2f`, `%2e%2e/`, null-byte injection, absolute path outside root, UNC path `\\server\share`, symlink to outside root (create actual symlink in temp dir), overlong path (260+ chars Windows) | P19-WP4-T2 | 0.5 d | CTRL-OGMA-008, R2 |
| P19-WP4-T4 | Fuzz test: `LanAssetEndpoint_PathTraversal_50Patterns` — same patterns as above applied to Phase 16 asset endpoint `bookId` parameter via `HttpClient` | P19-WP4-T3 | 0.25 d | CTRL-OGMA-010, R2 |
| P19-WP4-T5 | Verify library root canonicalization: `LibraryRoot_SymlinkIsResolved_ToAbsoluteCanonical` | P19-WP4-T3 | 0.25 d | CTRL-OGMA-009 |
| P19-WP4-T6 | Update `ctrl-ogma-matrix.md` rows 008..011 | P19-WP4-T5 | 0.1 d | CTRL-OGMA-008..011 |

---

## Work Package 5 — CTRL-OGMA-012..013 (Signed Updates)

**Note:** The Velopack update pipeline is built in Phase 22. Phase 19 establishes
the signing key policy and the verification tests so Phase 22 can implement
against a verified spec.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP5-T1 | Document signing key policy: algorithm (Ed25519 or RSA-4096), key rotation cadence, key storage (HSM or DPAPI-protected on a secure build machine), revocation procedure | P19-WP1-T4 | 0.25 d | CTRL-OGMA-012..013 |
| P19-WP5-T2 | Implement stub `IUpdateVerifier.VerifyDescriptor(descriptor, signature)` — returns `true` iff signature is valid for descriptor content; Phase 22 supplies the real implementation | P19-WP5-T1 | 0.25 d | CTRL-OGMA-012 |
| P19-WP5-T3 | Tampered-descriptor test: `UpdateVerifier_RejectsAlteredDescriptor` — modify one byte in a signed descriptor; assert `VerifyDescriptor` returns `false` | P19-WP5-T2 | 0.25 d | CTRL-OGMA-012, R2 |
| P19-WP5-T4 | Tampered-package test: `UpdateVerifier_RejectsAlteredPackage` — compute expected hash; alter one byte in package; assert mismatch | P19-WP5-T3 | 0.25 d | CTRL-OGMA-013, R2 |
| P19-WP5-T5 | Rollback test: `UpdatePipeline_RollsBack_OnVerificationFailure` — simulate a failed verification mid-apply; assert the installed version is unchanged | P19-WP5-T4 | 0.25 d | CTRL-OGMA-012..013, R1 |
| P19-WP5-T6 | Update `ctrl-ogma-matrix.md` rows 012..013 | P19-WP5-T5 | 0.1 d | CTRL-OGMA-012..013 |

---

## Work Package 6 — CTRL-OGMA-014..015 (At-Rest Encryption)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP6-T1 | Spike SQLCipher cross-platform: confirm `SQLitePCLRaw` with SQLCipher native lib builds and passes basic tests on Windows and macOS; record outcome | Phase 18 DoD | 0.5 d | CTRL-OGMA-014 |
| P19-WP6-T2 | If SQLCipher confirmed: implement `StudentDbContext` key derivation — key = HKDF-SHA256(device-secret-from-ICredentialStore, salt=profileId, length=32 bytes); pass key to SQLCipher via `PRAGMA key = '<hex>'` | P19-WP6-T1 | 0.5 d | CTRL-OGMA-014 |
| P19-WP6-T3 | If SQLCipher not feasible: implement app-level column encryption using `AesGcm` for `StudentAnnotations.Body` and `StudentAiHistory.Query` fields; document as partial coverage in CTRL-OGMA-014 | P19-WP6-T1 | 0.5 d | CTRL-OGMA-014 |
| P19-WP6-T4 | Encryption verification test: open encrypted student DB with a hex editor / `strings` tool; assert no recognizable annotation text or query text in raw bytes | P19-WP6-T2 or P19-WP6-T3 | 0.25 d | CTRL-OGMA-014, R2 |
| P19-WP6-T5 | Implement `IAtRestEncryptionService` for main catalogue (opt-in, CTRL-OGMA-015): toggle in `Settings > Privacy`; on enable, back up catalogue → re-encrypt in place; on disable, back up → decrypt | P19-WP6-T4 | 0.5 d | CTRL-OGMA-015 |
| P19-WP6-T6 | Integration test: `CatalogueEncryption_ToggleOn_BackupCreated_CatalogueEncrypted`, `CatalogueEncryption_ToggleOff_CatalogueDecrypted_BackupPreserved` | P19-WP6-T5 | 0.25 d | CTRL-OGMA-015, R1 |
| P19-WP6-T7 | Update `ctrl-ogma-matrix.md` rows 014..015 | P19-WP6-T6 | 0.1 d | CTRL-OGMA-014..015 |

---

## Work Package 7 — CTRL-OGMA-016..023 (Off-Device Controls)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP7-T1 | Architecture test: `ArchTests_NoDirectHttpClientInDomainOrApplication` — assert no `HttpClient` instantiation in `Domain` or `Application` layer projects; all HTTP is in `Infrastructure` and behind interfaces | P19-WP1-T5 | 0.25 d | CTRL-OGMA-016 |
| P19-WP7-T2 | Privacy tier enforcement: enumerate all `IAiProvider` call sites; for each, assert that `IAiPrivacyService.BuildPreviewAsync()` is called before the provider call (static analysis + runtime test with `MockAiProvider`) | P19-WP7-T1 | 0.5 d | CTRL-OGMA-017, NFR-PROD-011 |
| P19-WP7-T3 | Audit completeness: `AuditCompleteness_20AiCalls_20AuditRows` — run 20 AI calls through `IAiProvider`; assert exactly 20 `AuditEvents` rows written; assert timestamps monotonically increasing; assert no gap | P19-WP7-T2 | 0.25 d | CTRL-OGMA-018, NFR-PROD-013 |
| P19-WP7-T4 | AI history erasure: `AiHistoryErasure_DeleteAll_NoRemnants` — assert that after delete, no query text or response summary appears in the SQLite DB file (use `strings` on DB file in test) | P19-WP7-T3 | 0.25 d | CTRL-OGMA-019, FR-AI-009 |
| P19-WP7-T5 | LAN subnet validation: `SubnetValidation_BlocksNonRfc1918_OnListener` — verify the Phase 16 subnet check at the listener level; test from a non-RFC-1918 source (mock `IPAddress` injection) | P19-WP7-T4 | 0.25 d | CTRL-OGMA-020 |
| P19-WP7-T6 | Student tier enforcement: `StudentAi_ContentAwareBlocked_ForMinorWithoutAdminOptIn` (re-verify Phase 18 control; add to Phase 19 control matrix) | P19-WP7-T5 | 0.25 d | CTRL-OGMA-021 |
| P19-WP7-T7 | Answer-mode grounding: `ClassroomAnswerGrounder_FabricatedCitation_Stripped` (re-verify Phase 18; add to matrix) | P19-WP7-T6 | 0.1 d | CTRL-OGMA-022 |
| P19-WP7-T8 | Telemetry audit: enumerate all telemetry events; verify no `BookId`, `ProfileId`, annotation content, or query text in any event DTO; architecture test: `TelemetryEvent_ContainsNoPersonalData` | P19-WP7-T7 | 0.25 d | CTRL-OGMA-023 |
| P19-WP7-T9 | Update `ctrl-ogma-matrix.md` rows 016..023 | P19-WP7-T8 | 0.1 d | CTRL-OGMA-016..023 |

---

## Work Package 8 — CTRL-OGMA-024 (DPIA + Minors)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP8-T1 | Build `docs/security/dpia-register.md`: enumerate every off-device feature (metadata lookup, AI search, update check, telemetry, sync blob upload); for each: data transmitted, legal basis, data subject type, retention, delete mechanism, DPIA result | Phase 18 DoD | 0.75 d | CTRL-OGMA-024 |
| P19-WP8-T2 | Jurisdiction matrix: for each jurisdiction configured (Uganda DPPA, EU GDPR — per owner decision): document the allowed tiers for minors, the legal basis required, and the `IDpiaScreeningService` rule that enforces it | P19-WP8-T1 | 0.5 d | CTRL-OGMA-024 |
| P19-WP8-T3 | `IDpiaScreeningService` — harden to cover full jurisdiction matrix (add all jurisdictions from P19-WP8-T2); fail-safe for unrecognized jurisdiction | P19-WP8-T2 | 0.5 d | CTRL-OGMA-024, R2 |
| P19-WP8-T4 | Minor-profile test matrix: for each tier × jurisdiction × birth-year-present/absent: assert `Approved` or `Disqualified` as expected | P19-WP8-T3 | 0.5 d | CTRL-OGMA-024, R2 |
| P19-WP8-T5 | Off-device feature coverage: `DpiaRegister_EveryOffDeviceFeature_HasRecord` — automated test reads `docs/security/dpia-register.md` metadata and asserts it covers every `IAiProvider` + HTTP call path in the codebase | P19-WP8-T4 | 0.25 d | CTRL-OGMA-024 |
| P19-WP8-T6 | Update `ctrl-ogma-matrix.md` row 024 | P19-WP8-T5 | 0.1 d | CTRL-OGMA-024 |

---

## Work Package 9 — SAST

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP9-T1 | Configure SAST: add `SecurityCodeScan.VS2019` and `SonarAnalyzer.CSharp` NuGet references to `Directory.Build.props`; enable security rule categories; configure rule severity in `.editorconfig` (`security-scanning:sast-configuration`) | P19-WP1-T5 | 0.25 d | SRS §7 |
| P19-WP9-T2 | Run SAST scan: `dotnet build` with analyzers active; capture SARIF output | P19-WP9-T1 | 0.25 d | SRS §7 |
| P19-WP9-T3 | Resolve all high-severity SAST findings; for each medium-severity finding: fix, or write a disposition note (accepted / false-positive) | P19-WP9-T2 | 0.5 d | SRS §7, R2 |
| P19-WP9-T4 | Write `docs/security/sast-report-phase-19.md`: tool used, rule set, high-severity findings (all fixed), medium-severity findings with dispositions, run date, build hash | P19-WP9-T3 | 0.25 d | SRS §7 |
| P19-WP9-T5 | Add SAST step to CI pipeline: `dotnet build /p:TreatWarningsAsErrors=true` with analyzers active; must pass before merge | P19-WP9-T4 | 0.25 d | SRS §7, Phase DoD |

---

## Work Package 10 — Privacy Settings UI

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP10-T1 | Create `PrivacySettingsView.axaml` + `PrivacySettingsViewModel`: sections: (1) Off-device features table; (2) AI history delete; (3) Reset to defaults; (4) At-rest encryption toggle | P19-WP6-T5, P19-WP7-T2 | 0.75 d | NFR-PROD-011, CTRL-OGMA-016/019/024 |
| P19-WP10-T2 | Off-device features table: columns: Feature name, Current tier, DPIA status (`Pass` / `Disqualified` / `Not configured`), toggle to disable; bound to `IPrivacySettingsService` | P19-WP10-T1 | 0.5 d | CTRL-OGMA-016..024 |
| P19-WP10-T3 | Data-erase actions: "Delete all AI history" (confirmation required, name-entry confirmation); "Reset all privacy settings to defaults"; both keyboard-navigable; SR announces consequence | P19-WP10-T2 | 0.25 d | CTRL-OGMA-019, reversibility |
| P19-WP10-T4 | i18n: all Privacy settings strings in `Strings.en.resx` + `Strings.fr.resx`; pseudolocale check | P19-WP10-T3 | 0.25 d | I18N-STRATEGY |
| P19-WP10-T5 | Accessibility walkthrough: keyboard-only navigation of entire Privacy settings view; SR reads table rows and DPIA status | P19-WP10-T4 | 0.25 d | WCAG 2.2 AA |

---

## Work Package 11 — `/security-review` & Resolution

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P19-WP11-T1 | `/security-review` — full review of Phase 19 output: threat model, control matrix, SAST report, at-rest encryption, DPIA register, hardened workers, path validation | All WPs | 1 d | Phase DoD |
| P19-WP11-T2 | Resolve all security-review findings; re-run affected tests | P19-WP11-T1 | 0.5 d | Phase DoD |
| P19-WP11-T3 | `comprehensive-review:security-auditor` — independent reviewer reads threat model and WP11 findings; sign-off recorded | P19-WP11-T2 | 0.5 d | Phase DoD |
| P19-WP11-T4 | Update CTRL-OGMA matrix final status; owner sign-off on any `Accepted-gap` rows | P19-WP11-T3 | 0.25 d | Phase DoD |
