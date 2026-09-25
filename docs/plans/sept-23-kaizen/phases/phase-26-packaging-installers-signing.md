# Phase 26 — Packaging, installers and signing

## 1. Header

| Field | Value |
|---|---|
| Wave | G. Ship |
| Size | 6–9 engineering days, plus certificate procurement lead time (often 1–3 weeks for organisation validation) |
| Depends on | 24 (performance and reliability evidence), D-08 |
| Owner decisions | **D-08** installer technology; certificate purchase (Windows code signing, Apple Developer Program); update hosting location |
| Primary defects | K06, K92 |
| Requirements | NFR distribution and update set, CTRL-OGMA-012/013 (signed builds, verified updates), ADR-0009 |

## 2. Why this phase exists

Nobody outside the development machine can install Ogma today.

- **No installer.** `scripts/New-ReleaseCandidate.ps1` publishes a self-contained, RID-specific
  build (`--runtime win-x64|osx-arm64 --self-contained true -p:PublishTrimmed=false
  -p:PublishSingleFile=false`), optionally signs it with `signtool` or `codesign`, submits for
  notarisation, and zips it (`OgmaLibrary-<version>-<rid>.zip`). There is no MSIX, no DMG, no
  Velopack feed and no uninstaller.
- **ADR-0009 is accepted but not implemented.** It chose Velopack for direct distribution (Windows
  and macOS, delta updates with signature verification) plus MSIX for Store and enterprise. The
  master plan's D-08 has been aligned with it (reconciled 2026-09-25). This phase recommends **keeping ADR-0009**: Velopack produces the signed Windows
  setup EXE and the macOS app/DMG with an update feed, and MSIX serves the managed channel. Any
  departure needs a superseding ADR.
- **Build output bloat (K06).** The development `bin/Release/net10.0` output contains native folders
  for about 40 RIDs (android, ios, linux-musl, s390x…). `Directory.Build.props:13` declares only
  `win-x64;osx-arm64`. The RID-specific publish should already drop the others, but this is **not
  measured**: verify the published tree, not `bin`.
- **Avalonia.Diagnostics ships in Release (K06).** `OgmaLibrary.App.csproj:29` conditions the package
  on `'$(Configuration)' == 'Debug'`, yet `OgmaLibrary.App.deps.json` in the Release output lists
  `Avalonia.Diagnostics/11.3.17`. The likely cause is that NuGet restore evaluates the condition
  without `-c Release` (restore and `packages.lock.json` are configuration-agnostic). Confirm the
  cause and move the reference to a mechanism that restore honours, for example a
  `DefineConstants`-guarded `AttachDevTools` with the package excluded from Release publish.
- **Native dependencies need explicit checks.** `pdfium.dll` (PDFtoImage), `e_sqlite3`, Tesseract
  native libraries plus `tessdata/eng.traineddata`, SkiaSharp and HarfBuzz, the `OgmaLibrary.Workers`
  executable next to the app, and the `shelf3d` bundle. Tesseract packaging on macOS is currently
  NOT ASSESSED.
- **WebView2 Runtime.** The 3D shelf needs the Evergreen WebView2 Runtime on Windows. The installer
  must detect it and offer the bootstrapper; the app must degrade clearly when it is absent (Phase 18).
- **No clean-machine evidence.** Clean install, upgrade, rollback and uninstall have never been run
  (prior-audit open gates).

The engines contain **no dedicated code-signing or notarisation skill** (engines survey). Signing and
notarisation rules change often (certificate types, timestamping, notarisation tooling, the
`codesign --deep` guidance, MSIX signing requirements). They must pass the digital-research-engine
currentness gate before implementation.

## 3. Objectives and exit criteria

