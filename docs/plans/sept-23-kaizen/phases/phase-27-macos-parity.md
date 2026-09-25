# Phase 27 — macOS parity and cross-platform acceptance

## 1. Header

| Field | Value |
|---|---|
| Wave | G. Ship |
| Size | 6–10 engineering days on Mac hardware, plus notarisation turnaround |
| Depends on | 26 (packaging recipe and certificates); an Apple silicon Mac available to the implementer |
| Owner decisions | Provide or approve the Mac hardware (R-Mac: Apple silicon, 8 GB RAM, current macOS); Apple Developer Program (Phase 26) |
| Primary defects | K92, K80 (macOS containment), K22-class journeys re-run on macOS, F22 (Cmd parity), K82 |
| Requirements | All platform NFRs; READ, LIB, SEARCH, AI, LAN and CLIENT journeys on macOS; CTRL secret custody on macOS |

## 2. Why this phase exists

Ogma is specified as a Windows **and** macOS product, but in this cycle macOS has never been built,
launched or used (K92). CI runs on `macos-latest` (`.github/workflows/ci.yml:23`), yet the tests
that matter return early off Windows:

- `Ocr/Phase24RealOcrCorpusTests.cs:19` returns on non-Windows, so real Tesseract OCR has never run
  on macOS.
- `Security/PasswordProviderTests.cs:40` skips the real Keychain path, so
  `MacOsKeychainPasswordProvider` is untested.
- Commit `e7d83ab` ("support macOS command modifiers") added Cmd handling in three places, but it was
  never exercised on a Mac.

Platform-specific code paths that need physical evidence:

- **PDF worker containment.** `PdfWorkerClient.cs:460-466` requires the Windows Job Object only when
  `OperatingSystem.IsWindows()`. On macOS the worker runs **without any CPU, memory or child-process
  limit**, and the worker executable name switches to `OgmaLibrary.Workers` (`:711`). This is a
  security gap (Phase 23 design, implemented here).
- **3D shelf.** `CorePlatformModule.cs` selects `WKWebViewBridge` on macOS. WebGL2 availability in
  WKWebView and the bridge message flow are unmeasured.
- **Secrets.** `MacOsKeychainPasswordProvider` for AI keys, school keys and classroom credentials.
- **Paths and data.** `~/Library/Application Support/...`, case-insensitive-but-preserving APFS,
  NFD/NFC Unicode filename normalisation (the Unicode test book name `Ngũgĩ-style Stories — Unicode
  Title.pdf` is a natural probe), security-scoped access to folders outside the sandbox if the app
  is ever sandboxed.
- **Signing.** The DMG recipe from Phase 26: hardened runtime, entitlements, notarisation, stapling
  and Gatekeeper.
- **Input and conventions.** Cmd shortcuts, the app menu (About, Settings… ⌘,, Quit ⌘Q), window
  close vs. app quit, trackpad zoom and scroll in the reader, Retina rendering sharpness (the
  Windows reader was already blurry, K33).
- **Accessibility.** VoiceOver has never been used on Ogma.

## 3. Objectives and exit criteria

1. The app builds, publishes (`osx-arm64`), signs, notarises and staples through the Phase 26 recipe,
   and opens on a clean macOS user account with no Gatekeeper warning.
2. The full Phase 01 golden-journey suite passes on macOS (using a macOS automation driver; see 27.2),
   with screenshots at 1440×900 and 1920×1080 logical sizes.
3. PDF worker containment on macOS enforces CPU time, memory and no-child-process limits, and fails
   closed if the limits cannot be applied. The Phase 23 hostile corpus passes on macOS.
4. Keychain round trips work for every secret type; revoking an item is handled gracefully.
5. OCR produces the expected words for the golden-corpus scanned fixture on macOS, with the native
   Tesseract library and `tessdata` loaded from the app bundle.
6. The 3D shelf renders in WKWebView with WebGL2, or shows the documented fallback. Its scene and
   cover assets load from the correct root (K20 fix verified on macOS).
7. Cmd shortcut parity: every Ctrl shortcut on Windows has a Cmd equivalent, and the app menu
   follows macOS conventions.
8. VoiceOver can complete the first-run, open-book, read and search journeys, with findings logged
   and Critical/High ones fixed (in coordination with Phase 21).
