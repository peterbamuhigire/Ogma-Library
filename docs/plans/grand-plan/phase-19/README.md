# Phase 19 — Security Hardening & Privacy / Compliance

Execute the consolidated, comprehensive security pass for both the standalone
desktop product and the networked classroom product: build the full threat model,
verify every CTRL-OGMA control, harden the new LAN Host inbound surface, protect
minors' data, and produce a verified SAST baseline.

---

## 1. Title & one-line mission

**Phase 19 — Security Hardening & Privacy / Compliance**
Harden every security and privacy control across all 19 prior phases — the new
LAN Host inbound surface (the highest-risk new asset), untrusted-PDF isolation,
credential storage, path validation, at-rest encryption, update signing, off-device
privacy tiers, and the DPIA covering minors in the classroom — and verify the full
CTRL-OGMA control set with SAST, threat modeling, and `/security-review`.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Release tier** | V2 (some controls are MVP — noted per control) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | Spans all prior phases; primary owner of the security posture |
| **Platforms** | Windows 10/11 + macOS 13+ |
| **Status** | Planned — not started |
| **Depends on** | Phases 16, 17, 18 (networked product built); Phase 12 (AI gateway + privacy); Phase 04 (catalogue + encryption option); Phase 09 (annotation durability) |
| **ADRs introduced** | None — Phase 19 verifies and hardens; it may amend existing ADRs if gaps are found |

---

## 3. Objectives

When this phase is done, all of the following are true:

1. A complete STRIDE threat model (with attack trees for the top-5 threats)
   covers both the standalone product and the networked classroom product, with
   the LAN Host inbound surface as the primary new threat surface.
2. Every CTRL-OGMA-001 through CTRL-OGMA-024 control is verified by an
   automated test, a manual inspection record, or a documented coverage gap with
   a risk acceptance.
3. Untrusted-PDF worker isolation (CTRL-OGMA-004..007) is verified: workers run
   in a constrained execution context; a malformed PDF cannot escape the worker
   boundary.
4. Path and library-root validation (CTRL-OGMA-008..011) is verified: no code
   path allows traversal outside the declared library root for any operation
   (scan, render, sidecar write, LAN asset serve).
5. Credential storage (CTRL-OGMA-001..003) is verified: all secrets use the OS
   credential store (DPAPI / Keychain); no secret in plain text in SQLite, logs,
   config files, or HTTP responses.
6. Signed updates (CTRL-OGMA-012..013): update pipeline verified end-to-end
   (sign → verify → apply) with a rollback test.
7. At-rest encryption (CTRL-OGMA-014..015) is implemented for the student
   private DB and optionally for the main catalogue; key derivation verified.
8. Off-device controls and privacy tiers (CTRL-OGMA-016..023) are all
   verified; the four privacy tiers enforce their payload contracts; payload
   preview is exercised on every AI/enrichment path.
9. DPIA per off-device feature (CTRL-OGMA-024) is implemented for every feature
   that transmits data off the device; minors'-data handling is verified for the
   classroom product.
10. SAST scan (Roslyn analyzers + security-focused rules) passes with zero high-
    severity findings; medium findings are triaged.
11. The full `/security-review` is completed and all findings resolved.

---

## 4. Scope

### In scope

**Threat modeling:**
- STRIDE analysis of the full application surface (standalone + networked),
  with special focus on the LAN Host inbound surface (Phase 16 ADR-0010).
- Attack trees for the top-5 threats identified by STRIDE.
- Threat-to-control mapping: every threat maps to at least one CTRL-OGMA control.

**CTRL-OGMA control verification (full set):**

| Control range | Area | Phase of origin |
| --- | --- | --- |
| CTRL-OGMA-001..003 | OS credential store; key/secret management | 12, 16, 17, 18 |
| CTRL-OGMA-004..007 | Untrusted-PDF worker isolation | 05, 08, 15 |
| CTRL-OGMA-008..011 | Path + library-root validation | 05, 08, 15, 16 |
| CTRL-OGMA-012..013 | Signed builds + reversible updates | 22 (implemented here in principle; pipeline hardened) |
| CTRL-OGMA-014..015 | At-rest encryption (catalogue + student private DB) | New in Phase 19 |
| CTRL-OGMA-016..023 | Off-device controls + privacy tiers | 12, 18 |
| CTRL-OGMA-024 | DPIA per off-device feature; minors' data | 18 |

