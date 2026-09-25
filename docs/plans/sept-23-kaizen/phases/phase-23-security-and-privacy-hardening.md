# Phase 23 — Security and privacy hardening

## 1. Header

| Field | Value |
|---|---|
| Wave | F. Quality attributes |
| Size | 6–10 engineering days, plus the external review window |
| Depends on | 05 (roots and asset location), 15 (AI gateway enabled), 19 (Host and school admin reachable) |
| Owner decisions | D-04 (derived-asset location), D-09 (client copies) as inputs; the owner appoints the independent reviewer |
| Primary defects | K80, K70, K27, K31 (residual), K81, K82 |
| Requirements | CTRL-001..032, NFR-OGMA security and privacy set, LAN-002/004/006, ADMIN-004/013, AI-004/005/009 |

## 2. Why this phase exists

Security has good foundations: a PDF input broker, an isolated worker under a Windows Job Object,
AES-GCM at-rest encryption, a path guard, OS-backed password providers, a signed update descriptor
verifier, and fail-closed AI. But the release gate has been open since 30 August, and later phases
change the attack surface.

- **Independent PDF containment review (open P0 since 30 Aug, K80).** No independent reviewer has
  attacked the worker boundary. The hostile corpus is synthetic only.
- **The containment is Windows-only.** `WindowsChildProcessLimit.cs` is the only resource and
  containment control. A search of `src/OgmaLibrary.Infrastructure/Pdf` and `src/OgmaLibrary.Workers`
  found no macOS equivalent (no `sandbox-exec`, `setrlimit` or platform branch). Until Phase 27
  measures macOS, its containment status is NOT ASSESSED.
- **Two instances can corrupt the catalogue (K70).** There is no single-instance guard. A second
  launch runs `CatalogueMigrator` backup and restore against a database the first instance has open,
  and starts a second set of workers and a second LAN listener.
- **The app writes into the user's library folder (K27).** It creates `<library>/.ogma/covers` and
  `.ogma/spines` (measured) without disclosure. That affects read-only, removable, network and
  cloud-synchronised folders.
- **New surfaces from phases 15, 19 and 20.** Real AI providers (prompt injection from PDF text,
  payload minimisation, key custody), a reachable classroom Host (TLS, TOFU, join codes, range
  streams) and student profiles (a DPIA covering minors).
- **Information disclosure (K81).** Raw SHA-256 values and raw `ex.Message` text appear in the UI.

## 3. Objectives and exit criteria

1. The STRIDE threat model is refreshed for the post-Phase-19 architecture and stored as
   `docs/security/sept23-threat-model.md`, superseding `docs/security/phase-04-threat-model.md`
   for current risk. Every threat has a control, test or accepted-risk entry with an owner.
2. An independent reviewer (not the implementer) has attacked the PDF worker boundary. All
   findings are Critical/High-closed or have signed risk acceptance. The report is stored under
   `docs/security/`.
3. A hostile-PDF corpus of at least 60 files (malformed xref, deep object nesting, decompression
   bombs, huge page counts, JavaScript and launch actions, embedded files, oversized images,
   truncated streams, encrypted with odd handlers, polyglots) runs in CI through the real worker.
   Every file ends in a classified outcome within its budget. The app never crashes and never hangs.
4. The single-instance guard is in place. A second launch activates the first window and exits.
   Migration never restores a backup over a database held by another process.
5. Derived-asset writes follow D-04, with disclosure in Settings. Nothing is written into a library
   folder that is read-only or that the user has not opted into.
6. Secrets (AI provider keys, school AI key, Host CA private key, classroom credentials) are stored
   only in Windows Credential Manager/DPAPI or the macOS Keychain. A repository and data-folder scan
   finds none in plaintext.
7. A DPIA for school use with minors exists, produced with the `dpia-generator` skill, and the
   school-controller approval step is documented. Legal sign-off is NOT ASSESSED until the owner
   supplies it.
