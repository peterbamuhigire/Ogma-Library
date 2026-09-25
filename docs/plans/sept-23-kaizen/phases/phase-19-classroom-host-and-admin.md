# Phase 19: Classroom Host and school administration

## 1. Header

| Field | Value |
|---|---|
| Wave | E, Advanced surfaces |
| Size | 8–12 engineering days |
| Depends on | Phase 08 (Settings → Classroom section replaces the env flag), Phase 15 (provider profiles and key custody feed the school AI proxy), Phase 02 (crash guard and logging), Phase 07 (Sharing becomes a proper destination) |
| Owner decisions | D-06 (which provider the school proxy uses), D-11 (teacher dashboard scope, shared with Phase 20) |
| Primary defects | K61, K51, K17 |
| Requirements | LAN-001..006, LAN-008, LAN-010 (W\*, only with `OGMA_ENABLE_CLASSROOM_HOST`), LAN-007 (B), LAN-009 (P), ADMIN-001 (B), ADMIN-002 (B), ADMIN-003/004/006/010/012/013 (W\*), ADMIN-005 (P), ADMIN-007 (P), ADMIN-008 (B), ADMIN-009 (B), ADMIN-011 (B) |

## 2. Why this phase exists

Ogma's classroom promise is that a teacher's machine shares a curated library with students on the
school network. Today a teacher cannot reach it:

- `HostSharingViewModel` is constructed only when `OGMA_ENABLE_CLASSROOM_HOST=true`
  (`src/OgmaLibrary.App/Composition/ShellModule.cs:144`). The *Sharing* button is always visible and
  opens an almost empty page with unlabeled *Start*/*Stop* buttons (`sheet2.png`, K61, K17). The client
  join controls live in the same view model, so students cannot join either.
- The Sharing view has about 25 hard-coded English attribute strings
  (`src/OgmaLibrary.App/Views/Settings/SharingSettingsView.axaml`: 18 `Text=` and 7
  `Content=`/`Watermark=`/`Header=` literals), and the view model hard-codes titles such as
  "School administration" and "Connect to Host"
  (`src/OgmaLibrary.App/ViewModels/Catalogue/HostSharingViewModel.cs:37-99`). The register counts
  about 50 in total across the view and view model.
- Publishing folders (`ILibraryPublishingService`) and shared shelves (`ISharedShelfService`)
  (`src/OgmaLibrary.Application/SchoolAdmin/SchoolAdminInterfaces.cs:4-30`) have no UI (ADMIN-001/002),
  so LAN-009 ("only published content is visible") cannot be exercised by a teacher.
- `IClientSessionService` (`src/OgmaLibrary.Application/LanHost/IClientSessionService.cs`) offers only
  issue, validate, count and **revoke all**. There is no per-session list or revoke (LAN-007).
- The school AI proxy is built with `provider.GetService<IAiProvider>()`
  (`src/OgmaLibrary.Infrastructure/SchoolAdmin/SchoolAdminServiceExtensions.cs:46-53`), which resolves the
  fail-closed `AiDisabledProvider`, so Student Smart Search always fails (K51).
- `LibraryHostService.StartAsync` has no failure handling (a port in use or a certificate error throws
  into an `async void` handler), and `KestrelHostModeListener` is never stopped at exit (journeys J8, J10).

## 3. Objectives and exit criteria

1. **Reachable.** Settings → Classroom lets the user choose a mode: *Standalone* (default), *Host this
   library for a classroom*, or *Join a classroom* (Phase 20). Choosing Host shows the Host dashboard with
   no restart. The env var remains an admin override that is labelled in the UI.
2. **Complete Host dashboard.** Status (Stopped, Starting, Running on `https://<host>:<port>`, Error with
   reason), start and stop, the join code with QR (the QR must scan; test with a phone camera, not
   `NOT ASSESSED`-by-default), connected sessions, published folders, shared shelves, class AI policy,
   quota, usage and audit export. Every control is labelled and localised (en, fr).
3. **Publishing.** The teacher picks folders and shelves to publish; clients see only published content
   (LAN-009). Unpublishing takes effect for new requests immediately.
4. **Sessions.** Extend `IClientSessionService` with `ListAsync` and `RevokeAsync(sessionId)`. The
   dashboard lists sessions (profile, device name, since, last seen) with per-session revoke and a
   session-limit setting (LAN-007).
5. **Start-failure handling.** Port conflicts, certificate failures, firewall blocks and mDNS failures
   map to specific messages with actions ("Choose another port", "Regenerate certificate", "Allow Ogma
   through Windows Firewall"). The state never sticks at *Starting*.
6. **Clean shutdown.** App exit stops the listener and mDNS advertisement within 3 s and writes an audit
   event. The Phase 01 harness asserts that the port is free after exit.
7. **School AI proxy.** The proxy resolves the provider from the Host's school provider profile
   (Phase 15), not the fail-closed default. With no key configured, Student Smart Search shows "Your
   teacher has not enabled AI" instead of failing. `ClassroomAnswerGrounder` uses the Phase 16
   grounded-answer contract (ADMIN-011).
8. **Firewall guidance.** On first Host start, detect whether an inbound rule exists for the app on the
   private profile. Show guidance, and on explicit consent create a scoped rule (Private profile only,
   program-scoped, TCP port). Never create rules silently or for the Public profile.
9. **Two-machine journey.** The Host on machine A and a client on machine B complete publish → join →
   browse → open a book → revoke. This stays **NOT ASSESSED until physically run** and is recorded with
   machine identifiers and network type.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\architecture\distributed-systems-patterns\SKILL.md` (idempotency, failure modes, timeouts)
- `C:\wamp64\www\chwezi-dev-engine\skills\security\vibe-security-skill\SKILL.md` (role-by-resource matrix for teacher, student and anonymous)
- `C:\wamp64\www\chwezi-dev-engine\skills\security\dpia-generator\SKILL.md` (minors' data; school controller)
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-model-gateway\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-cost-and-metering\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\12-data-viz-and-dashboards\dashboard-and-data-product-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\error-empty-and-system-messaging\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\internationalization-and-rtl-design\SKILL.md`
- `C:\wamp64\www\windows-admin-engine-skills\skills\meta\kaizen-engine-and-product-improvement\SKILL.md` (R0–R5 risk classes; firewall changes are R2, need consent and rollback)
- Also glob `C:\wamp64\www\windows-admin-engine-skills\skills\**\SKILL.md` for the Windows Firewall/networking skill and read it before task 8.
- `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md` (Windows Firewall API behaviour, mDNS on Windows 11)

**Typeface decision:** the dashboard uses Public Sans for body and controls, Spectral for section
headings, and JetBrains Mono for the address, port, join code and certificate fingerprint. The QR code
is rendered as a vector image, not as 3–4 px Consolas text with hard-coded black and white
(`CatalogueShellView.axaml:590`, `SharingSettingsView.axaml:687` in the engines report).

## 5. Scope

**In scope:** mode selection, Host dashboard UI and localisation, publishing and shared-shelf UI, the
session contract and UI, start-failure mapping, shutdown, the AI proxy provider, firewall guidance,
removal of the three permanently hidden host panels in `CatalogueShellView.axaml:558,658,734` (fold them
into the dashboard or delete them), and the two-machine test protocol.

**Out of scope:** the client-side join, cache and sync (Phase 20), the formal DPIA sign-off (Phase 23),
and multi-host federation.

## 6. Work breakdown

1. **Always compose the classroom services.** Construct `HostSharingViewModel` and the client view model
   regardless of the flag. Gate *availability* at runtime from Settings (Phase 08) and policy. Split the
   combined view model into `HostDashboardViewModel` and `ClassroomJoinViewModel`. Update the composition
   and architecture tests.
2. **Mode selection in Settings.** Standalone, Host or Join, persisted, with a confirmation that explains
   what Host mode exposes on the network.
3. **Host dashboard view.** Rebuild `SharingSettingsView.axaml` from design tokens. Move every literal to
   localisation keys (en and fr). Remove `Foreground="White"` and other hard-coded colours. Render the QR
   with a vector image.
4. **Publishing and shared-shelf UI.** A folder and shelf picker bound to `ILibraryPublishingService` and
   `ISharedShelfService`, with an explicit "Students will see N books" preview.
5. **Session contract.** Add `ListAsync` and `RevokeAsync(sessionId)` to `IClientSessionService` plus the
   implementation and audit events. Build the session table with revoke and the session limit.
6. **Start-failure mapping.** Wrap `LibraryHostService.StartAsync` so every failure returns a typed
   result (`PortInUse`, `CertificateInvalid`, `FirewallBlocked`, `MdnsUnavailable`, `Unknown`) and the
   state machine always leaves *Starting*. Map the results to localised messages and actions.
7. **Shutdown.** Register listener and mDNS stop in the application shutdown sequence with a 3 s
   timeout (Phase 02/24 shutdown pattern), and make `KestrelHostModeListener` disposable.
8. **Firewall guidance.** Probe the rule state. Offer consented creation of a Private-profile,
   program-scoped rule, and removal of it in Settings. Log the action as an audit event.
9. **School AI proxy.** Resolve the provider from the school profile via `IAiProviderFactory`
   (Phase 15). Enforce the policy, quota (ADMIN-008) and rate limit (ADMIN-009) already present in
   `AiProxyEndpointHandler`. Ground answers with `ClassroomAnswerGrounder` on the Phase 16 contract.
10. **Remove dead panels.** Delete or integrate the three `IsVisible="False"` host panels and their dead
    handlers (`CatalogueShellView.axaml.cs:261-329`).
11. **Two-machine protocol.** Write `docs/qa/classroom-two-machine-protocol.md`: hardware, network
    (school-like Wi-Fi with client isolation off and on), steps, expected results and evidence capture.
    Run it when two machines are available.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Host unreachable | Composition gated by an env var | Always compose; gate by Settings | Teachers can host | Host start from UI: impossible → 1 click plus confirm | Harness screenshots | Accidental exposure | Explicit confirmation; Standalone default |
| Blank or English-only page | Unfinished view, literals | Token-based localised dashboard | Clear, bilingual host UI | Literals ≈50 → 0 | String-scan CI gate | Missing fr strings | Fallback to en with warning |
| Published scope unusable | Backend-only services | Publishing UI | LAN-009 becomes verifiable | Published-only visibility test: unrun → pass | Integration test | Over-sharing | Preview count before publish |
| No per-session control | Contract lacks list/revoke | Extend contract | Teachers manage devices | Revoke one session: impossible → yes | Test plus screenshot | Token races | Revoke-all remains |
| Student AI always fails | Proxy gets the disabled provider | Profile-resolved provider | Student AI works where enabled | Proxy success with fake provider: 0 → 100 % | Test | Cost overrun | Quota and daily cap |
| Stuck at Starting / crash | No failure mapping | Typed start results | Recoverable errors | Crash on port conflict: yes → no | Negative test | — | — |

## 8. Test plan

- **Unit:** start-result mapping; session list and revoke; publish and unpublish filtering; proxy
  provider resolution with and without a key; quota and rate-limit boundaries.
- **Integration (loopback only, so no firewall prompt, K04):** Host on `127.0.0.1` with a test client
  covering publish → browse → range stream → revoke → 401. Port-conflict test. Shutdown frees the port.
- **Headless UI:** mode switch; dashboard states (Stopped, Starting, Running, Error); literal-free views
  (a test that scans rendered text for untranslated keys in fr).
- **Real-window:** journey "enable Host in Settings → start → see join code and QR → publish a folder →
  stop → quit", with the port verified free.
- **Physical (NOT ASSESSED until run):** the two-machine protocol; QR scan with a phone; mDNS discovery
  on school Wi-Fi.

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "FullyQualifiedName~LanHost|FullyQualifiedName~SchoolAdmin|FullyQualifiedName~ClientSession"
dotnet test tests/OgmaLibrary.Tests.Architecture --configuration Release --no-build
dotnet test tests/OgmaLibrary.Tests.Ui --configuration Release --no-build --filter "FullyQualifiedName~HostDashboard|FullyQualifiedName~Sharing"
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Journey HostStartStop
Get-NetTCPConnection -State Listen -OwningProcess (Get-Process OgmaLibrary.App -ErrorAction SilentlyContinue).Id -ErrorAction SilentlyContinue  # must be empty after exit
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Two-machine classroom journey | Owner (two Windows machines on one network) | Classroom mode stays "beta" |
| School Wi-Fi with client isolation, mDNS | Owner or pilot school IT | Manual host-address entry must remain available |
| 20-client load on real hardware | Phase 24 | Only the local P95 exists (≤ 800 ms) |
| DPIA and school-controller approval | Owner and school, Phase 23 | No deployment to minors until signed |
| Provider terms for school use | Owner, Phase 15/23 | Student AI stays disabled by default |

## 11. Risks and mitigations

- **Exposing the library on a network by mistake.** Standalone by default, explicit confirmation,
  published-scope preview, a visible *Hosting* indicator in the shell, and Stop on quit.
- **Firewall changes are system changes.** Consent, Private profile only, reversible from Settings, and
  audited (windows-admin-engine R2 practice).
- **Tests triggering firewall prompts (K04).** All automated host tests bind to loopback.

## 12. Execution prompt

```
## Prompt 19 - Make classroom hosting reachable, complete and safe
You are implementing Phase 19 in C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/phases/phase-19-classroom-host-and-admin.md
5. docs/plans/sept-23-kaizen/03-defect-register.md (K04, K17, K51, K61)
Read the section 4 SKILL.md files; glob the windows-admin engine for its firewall skill before task 8.
Confirm Phases 02, 07, 08 and 15 are COMPLETE.
Order: 1-2 (composition and mode), 6-7 (start failures, shutdown), 3-5 (dashboard, publishing, sessions), 9 (AI proxy), 8 (firewall, with consent), 10, 11.
All automated networking tests bind to loopback. Never create firewall rules without explicit user consent.
Run section 9 commands. Record the two-machine, Wi-Fi and DPIA items as NOT ASSESSED with owners.
Write docs/implementation/execution/phase-sept23-19-completion.md and update the README status register.
```