**Specific hardening tasks:**

- Untrusted-PDF isolation: verify `Workers.UntrustedPdf` runs in a constrained
  context (no network, no filesystem outside temp dir, no registry write on
  Windows). If the Phase 01/05 spike chose process-isolation, verify the
  process boundary. Add fault-injection: supply a crafted malicious PDF; assert
  it cannot write to the library root or spawn a child process.
- Path validation: audit every file I/O operation that accepts an external input
  (bookId from LAN client, library root from settings, sidecar path from catalogue).
  Implement a `PathGuard.EnsureWithinRoot(path, root)` helper used at every I/O
  boundary. Fuzz test with `../` sequences, absolute paths, null bytes, UNC paths
  (Windows), symlinks.
- Credential store: scan all code paths for plain-text secret storage (string
  contains API key, connection string with password, etc.). Add lint rule
  prohibiting `string apiKey =` at the variable declaration level (configurable
  name patterns).
- At-rest encryption (CTRL-OGMA-014..015): implement `IAtRestEncryptionService`
  using AES-256-GCM. For the student private DB: encrypt the SQLite file using
  SQLCipher or an application-level encryption wrapper. For the main catalogue:
  optional (off by default, admin opt-in). Key derived per device from OS
  credential store.
- Signed update verification (CTRL-OGMA-012..013): the Velopack update pipeline
  is signed in Phase 22; Phase 19 establishes the signing key policy, the
  verification test (simulate a tampered update descriptor, assert rejection),
  and the rollback test.
- Privacy tier enforcement audit: for every AI/enrichment call path in the
  codebase, verify the call passes through `IAiProvider` (the single egress
  chokepoint). Architecture tests already cover this; Phase 19 adds a runtime
  test using `MockAiProvider` injection.
- DPIA per off-device feature: for every feature that transmits data off-device
  (metadata lookup providers, AI providers, update check, telemetry opt-in),
  verify `IDpiaScreeningService.CheckAsync()` is called (or a DPIA exemption is
  explicitly documented). Update the DPIA register.
- Minors' data (classroom): verify that Phase 18's `IDpiaScreeningService`
  correctly applies the conservative (treat-as-minor) policy when `BirthYear` is
  absent; extend to cover the full jurisdiction matrix (Uganda DPPA, EU GDPR —
  per Phase 00 / Phase 18 owner decision).
- SAST: run Roslyn security-focused analyzers (`SecurityCodeScan`,
  `Roslynator.Security`, or equivalent); configure rules; resolve high-severity
  findings; triage mediums. Output: `docs/security/sast-report-phase-19.md`.
- LAN Host surface hardening: extend the Phase 16 threat model with the full
  STRIDE pass; verify subnet validation cannot be bypassed; verify session token
  expiry; verify graceful shutdown revokes all sessions (no dangling tokens);
  verify that the `LanHost` architectural isolation tests pass on both platforms.

**Privacy settings UI (modest):**
- Privacy settings panel: a single consolidated view (`Settings > Privacy`)
  listing all off-device features, their current tier, last DPIA result, and
  one-click disable. Includes the "Delete all AI history" and "Reset all privacy
  settings to defaults" actions.

### Explicitly out of scope

- New features (Phase 19 is a hardening and verification phase, not a feature phase).
- Full penetration test by an external party (recommended as a Phase 23 pre-launch
  activity — owner ask §14.4).