8. Prompt-injection tests against the Advisor and Student Smart Search pass: PDF-embedded
   instructions cannot change the tier, reveal other books' content, exceed the payload boundary or
   call tools.
9. Backup and restore are rehearsed on a real 2k-book library, with measured restore time and a
   verified integrity check.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\security\vibe-security-skill\SKILL.md` (STRIDE, role-by-resource matrix)
- `C:\wamp64\www\chwezi-dev-engine\skills\security\code-safety-scanner\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\security\dpia-generator\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-security\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-agent-safety-and-red-team\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\backend-databases\database-reliability\SKILL.md`
- `C:\wamp64\www\windows-admin-engine-skills\skills\policy-security-and-compliance\windows-security-analysis\SKILL.md`
- `C:\wamp64\www\srs-skills\05-testing-documentation\05-ai-red-team-test-plan\SKILL.md`
- Currentness gate: `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md`,
  applied to PDFium CVE status, Tesseract and pdfium package advisories, and data-protection law for
  minors in the target jurisdictions.

## 5. Scope

**In:** threat model refresh; independent containment review; hostile corpus in CI; single-instance
guard and migration locking; derived-asset location enforcement; secret-custody audit; AI injection
defences and tests; audit-log minimisation review; DPIA; backup and restore rehearsal; information
disclosure fixes; SAST and dependency re-scan.

**Out:** penetration testing of the classroom LAN by an external firm (recorded as NOT ASSESSED
unless the owner funds it); macOS worker containment implementation (designed here, built and
measured in Phase 27); code signing (Phase 26).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 23.1 | Refresh the STRIDE model: trust boundaries for the app, PDF worker, SQLite, library folders, WebView (3D), LAN Host, classroom client, AI providers, and the update channel. | `docs/security/sept23-threat-model.md`; reuse `docs/security/phase-04-risk-register.md` IDs | Every boundary has S/T/R/I/D/E entries, and every entry maps to a control ID or test |
| 23.2 | Single-instance guard: a named mutex on Windows and a lock file with `flock` on macOS, keyed by data directory. A second instance forwards activation (and an "open this PDF" argument) over a local pipe, then exits. | `src/OgmaLibrary.App/Program.cs`, new `Startup/SingleInstanceGuard.cs` | Real-window test: a second launch leaves one process and one set of workers; the first window comes to the front |
| 23.3 | Migration safety: take an exclusive database lock before backup or restore; refuse restore while another connection exists; write the backup to a temporary file and rename it atomically. | `src/OgmaLibrary.Infrastructure/Catalogue/CatalogueMigrator.cs` | Test that holds a second connection: migration aborts safely with a localised message; no data loss |
| 23.4 | Enforce the D-04 asset location. Default to app data; library sidecars are an opt-in with disclosure; probe for writability; never write to read-only roots. | Asset and sidecar services (`src/OgmaLibrary.Infrastructure/Sidecar/`, catalogue asset services) | Read-only-root fixture: zero writes under the root (file-system watcher assertion) |
| 23.5 | Build the hostile PDF corpus generator and fixtures (synthetic, original content only, no third-party copyrighted files) and a CI job that runs each file through the real worker and asserts the outcome class and budget. | `tests/OgmaLibrary.Tests/Security/`, corpus under `tests/fixtures/hostile-pdf/` with a manifest and SHA-256 | CI job green; the per-file outcome table is stored in the phase evidence |
| 23.6 | Independent containment review: prepare a briefing pack (architecture, worker protocol, Job Object limits, broker, known gaps including macOS), give the reviewer the build and corpus, and triage the findings. | `docs/security/sept23-containment-review.md` | Report received; Critical/High findings fixed or risk-accepted in writing by the owner |
| 23.7 | Secret custody audit: enumerate every secret type; confirm it is stored through `WindowsPasswordProvider`, `MacOsKeychainPasswordProvider`, `ClassroomCredentialStores` or `HostCaStores`; remove `InMemoryClassroomCredentialStore` from runtime composition, or justify keeping it. | `src/OgmaLibrary.Infrastructure/Security/`, `ClassroomClient/`, `LanHost/` | Scan of the data folder and repo: no key material in plaintext files or logs (Phase 02 logging included) |
| 23.8 | AI injection defences: a PDF-text delimiting and untrusted-content policy in the payload builder; output validation of citations against allowed book IDs; tier and consent state not modifiable by content; a red-team suite of at least 25 injected passages. | AI gateway, `LocalEvidenceAnswerPipeline`, `ClassroomAnswerGrounder` | Red-team suite passes in CI; results table in evidence |
| 23.9 | Fix F18: invalidate the remembered AI preview when payload, provider, tier or model changes (`AiGateway.cs:22`, `_previewRememberedForSession`). | `src/OgmaLibrary.Infrastructure/AI/AiGateway.cs` | Unit test for each invalidation trigger |
| 23.10 | Audit-log minimisation: review `AuditEvents` fields (319 rows for one 17-book scan, measured); remove paths and titles where an ID suffices; add a retention policy and an export redaction check. | Audit writers | Sample audit export contains no titles, paths or query text unless policy allows it |
| 23.11 | Information disclosure (K81): hide hashes behind a "Technical details" expander; map exceptions to localised, actionable messages; keep raw detail only in the Phase 02 log. | `BookDetailView`, `MainShellViewModel.cs:219-221`, folder picker `FailedFormat` | UIA scan of all screens: no raw exception text or 64-hex strings in default views |
| 23.12 | DPIA and controller approval, with the `dpia-generator` template: data inventory for student profiles, AI history, audit logs and offline caches; lawful basis; retention; erasure proof (`PurgeAiHistoryAsync`, student self-deletion). | `docs/security/sept23-dpia-schools.md` | DPIA complete; the legal-review gate is recorded NOT ASSESSED with the owner as responsible party |
| 23.13 | Backup and restore rehearsal: back up a 2k-book library with annotations; restore on a clean profile; verify counts, annotations, reading positions and encryption keys; measure time. Include `SchoolBackupService`. | Scripted rehearsal under `scripts/` | Restore verified, time recorded; failure injection (truncated backup) is rejected safely |
| 23.14 | SAST and dependency re-scan with `code-safety-scanner`; resolve new findings. | Whole solution | Report stored as `docs/security/safety-scan-sept23.md` |

**T23.K82: Cross-platform worker containment (K82).** `RequireWindowsProcessLimit` (`src/OgmaLibrary.Infrastructure/Pdf/PdfWorkerClient.cs:460-470`) fails closed only on Windows, so the untrusted-PDF worker has no CPU or memory limit on macOS. Define the macOS containment (for example `setrlimit` for CPU/address space applied by a launcher, plus the App Sandbox profile decided in Phase 27) and make the client fail closed when the platform limit cannot be applied, as on Windows. Add the hostile-corpus runs on macOS to the review scope.
*Acceptance:* a runaway fixture (CPU loop, memory balloon) is terminated on both OSs within the configured limits; the worker refuses to start when limits cannot be applied; the independent reviewer's scope includes macOS.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| No independent containment review (K80) | Reviewer never engaged; gate had no owner | Briefing pack plus appointed reviewer | External attack finds issues internal tests miss | 0 reviews → 1 completed, Critical/High = 0 open | Review report | Reviewer availability delays release | Keep the release gate blocked; do not ship |
| Two instances corrupt the DB (K70) | No process guard; migration assumes exclusivity | Mutex/flock plus migration lock | One live stack per data directory | Second launch: full stack → activation only | Real-window test | Stale lock after crash | Lock tied to the process handle; auto-released on exit |
| Undisclosed writes to the library folder (K27) | Asset root equals library root by design | D-04 policy with a writability probe | No user-folder writes by default | `.ogma` created → none unless opted in | FS-watcher test | Existing users' sidecars orphaned | Migration moves assets; the old folder is left in place until the user confirms |
| Content can steer AI | PDF text enters the payload undelimited | Untrusted-content framing and output validation | Injection success rate drops to 0 in the suite | Untested → 25/25 blocked | Red-team table | Over-blocking legitimate quotes | Tunable policy; regression cases kept |
| Raw internals in the UI (K81) | Debug-oriented views | Technical expander; error map | Users see actionable text | Hash and `ex.Message` visible → hidden | UIA scan | Support loses detail | Detail stays in the local log and diagnostics export |

## 8. Test plan

- **Unit:** mutex and lock acquisition and release; migration lock refusal; preview invalidation;
  secret-provider round trips (Windows in CI, macOS in Phase 27); audit-field redaction.
- **Integration:** the hostile corpus through the real `OgmaLibrary.Workers` process; injection suite
  against the gateway using a recorded or fake provider; backup, restore and failure injection.
- **Real window (Phase 01 harness):** second-launch activation; read-only library root journey;
  error screens show localised messages.
- **Negative:** kill the first instance mid-migration, then launch; corrupt the backup file; revoke
  a Credential Manager entry while running; a Host certificate mismatch on the client (TOFU warning).

## 9. Acceptance commands

```powershell
./scripts/Test-RequirementAccountability.ps1
dotnet restore OgmaLibrary.sln --locked-mode
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet list OgmaLibrary.sln package --vulnerable --include-transitive
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "FullyQualifiedName~Security"
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "Category=HostileCorpus"
# Real-window: single-instance and read-only-root journeys from the Phase 01 harness
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence while open |
|---|---|---|
| Independent containment reviewer | Owner appoints; engineering supports | Release blocked |
| Legal review of the DPIA and school-controller approval | Owner and school partner | Classroom mode cannot be offered to schools |
| macOS worker containment | Phase 27 | macOS release blocked |
| External LAN penetration test | Owner (budget) | Recorded as an accepted risk or a blocker, by owner decision |
| Physical backup and restore on the reference machine | Phase 24 reference machine | Restore time budget unverified |