1. D-08 is recorded, and ADR-0009 is either confirmed or superseded.
2. The currentness register (in `00-currentness-register.md` or this phase's evidence) holds verified
   entries for: Windows code-signing certificate options and timestamping; SmartScreen reputation
   behaviour; MSIX signing and packaging requirements; Apple Developer ID signing, hardened runtime,
   entitlements and `notarytool` usage; Velopack current version and signing hooks; WebView2
   Evergreen distribution options.
3. Windows: a signed Velopack setup EXE (per-user install, no admin) and a signed MSIX. Both install
   on a clean Windows 11 VM, launch, pass the golden-journey smoke suite, upgrade from N-1 to N,
   roll back, and uninstall without leaving program files (user data is kept unless the user
   chooses to remove it).
4. macOS: a signed, notarised and stapled DMG containing the `.app`, with `Info.plist` from
   `packaging/macos/Info.plist`. It passes Gatekeeper on a clean macOS account (executed in Phase 27
   hardware).
5. The published tree contains only the target RID's native assets; `Avalonia.Diagnostics` is absent;
   the package size is recorded and justified.
6. Update channel: the signed release descriptor and Velopack feed are verified by the existing
   `ReleaseDescriptorVerifier` and `RsaUpdateVerifier` before any update is applied; a tampered feed
   is rejected (negative test).
7. Trimming and ReadyToRun are evaluated with a measured decision (size, start time, reflection
   risk with Avalonia and EF Core). The default is no trimming unless measurement shows a safe gain.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\deployment-release-engineering\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\cicd-pipelines\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\architecture\validation-contract\SKILL.md` (seven-category release evidence bundle)
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md` (packaging section)
- `C:\wamp64\www\chwezi-dev-engine\skills\languages\csharp-dotnet-development\SKILL.md` (publish, trimming, ReadyToRun)
- `C:\wamp64\www\windows-admin-engine-skills\skills\patching-software-and-endpoint-management\windows-patch-management\SKILL.md` (software deployment on Windows)
- `C:\wamp64\www\windows-admin-engine-skills\skills\virtualization-containers-and-development\windows-hyper-v\SKILL.md` (clean VMs and checkpoints for install tests)
- `C:\wamp64\www\windows-admin-engine-skills\skills\meta\kaizen-engine-and-product-improvement\SKILL.md` (R0–R5 risk classes, canary rings, stop conditions)
- Currentness gate (mandatory): `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md`,
  `C:\wamp64\www\digital-research-engine\skills\source-evaluation\SKILL.md`,
  `C:\wamp64\www\digital-research-engine\skills\source-verification\SKILL.md`

**Risk classes (windows-admin engine).** Local builds and packaging are R0/R1. Signing with the
production certificate is R3 (irreversible reputation effect; performed only in the protected release
workflow). Publishing to the update feed is R4 (reaches all users): it needs a canary ring (owner and
two beta testers) before general release, with explicit stop conditions (signature verification
failure, install failure on any clean VM, a crash in the smoke suite).

## 5. Scope

**In:** D-08 and ADR reconciliation; currentness register; publish profile clean-up; Velopack and
MSIX packaging; the DMG build recipe; signing integration in `release-candidate.yml`; WebView2
detection; native asset verification; clean-VM install, upgrade, rollback and uninstall tests;
update-feed verification; size and start-time measurement.

**Out:** Microsoft Store submission and Mac App Store (post-beta); macOS hardware execution
(Phase 27); auto-update UX copy beyond "an update is ready" (Phase 22 for strings).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 26.1 | Obtain D-08; confirm or supersede ADR-0009. | `docs/adrs/0009-velopack-msix-dmg.md` or a new ADR | Owner decision recorded |
| 26.2 | Currentness research for every signing, notarisation and packaging rule used; register entries with sources, dates and freshness class. | `docs/plans/sept-23-kaizen/00-currentness-register.md` | No NOT_ASSESSED entry remains for rules the pipeline relies on |
| 26.3 | Certificate procurement: Windows code-signing certificate (type per 26.2), Apple Developer Program membership and a Developer ID Application certificate; store keys in the CI secret store or a cloud HSM, never in the repo. | Owner procurement; CI secrets | Test signature verifies on a clean VM |
| 26.4 | Fix `Avalonia.Diagnostics` leakage: confirm the cause, change the reference so Release restore and publish exclude it, and add a check that fails the release if it is present. | `src/OgmaLibrary.App/OgmaLibrary.App.csproj`, `scripts/Test-ReleaseCandidate.ps1` | Published tree has no `Avalonia.Diagnostics.dll`; the check fails when it is injected |
| 26.5 | Publish tree audit: list native assets per RID; assert only win-x64 or osx-arm64 folders; verify `pdfium`, `e_sqlite3`, Tesseract and `tessdata`, SkiaSharp, the Workers executable, and `shelf3d` are present and load. | `scripts/Test-ReleaseCandidate.ps1` | Test passes on both RIDs; size table recorded |
| 26.6 | Trimming and ReadyToRun experiment: build variants, measure size and cold start (Phase 24 harness), run the full journey suite. | Publish profiles | Decision recorded with numbers |
| 26.7 | Velopack integration: `vpk pack` for win-x64 and osx-arm64, signing hooks, release notes, the feed layout; wire in-app update check behind the existing descriptor verification. | `scripts/New-ReleaseCandidate.ps1`, App update service | Signed setup EXE and macOS package produced in CI |
| 26.8 | MSIX: manifest (identity, capabilities: no broad file-system capability; user-picked folders only), signing, the WebView2 dependency declaration. | `packaging/windows/` (new) | MSIX installs and runs on a clean VM |
| 26.9 | DMG recipe: `.app` bundle from `packaging/macos/Info.plist`, entitlements for hardened runtime (JIT for .NET if required, per 26.2), sign inside-out, notarise, staple. | `packaging/macos/` | Recipe executes in Phase 27 |
| 26.10 | WebView2: installer detects the Evergreen runtime and offers the bootstrapper; the app shows a clear message in the 3D view when absent. | Installer, Phase 18 view | Clean VM without WebView2: install offers it; app degrades clearly |
| 26.11 | Clean-VM test matrix on Hyper-V checkpoints: fresh install, upgrade N-1 → N with a 2k library and annotations, rollback, uninstall (keep and remove user data), non-admin account, path with Unicode user name. | Scripted `scripts/Test-InstallMatrix.ps1` | All cells pass; results table in evidence |
| 26.12 | Update security negative tests: tampered package, unsigned descriptor, downgrade attempt, wrong public key. | Update service tests | Each rejected with a localised message; nothing installed |
| 26.13 | Release workflow: signing only in the protected `release-candidate.yml` environment with required reviewers; artefact checksums; SBOM generation. | `.github/workflows/release-candidate.yml` | Dry run produces signed artefacts plus checksums plus SBOM |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| No installable product (K92) | ADR-0009 never implemented; zip only | Velopack plus MSIX plus DMG | Users can install without technical help | 0 installers → 3 signed formats | Clean-VM matrix | Certificate delays | Beta testers use the signed zip with instructions |
| Diagnostics in Release (K06) | Configuration condition evaluated at restore | Release-safe reference plus check | No dev tooling ships | Present → absent, enforced | Release check | Dev tooling lost in Debug | Keep a Debug-only attach path |
| Unverified native payload | No publish-tree audit | Asset audit per RID | Missing natives caught before users | Unmeasured → audited each build | Test output | False failures on new packages | Allowlist updated in review |
| Signing rules may be stale | No signing skill in engines | Currentness gate | Pipeline follows current vendor rules | 0 verified claims → all verified | Register | Rules change after release | Re-run the gate every release |

## 8. Test plan

- **Unit and integration:** update verifier negative cases; release-candidate checks.
- **Clean-VM matrix (Windows):** install, upgrade, rollback, uninstall, non-admin, Unicode profile
  path, no WebView2, offline install.
- **Smoke suite:** the Phase 01 golden journeys run against the installed build, not the dev build.
- **macOS:** executed in Phase 27 using the recipe produced here.

## 9. Acceptance commands

```powershell
./scripts/New-ReleaseCandidate.ps1 -Platform windows -Version 0.9.0-beta.1 -WindowsCertificateThumbprint <thumbprint>
./scripts/Test-ReleaseCandidate.ps1 -Platform windows
./scripts/Test-InstallMatrix.ps1 -VmName OgmaClean-Win11
./scripts/Test-ReleaseAcceptance.ps1
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence while open |
|---|---|---|
| Windows code-signing certificate | Owner (purchase and identity validation) | No public Windows release; SmartScreen warnings |
| Apple Developer Program and Developer ID | Owner | No macOS release |
| Update feed hosting | Owner (choose host) | No auto-update; manual downloads only |
| macOS DMG execution and Gatekeeper | Phase 27 | macOS release blocked |
| SmartScreen reputation | Time and download volume | First users may see warnings; document it in the install guide |

## 11. Risks and mitigations

- **Certificate lead time.** Start 26.3 as soon as wave E begins, not at Phase 26.
- **Velopack and MSIX drift apart.** One publish output feeds both; the install matrix tests both.
- **Trimming breaks reflection (Avalonia bindings, EF Core).** Default to untrimmed; accept trimming
  only with a full journey pass.
- **User data removed on uninstall.** Default is keep; removal needs explicit confirmation.

## 12. Execution prompt

```
## Prompt 26 - Packaging, installers and signing
You are making Ogma Library installable and updatable in C:\wamp64\www\Ogma-Library. Read first, in order:
C:\wamp64\www\Ogma-Library\CLAUDE.md; docs/plans/sept-23-kaizen/README.md; docs/plans/sept-23-kaizen/AGENT_BRIEF.md;
docs/adrs/0009-velopack-msix-dmg.md; docs/plans/sept-23-kaizen/phases/phase-26-packaging-installers-signing.md.
Read the SKILL.md files in section 4 directly. Run the digital-research currentness gate BEFORE writing any
signing or notarisation step; do not rely on remembered vendor rules.
A. Serial: 26.1 decision -> 26.2 currentness -> 26.3 certificates (owner) -> 26.4/26.5 publish hygiene.
B. Then: 26.6 trimming experiment, 26.7 Velopack, 26.8 MSIX, 26.9 DMG recipe, 26.10 WebView2.
C. Verification: 26.11 clean-VM matrix, 26.12 update negatives, 26.13 protected workflow.
Signing with production keys happens only in the protected CI environment (risk class R3); publishing to the
feed is R4 and needs a canary ring. Never commit keys. Record completion at
docs/implementation/execution/phase-sept23-26-completion.md with NOT ASSESSED items and owners.
```