- Phase 22 signing pipeline implementation (Phase 19 establishes policy and tests).
- Performance hardening (Phase 20).
- Linux.

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| CTRL-OGMA-001 | MVP | API keys / secrets in OS credential store | Audit of all `ICredentialStore` call sites; lint rule; unit tests per phase |
| CTRL-OGMA-002 | MVP | No secret in plain text in any persistent store | Static analysis + SAST scan; integration test: no key string in SQLite DB file |
| CTRL-OGMA-003 | MVP | Credential store key rotation tested | Unit test: rotate key → retrieve new key → old key inaccessible |
| CTRL-OGMA-004 | V1 | PDF workers isolated from main process | Isolation test: malformed PDF → worker terminates → no main-process crash |
| CTRL-OGMA-005 | V1 | Worker has no network access | Fault-injection: worker attempts HTTP → blocked; main process continues |
| CTRL-OGMA-006 | V1 | Worker writes only to designated temp dir | Fault-injection: worker attempts write outside temp dir → blocked |
| CTRL-OGMA-007 | V1 | Worker cannot spawn child processes | Isolation test: crafted PDF triggers child-process spawn attempt → blocked |
| CTRL-OGMA-008 | MVP | Path validation: all file I/O validated against library root | Fuzz test: `PathGuard` with 50 traversal patterns; all rejected |
| CTRL-OGMA-009 | MVP | Library root is canonicalized before storage | Unit test: symlink, relative, UNC path → normalized to absolute canonical path |
| CTRL-OGMA-010 | MVP | LAN asset endpoint path-traversal prevention | Fuzz test on Phase 16 asset endpoint bookId; 0 traversal successes |
| CTRL-OGMA-011 | MVP | Sidecar writes go only to designated sidecar folder | Integration test: sidecar write attempt outside sidecar → exception |
| CTRL-OGMA-012 | V1 | Update descriptor signed; signature verified before apply | Tampered-descriptor test: altered descriptor → update rejected |
| CTRL-OGMA-013 | V1 | Update package signed and verified independently of transport | Man-in-the-middle test: altered package → verified payload hash mismatch → rejected |
| CTRL-OGMA-014 | V2 | At-rest encryption for student private DB (SQLCipher or app-level) | Integration test: raw DB bytes do not contain recognizable JSON/text annotation content |
| CTRL-OGMA-015 | V2 | Main catalogue at-rest encryption (opt-in) | Integration test: encrypted catalogue → correct read/write via `IAtRestEncryptionService` |
| CTRL-OGMA-016 | MVP | No off-device call without user consent | Architecture test: all HTTP egress routes through `IAiProvider`; no direct `HttpClient` in Domain/Application layers |
| CTRL-OGMA-017 | MVP | Payload preview before every off-device AI call | Integration test: every AI call path invokes `IAiPrivacyService.BuildPreviewAsync()` |
| CTRL-OGMA-018 | MVP | All off-device calls logged to tamper-evident audit trail | Audit-completeness test: 20 AI/enrichment calls → 20 audit rows; no gap |
| CTRL-OGMA-019 | V1 | AI query history erasure: user can delete own history | Integration test: delete → `AiQueryHistory` rows cleared; subsequent AI call starts clean history |
| CTRL-OGMA-020 | MVP | LAN listener bound to RFC-1918 subnet only | Integration test: non-RFC-1918 source IP → connection rejected at listener |
| CTRL-OGMA-021 | V2 | Student AI queries bounded by school privacy policy | Integration test: ContentAware tier blocked for minor profile when not admin-opted-in |
| CTRL-OGMA-022 | V2 | AI output bounded to curated collection (answer mode) | Integration test: fabricated citation → stripped by `ClassroomAnswerGrounder` |
| CTRL-OGMA-023 | V1 | Telemetry is opt-in; no personal data in telemetry payload | Architecture test: no `ProfileId`/`BookId`/annotation content in telemetry event DTO |
| CTRL-OGMA-024 | V2 | DPIA per off-device feature; minors' data protected | DPIA coverage matrix: every off-device feature has a DPIA record; minor-profile test |
| NFR-PROD-011 | MVP | Privacy-tier + payload-preview on every AI/enrichment path | Integration test: every path through `IAiProvider` invokes preview; no bypass |
| NFR-PROD-012 | MVP | Signed builds + reversible migrations | Update-signing test; migration UP/DOWN tests |
| NFR-PROD-013 | MVP | Local audit trail | Audit-completeness + ordering test |
| SRS §7 (security) | All | Full SRS §7 security requirements verified | SAST report; threat model; control matrix |