## 11. Risks and mitigations

- **The reviewer finds a design-level escape.** Keep the worker protocol small; budget a contingency
  slice; do not weaken the release gate.
- **The single-instance guard breaks the classroom client/Host on one PC.** Key the guard by data
  directory and profile; test Host and Client on the same machine.
- **Asset relocation loses covers.** Regenerate lazily; never delete the old `.ogma/` without consent.
- **Injection defences degrade answer quality.** Measure with the Phase 16 evaluation set before and after.

## 12. Execution prompt

```
## Prompt 23 - Security and privacy hardening
You are hardening security and privacy in C:\wamp64\www\Ogma-Library. Read these first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md (invariants: NOT ASSESSED is never a pass; window is the oracle)
4. docs/plans/sept-23-kaizen/03-defect-register.md (K70, K27, K80, K81)
5. docs/plans/sept-23-kaizen/phases/phase-23-security-and-privacy-hardening.md
Then read the skills listed in section 4 (read SKILL.md files directly; they are not Skill-tool skills).
Work plan:
A. Serial: 23.1 threat model -> 23.2/23.3 single instance and migration lock -> 23.4 asset location.
B. Independent: 23.5 corpus, 23.7 secrets, 23.8/23.9 AI injection, 23.10 audit, 23.11 disclosure, 23.13 backup.
C. Owner-dependent: 23.6 reviewer engagement, 23.12 DPIA legal sign-off (record NOT ASSESSED until supplied).
Each slice: failing test first, fix, full gate set, real-window journey where UI is touched, Conventional
Commit with DCO sign-off. Never store private book content or secrets in evidence. Record the completion at
docs/implementation/execution/phase-sept23-23-completion.md with commands, results, evidence paths,
NOT ASSESSED items with owners, and the rollback commit.
```