9. Phase 24 budgets are measured on R-Mac.
10. Cross-platform acceptance: `./scripts/Test-ReleaseAcceptance.ps1` passes with evidence from both
    platforms and both reference machines.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md` (platform integration, WebView fallback)
- `C:\wamp64\www\chwezi-dev-engine\skills\languages\csharp-dotnet-development\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\security\vibe-security-skill\SKILL.md` (macOS containment threat entries)
- `C:\wamp64\www\chwezi-dev-engine\skills\architecture\validation-contract\SKILL.md` (two-platform evidence)
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\accessibility-wcag-2-2-compliance\SKILL.md` (VoiceOver pass)
- `C:\wamp64\www\design-system-skills\skills\07-mobile-ios-android-cross-platform\cross-platform-design-parity\SKILL.md` (parity principles; it is mobile-oriented, so no engine skill covers macOS desktop HIG — use the currentness gate for menu, shortcut and window conventions)
- Currentness gate: `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md`
  for macOS version support, notarisation, hardened-runtime entitlements for .NET, and WKWebView WebGL2 support.

Note: the Windows-only `windows-desktop-e2e-testing` skill does not cover macOS automation. Choose the
macOS driver (27.2) through the currentness gate and record the decision.

## 5. Scope

**In:** build, sign and notarise on hardware; macOS worker containment; Keychain, OCR, WKWebView,
input conventions, Retina rendering, VoiceOver pass; macOS golden journeys; budgets on R-Mac;
two-platform acceptance run.

**Out:** Intel Macs (unless the owner requests `osx-x64`); Mac App Store sandbox packaging; iPad and
mobile (excluded by scope).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 27.1 | Provision R-Mac: clean user account, Xcode command-line tools, .NET SDK pinned by `global.json` (Phase 00), Node for `shelf3d`; record the machine in `docs/performance/reference-machines.md`. | Hardware | `dotnet --info` and `sw_vers` recorded |
| 27.2 | macOS real-window driver: port the Phase 01 harness journeys to macOS Accessibility (AX) automation using the driver chosen via the currentness gate; keep the journey definitions shared. | `tests/` real-window harness | First-run journey runs on macOS |
| 27.3 | Worker containment on macOS: apply `setrlimit` (CPU, address space, file size, `RLIMIT_NPROC`) in the child before exec, or an equivalent supervised launch; fail closed if it cannot be applied; mirror the Windows timeouts; make it testable. | `src/OgmaLibrary.Infrastructure/Pdf/PdfWorkerClient.cs:460-466`, a new macOS limit class, `src/OgmaLibrary.Workers` | Hostile corpus on macOS: budget kills are observed, the app stays alive |
| 27.4 | Keychain: make the password-provider tests run on macOS CI (remove the early return, use a test keychain); cover add, read, update, delete and access-denied. | `Security/PasswordProviderTests.cs:40`, `MacOsKeychainPasswordProvider.cs` | Tests run and pass on `macos-latest` and R-Mac |
| 27.5 | OCR on macOS: bundle the native Tesseract and Leptonica libraries for `osx-arm64` (source and licence recorded), load `tessdata` from the bundle, and enable `Phase24RealOcrCorpusTests` on macOS. | Infrastructure OCR, publish profile | Test passes on macOS CI and R-Mac |
| 27.6 | WKWebView 3D: verify WebGL2, the message bridge, scene sequencing (Phase 18 fix) and the asset root; measure frame time with 2k books. | `WKWebViewBridge`, `shelf3d` | Renders or falls back per spec; evidence screenshots |
| 27.7 | Input and menus: Cmd equivalents for all shortcuts (audit Ctrl-only handlers beyond the three touched by `e7d83ab`); native app menu (About, Settings ⌘,, Quit ⌘Q, Window); close-window vs. quit semantics; trackpad pinch-zoom and two-finger scroll in the reader. | App shell, reader view | Keyboard and trackpad journey passes on R-Mac |
| 27.8 | Retina and rendering: the reader rasterises at the backing scale factor; covers are sharp; check no hard-coded pixel sizes break at 2×. | Reader render path (Phase 04/12 work) | Side-by-side 100 % zoom screenshot is sharp |
| 27.9 | File system: NFC/NFD normalisation when matching paths and search terms; case-only renames; folders on external APFS and exFAT drives; iCloud Drive placeholder files handled as unavailable, not as corrupt. | Discovery and path guard | Fixture tests pass on macOS |
| 27.10 | Sign, notarise, staple and Gatekeeper check using the Phase 26 recipe; install from the DMG on a clean account; upgrade and uninstall. | `packaging/macos/` | `spctl` assessment passes; journeys pass on the installed build |
| 27.11 | VoiceOver pass on the core journeys, with a findings log; fix Critical/High findings with Phase 21. | Views | Findings log; re-test passes |
| 27.12 | Budgets on R-Mac with the Phase 24 harness. | `scripts/Measure-OgmaBudgets.ps1` | Budget table for R-Mac |
| 27.13 | Two-platform acceptance: run the release acceptance contract with evidence from R-Win-Low, R-Win-Std and R-Mac. | `scripts/Test-ReleaseAcceptance.ps1`, `packaging/release-acceptance.schema.json` | Contract passes |