---

## 6. Dependencies

### Depends on

- **Phase 16**: LAN Host surface (highest-risk new asset; threat-modeled here).
- **Phase 17**: student private DB, session tokens, offline cache.
- **Phase 18**: `IDpiaScreeningService`, `ISchoolAiKeyProvider`, quota enforcement.
- **Phase 12**: `IAiProvider` gateway, privacy tiers, `IAiPrivacyService`.
- **Phase 05, 08, 15**: untrusted-PDF worker isolation (verify here).
- **Phase 04**: catalogue DB (at-rest encryption opt-in).

### Unblocks

- **Phase 20**: reliable performance benchmarking requires security to be settled
  (no panic-patching mid-benchmark).
- **Phase 22**: update signing pipeline implementation builds on the policy and
  tests from Phase 19.
- **Phase 23**: go-live readiness gate includes a security-posture sign-off from
  Phase 19.

---

## 7. Architecture & approach

### Threat modeling methodology

1. **STRIDE analysis** (`security-scanning:stride-analysis-patterns`): systematically
   apply Spoofing / Tampering / Repudiation / Information Disclosure /
   Denial of Service / Elevation of Privilege to every component and trust
   boundary in the HLD architecture.
2. **Trust boundaries identified:**
   - Desktop process ↔ OS file system
   - Desktop process ↔ AI providers (internet egress, through `IAiProvider`)
   - Desktop process ↔ metadata providers (HTTP, through single gateway)
   - Desktop process ↔ PDF worker (process boundary)
   - LAN Host ↔ LAN client (HTTPS, session-token authenticated)
   - LAN Host ↔ AI providers (internet egress, `ISchoolAiKeyProvider`)
   - LAN Host listener ↔ LAN network (new highest-risk boundary)
   - OS credential store ↔ application
   - Velopack update feed ↔ application
3. **Attack trees** (`security-scanning:attack-tree-construction`): for the
   top-5 threats from STRIDE. Proposed candidates:
   - T1: Client device exfiltrates school AI API key.
   - T2: Malformed PDF escapes worker isolation and writes to library root.
   - T3: Unauthenticated client accesses Host catalogue or PDF content.
   - T4: Student reads another student's private annotations.
   - T5: Tampered update binary executed on user device.
4. **Threat-to-control mapping** (`security-scanning:threat-mitigation-mapping`):
   output a matrix: Threat → CTRL-OGMA control(s) → test that verifies the control.

### At-rest encryption (CTRL-OGMA-014..015)

**Student private DB (CTRL-OGMA-014 — mandatory for V2):**
Option A (preferred): SQLCipher — replaces SQLite; same EF Core provider via
`EntityFrameworkCore.Sqlite` with a compile-switch. Key = HKDF-SHA256 derived
from a device secret in `ICredentialStore`.
Option B (fallback): application-level encryption of sensitive columns (annotation
body, AI history query text) using `AesGcm`; less comprehensive but avoids
SQLCipher dependency.
Decision: recorded in an ADR amendment to ADR-0011 or a new ADR-0013 if the
SQLCipher vs. app-level choice is significant.

**Main catalogue (CTRL-OGMA-015 — opt-in):**
Same approach; admin toggle in Settings > Privacy. Default off. When enabled,
existing catalogue file is encrypted in-place after a backup (reversibility).

### SAST configuration

`security-scanning:sast-configuration` + `security-scanning:security-sast`:
- Roslyn analyzers: `SecurityCodeScan.VS2019`, `SonarAnalyzer.CSharp`, `Roslynator`.
- Rules explicitly enabled: SQL injection, path traversal, insecure deserialization,
  hardcoded credentials, weak cryptography (DES/3DES/RC4/MD5 for security use).
- Output: SARIF report; zero high-severity findings required for Phase 19 DoD;
  medium findings triaged with risk acceptance or fixed.
- CI integration: SAST step added to both Windows and macOS CI pipelines.

### Privacy settings UI

New view: `Settings > Privacy` (`PrivacySettingsView.axaml`).
Sections:
1. Off-device features table: feature name, current privacy tier, last DPIA
   result (`Pass` / `Disqualified` / `Not configured`), toggle to disable.
2. AI history: "Delete all AI history" button (confirmation required).
3. Data erasure: "Reset all privacy settings to defaults" button.
4. Encryption: toggle for main catalogue at-rest encryption.

Icons: `ic_privacy_settings`, `ic_data_erase`, `ic_encryption_lock`.

### Cross-platform notes

| Concern | Windows | macOS |
| --- | --- | --- |
| SQLCipher | `SQLitePCLRaw.bundle_e_sqlite3` with SQLCipher native lib | Same; macOS dylib bundled |
| Path traversal | UNC paths (`\\server\share`), `%APPDATA%` expansion, NTFS alternate data streams | POSIX symlinks, `/proc`, `~/Library` expansion |
| Worker isolation | Windows Job Objects (`AssignProcessToJobObject`, `SetInformationJobObject`) or `SandboxPolicy` | macOS App Sandbox entitlements or `posix_spawn` with reduced capabilities |
| Keychain interaction | DPAPI via `ProtectedData.Protect()` | `SecKeychain` / `SecItem` APIs |

---

## 8. Work breakdown (summary)

Full task detail in `tasks.md`.

| Work package | Key tasks | Est. |
| --- | --- | --- |
| **WP1 — Threat model** | STRIDE analysis; attack trees (T1..T5); threat-to-control matrix | 3 d |
| **WP2 — CTRL-OGMA-001..003 (credential store)** | Audit all credential-store call sites; lint rule; key rotation test | 1 d |
| **WP3 — CTRL-OGMA-004..007 (PDF worker isolation)** | Verify/harden isolation; malformed-PDF fault injection; worker boundary tests | 2 d |
| **WP4 — CTRL-OGMA-008..011 (path validation)** | `PathGuard` utility; audit all I/O paths; fuzz test traversal; LAN asset endpoint fuzz | 2 d |
| **WP5 — CTRL-OGMA-012..013 (signed updates)** | Signing key policy; tampered-descriptor test; rollback test | 1 d |
| **WP6 — CTRL-OGMA-014..015 (at-rest encryption)** | `IAtRestEncryptionService`; SQLCipher or app-level for student DB; opt-in for catalogue | 2 d |
| **WP7 — CTRL-OGMA-016..023 (off-device controls)** | Privacy tier audit; payload-preview completeness; telemetry audit; LAN subnet validation | 2 d |
| **WP8 — CTRL-OGMA-024 (DPIA + minors)** | DPIA register; jurisdiction matrix; minor-profile coverage; classroom DPIA completeness | 2 d |
| **WP9 — SAST** | Configure analyzers; run scan; fix high-severity findings; triage mediums | 1 d |
| **WP10 — Privacy settings UI** | `PrivacySettingsView`; DPIA status display; data-erase actions; at-rest encryption toggle | 1.5 d |
| **WP11 — `/security-review` & resolution** | Full `/security-review`; resolve all findings; sign off | 2 d |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons + manifest**: `icons.md` defines `ic_privacy_settings`,
  `ic_data_erase`, `ic_encryption_lock`, `ic_threat_model` — all `⬜ to procure`.
  (Modest icon set — this is primarily a hardening phase.)
- [x] **i18n (en/fr)**: privacy settings panel strings and data-erase
  confirmation copy externalized; fr translations in same PR; pseudolocale check.
- [x] **Accessibility**: Privacy settings table has proper ARIA; data-erase
  button is destructive — confirmation dialog is keyboard-navigable and
  screen-reader-announced; encryption toggle ARIA role `switch`.
- [x] **Privacy/egress**: this phase verifies the entire privacy control set —
  the output is a verified CTRL-OGMA matrix, not new egress features.