**T27.K82: Verify macOS worker containment (K82).** Run the Phase 23 runaway-fixture tests on Apple silicon hardware and confirm the macOS limits and fail-closed start. This phase measures; Phase 23 implements.
*Acceptance:* runaway fixtures terminate within limits on macOS; results recorded as MEASURED, or NOT ASSESSED with an owner if hardware is unavailable.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| macOS never run (K92) | No hardware in the loop; tests skip off Windows | Hardware plus macOS driver | Real defects surface before users | 0 journeys → full suite green | Harness output | Hardware delay | Beta is Windows-first with an explicit notice |
| Unbounded PDF worker on macOS | Containment written for Windows only | `setrlimit`-based limits, fail closed | Hostile PDFs cannot exhaust the Mac | No limits → enforced | Hostile corpus on macOS | Limits too tight for large books | Budget tuned with Phase 24 data |
| Keychain and OCR untested | Early-return tests | Real tests on macOS CI | Secret and OCR paths proven | Skipped → passing | CI logs | Keychain prompts in CI | Dedicated test keychain |
| Cmd parity unproven (F22) | Only 3 handlers changed | Full shortcut audit | Mac users can work by keyboard | Untested → journey passes | Keyboard journey | Conflicts with system shortcuts | Follow macOS HIG table |

## 8. Test plan

- **CI (`macos-latest`):** Keychain, OCR, path normalisation and containment unit and integration tests
  no longer skip.
- **R-Mac real window:** golden journeys, keyboard-only, trackpad, VoiceOver, 3D.
- **Installed build:** DMG install, first launch under Gatekeeper, upgrade, uninstall.
- **Negative:** Keychain access denied; revoked notarisation ticket simulated by a modified bundle (must
  fail `spctl`); hostile PDFs; iCloud placeholder files; external drive unplugged during a scan.

## 9. Acceptance commands

```bash
dotnet test OgmaLibrary.sln --configuration Release -m:1 --filter "Category!=Benchmark&Category!=Soak"
pwsh ./scripts/New-ReleaseCandidate.ps1 -Platform macos -Architecture arm64 -Version 0.9.0-beta.1 -AppleSigningIdentity "<identity>" -NotaryProfile "<profile>"
spctl --assess --type execute --verbose "<path>/Ogma Library.app"
pwsh ./scripts/Measure-OgmaBudgets.ps1 -Machine R-Mac -Library 2k
pwsh ./scripts/Test-ReleaseAcceptance.ps1
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence while open |
|---|---|---|
| Apple silicon Mac (R-Mac) | Owner | Entire phase blocked; macOS release blocked |
| Apple Developer ID and notarisation | Owner (Phase 26) | Unsigned builds only for internal testing |
| VoiceOver expert review | Owner may engage an AT user | Automated and implementer checks only |
| Intel Mac support | Owner decision | Out of scope unless requested |

## 11. Risks and mitigations

- **Tesseract native libraries for arm64 are hard to source or licence.** Decide early (27.5); fall
  back to disabling OCR on macOS with a clear message, recorded as a partial requirement.
- **WKWebView lacks a needed WebGL2 feature.** Keep the accessible 2D fallback as the macOS default and
  record the gap.
- **The macOS automation driver is flaky.** Keep journeys short and idempotent; use AX identifiers set
  by Phase 21 (AutomationId parity).

## 12. Execution prompt

```
## Prompt 27 - macOS parity and cross-platform acceptance
You are bringing Ogma Library to verified parity on macOS (Apple silicon). Read first, in order:
C:\wamp64\www\Ogma-Library\CLAUDE.md; docs/plans/sept-23-kaizen/README.md; docs/plans/sept-23-kaizen/AGENT_BRIEF.md;
docs/plans/sept-23-kaizen/phases/phase-26-packaging-installers-signing.md (recipe);
docs/plans/sept-23-kaizen/phases/phase-27-macos-parity.md.
Read the SKILL.md files in section 4 directly and run the currentness gate for macOS-specific rules.
A. Serial: 27.1 hardware -> 27.2 macOS driver -> 27.3 worker containment (security first).
B. Parallel: 27.4 Keychain, 27.5 OCR, 27.6 3D, 27.7 input, 27.8 Retina, 27.9 file system.
C. Then: 27.10 sign/notarise/install, 27.11 VoiceOver, 27.12 budgets, 27.13 two-platform acceptance.
A test that returns early on macOS is not a pass: convert early returns into real runs or explicit
NOT ASSESSED records. Record completion at docs/implementation/execution/phase-sept23-27-completion.md.
```