- [x] **Reversibility**: at-rest encryption is applied after a backup; key
  rotation is non-destructive; data-erase confirmation is required.
- [x] **Performance budgets**: SAST scan must not introduce regressions;
  `PathGuard` must be O(1) per call (simple prefix comparison, no filesystem
  stat); at-rest encryption/decryption adds < 5 ms P95 to SQLite open (verified).
- [x] **Bounded-context tests**: `ArchTests_NoDirectHttpClientInDomainOrApplication`
  (all egress through `IAiProvider`) verified and green; new tests for
  at-rest encryption context isolation.
- [x] **Documentation**: `docs/security/threat-model-phase-19.md` (STRIDE output);
  `docs/security/ctrl-ogma-matrix.md` (control verification matrix);
  `docs/security/sast-report-phase-19.md`; DPIA register updated;
  ADR amendments if needed.

---

## 10. Definition of Done

### Global DoD

- [ ] Every in-scope FR/NFR/CTRL ID has a passing test or a tagged gap.
- [ ] Golden-corpus suite green; no open R1/R2 defect.
- [ ] `dotnet format`, `dotnet build`, `dotnet test`, architecture tests pass.
- [ ] Builds and tests pass on **both Windows and macOS** CI runners.
- [ ] New user strings externalized and present in **en + fr**; pseudolocale.
- [ ] Every new control has a colorful icon + accessible label; `icons.md` complete.
- [ ] ADRs/decisions recorded; reference docs updated.
- [ ] Performance budgets instrumented.
- [ ] `/security-review` completed and all findings resolved.

### Phase-19-specific exit criteria

- [ ] STRIDE threat model document complete and reviewed:
      `docs/security/threat-model-phase-19.md`.
- [ ] Attack trees for T1..T5 complete; each tree leaf maps to a CTRL-OGMA
      control or an accepted residual risk.
- [ ] CTRL-OGMA matrix (`docs/security/ctrl-ogma-matrix.md`) lists all 24
      controls; each has status `Verified`, `Accepted-gap`, or `Deferred` with
      the evidence.
- [ ] CTRL-OGMA-001..003 verified: no plain-text secret in SQLite, logs, HTTP
      responses (secret scan passes).
- [ ] CTRL-OGMA-004..007 verified: malformed-PDF fault injection passes on
      Windows and macOS.
- [ ] CTRL-OGMA-008..011 verified: `PathGuard` fuzz test with 50 traversal
      patterns passes; LAN asset endpoint fuzz passes.
- [ ] CTRL-OGMA-014 implemented: student private DB encrypted; raw bytes do not
      contain recognizable annotation text.
- [ ] CTRL-OGMA-016..017 verified: architecture test `NoDirectHttpClientInDomain`
      passes; every AI call path invokes payload preview.
- [ ] CTRL-OGMA-024 verified: DPIA register covers every off-device feature;
      minor-profile without `BirthYear` correctly blocked from ContentAware AI.
- [ ] SAST scan: zero high-severity findings; medium findings documented in SAST
      report with disposition (fix / accept / false-positive).
- [ ] `/security-review` sign-off from a reviewer who has read the threat model.
- [ ] Privacy settings UI (`Settings > Privacy`) keyboard-navigable and SR-
      verified on both Windows and macOS.

---

## 11. Skills to use

Full guidance in `skills.md`. Key skills:

- `security-scanning:stride-analysis-patterns` — STRIDE threat model (WP1).
- `security-scanning:attack-tree-construction` — attack trees T1..T5 (WP1).
- `security-scanning:threat-mitigation-mapping` — control matrix (WP1).
- `security-scanning:security-hardening` — PDF worker isolation hardening (WP3),
  path validation (WP4), at-rest encryption (WP6).
- `security-scanning:security-sast` + `security-scanning:sast-configuration`
  — SAST tooling and rule configuration (WP9).
- `security-scanning:security-requirement-extraction` — map CTRL-OGMA controls
  to test cases (WP2..WP8).
- `security:dpia-generator` + `security:uganda-dppa-compliance` — DPIA
  register and jurisdiction matrix (WP8).
- `/security-review` — full phase security review (WP11).
- `comprehensive-review:security-auditor` — independent review of WP1 threat
  model and WP11 findings.

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| STRIDE threat model | `docs/security/threat-model-phase-19.md` |
| Attack trees (T1..T5) | `docs/security/attack-trees-phase-19.md` |
| CTRL-OGMA control matrix | `docs/security/ctrl-ogma-matrix.md` |
| SAST report | `docs/security/sast-report-phase-19.md` |
| DPIA register (all off-device features) | `docs/security/dpia-register.md` |
| `PathGuard` utility | `src/OgmaLibrary.Infrastructure/Security/PathGuard.cs` |
| `IAtRestEncryptionService` + implementation | `src/OgmaLibrary.Application/Security/IAtRestEncryptionService.cs` |
| Privacy settings view | `src/OgmaLibrary.App/Views/Settings/PrivacySettingsView.axaml` |
| Architecture tests (new) | `src/OgmaLibrary.Tests/Architecture/SecurityIsolationTests.cs` |
| Fuzz + fault-injection tests | `src/OgmaLibrary.Tests/Security/` |
| SAST analyzer configuration | `.editorconfig` / `Directory.Build.props` (analyzer packages + rule config) |
| `icons.md` | `docs/plans/grand-plan/phase-19/icons.md` |

---

## 13. Risks

| Risk | R-tier | Mitigation |
| --- | --- | --- |
| SQLCipher introduces a new native dependency that fails to build on macOS | R4 | Spike SQLCipher cross-platform in WP6 before committing; app-level column encryption is the fallback |
| STRIDE reveals a threat with no existing CTRL-OGMA control | R2 | New control(s) added; threat accepted only with explicit owner sign-off |
| DPIA jurisdiction matrix not complete by Phase 19 start (Phase 00 gap not resolved) | R2 | Conservative fail-safe: DPIA disqualifies all minor-profile ContentAware calls until jurisdiction configured; Phase 00 owner ask escalated |
| SAST produces > 50 medium findings, blocking Phase 19 DoD | R3 | Triage with owner: fix, accept, or mark false-positive; high-severity always fixed; medium-severity triaged within 2 d |
| Malformed-PDF worker isolation test exposes a real escape path | R2 | Fix before Phase 19 closes; do not accept this as a gap |
| At-rest encryption key loss (device wipe, credential store corruption) | R1 | Key derivation uses OS credential store; document recovery procedure; backup before encryption (CTRL-OGMA-014 pattern) |

---

## 14. Owner asks

1. **Jurisdiction matrix sign-off**: confirm which jurisdictions (Uganda DPPA,
   EU GDPR, UK GDPR, others) the DPIA register must cover for V2. This
   unblocks `IDpiaScreeningService` implementation in Phase 18 and the DPIA
   register in Phase 19.
2. **At-rest encryption scope**: confirm that student private DB encryption
   (CTRL-OGMA-014) is mandatory for V2 and main catalogue encryption
   (CTRL-OGMA-015) is opt-in. Confirm whether SQLCipher is acceptable as a
   dependency (open-source, BSL 1.0 or commercial license options).
3. **External penetration test**: strongly recommended to commission an external
   pen test before the V2 classroom product goes to schools. This is an owner
   decision for timing (Phase 23 pre-launch is the proposed window).
4. **Icon procurement**: please procure the 4 privacy/security icons listed in
   `icons.md`: `ic_privacy_settings`, `ic_data_erase`, `ic_encryption_lock`,
   `ic_threat_model` (used in security docs, not shipping UI — but `ic_privacy_settings`
   and `ic_data_erase` and `ic_encryption_lock` are UI-shipping icons).
5. **CTRL-OGMA acceptance**: any control where Phase 19 finds a genuine gap
   that cannot be closed in 3 weeks requires Peter's explicit risk acceptance.

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Planning agent | Initial v1.0 draft |
