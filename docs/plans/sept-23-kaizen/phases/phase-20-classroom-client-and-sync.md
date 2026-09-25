# Phase 20: Classroom client, offline cache and sync

## 1. Header

| Field | Value |
|---|---|
| Wave | E, Advanced surfaces |
| Size | 7–10 engineering days |
| Depends on | Phase 19 (Host reachable, publishing, sessions, split `ClassroomJoinViewModel`), Phase 08 (Settings → Classroom → *Join a classroom*), Phase 13 (unified search contract reused for host-index search) |
| Owner decisions | **D-09** (clients store full PDFs, or stream with a bounded, encrypted, expiring cache; recommended: stream), **D-11** (teacher dashboard scope; recommended: minimal) |
| Primary defects | K61, K62 |
| Requirements | CLIENT-001, 002, 005, 006, 008, 009, 010 (W\*, only with the env flag), CLIENT-011 (W), CLIENT-003 (P), CLIENT-004 (P), CLIENT-007 (P), CLIENT-012 (M), CLIENT-013 (B) |

## 2. Why this phase exists

A student today cannot join a classroom, because the join controls live inside the env-gated
`HostSharingViewModel` (K61). The client stack underneath is substantial (trust-on-first-use pinning,
an encrypted per-profile store, an offline cache, sync), but several parts contradict the requirements
or will fail in a classroom:

- **Connection is not remembered.** `ClassroomClientServiceExtensions` registers
  `InMemoryClassroomHostConnectionService`
  (`src/OgmaLibrary.Infrastructure/ClassroomClient/ClassroomClientServiceExtensions.cs:21`), so every
  restart forgets the classroom.
- **Full plaintext copies.** `ClassroomBookFileMaterializer` writes the whole PDF from
  `resource.Content` (a full in-memory byte array) to disk
  (`src/OgmaLibrary.Infrastructure/ClassroomClient/ClassroomBookFileMaterializer.cs:66-70`). CLIENT-004
  requires streaming without a full copy, and a shared school computer then holds unencrypted copies
  outside the size-bounded cache.
- **Cache without expiry.** `DiskOfflineCacheService` enforces a 500 MB size limit
  (`DiskOfflineCacheService.cs:12,168`), but there is no time-based expiry or revocation-triggered purge
  tied to the Host session.
- **Search is AI-only.** Host-index search exists only through Student Smart Search
  (`src/OgmaLibrary.App/ViewModels/Catalogue/StudentSmartSearchViewModel.cs:266,294`); the ordinary
  search panel searches the local index only (CLIENT-007).
- **No roles, no teacher view.** There is no client role picker (CLIENT-003), no teacher dashboard
  (CLIENT-012) and no UI for Host entitlements (CLIENT-013).

## 3. Objectives and exit criteria

1. **Join flow.** Settings → Classroom → *Join a classroom* offers discovered Hosts (mDNS), manual
   address entry, or a join code or QR text. The first connection shows the Host name and certificate
   fingerprint for **trust on first use** (`HostTrustService`). A changed fingerprint blocks the
   connection with a clear warning and a *Contact your teacher* action. It never silently re-trusts.
2. **Persisted connection.** Replace `InMemoryClassroomHostConnectionService` with a file-backed,
   encrypted implementation. After a restart the client reconnects automatically. The offline chip
   shows *Connected*, *Reconnecting*, *Offline — using saved books* or *Access revoked*.
3. **Streaming reader (D-09 recommended).** Books open through HTTP range streaming into the reader
   without writing a full plaintext file. Offline copies are stored only in the cache, encrypted with the
   profile key (`AesGcmAtRestEncryptionService`), bounded by size (default 500 MB, configurable) and
   **expiry** (default 14 days since last Host validation), and purged on revocation or profile removal.
   If the owner chooses full copies instead, they are encrypted and teacher-consented, and the choice is
   documented in the DPIA.
4. **Host-index search.** The unified search panel (Phase 13) searches the Host's published catalogue
   when connected (metadata plus full text if the Host exposes it) and the local cache when offline,
   with a visible scope label.
5. **Offline and sync.** Private notes, highlights and reading progress sync when enabled
   (`ClassroomSyncService`). Conflicts show *Keep mine* or *Keep class copy* per item. Reconnection
   triggers a bounded sync. Nothing a student writes changes the Host catalogue (CLIENT-010).
6. **Teacher dashboard (D-11 minimal).** On the Host: who is connected, what they have open (title only,
   no page-level tracking unless policy allows), and what is published. This is read-only and localised.
7. **Isolation.** Two student profiles on one client machine cannot see each other's notes, cache or
   history (CLIENT-008). A revoked profile loses access immediately and its cache is purged (CLIENT-009).
8. **Two-machine and two-student journeys** pass. These are **NOT ASSESSED until physically run.**

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\architecture\distributed-systems-patterns\SKILL.md` (offline-first, idempotent sync, conflict resolution, retries)
- `C:\wamp64\www\chwezi-dev-engine\skills\security\vibe-security-skill\SKILL.md` (per-profile resource matrix, TOFU threat cases)
- `C:\wamp64\www\chwezi-dev-engine\skills\security\dpia-generator\SKILL.md` (cache contents of minors on shared machines)
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md` (connected, offline, revoked states)
- `C:\wamp64\www\design-system-skills\skills\12-data-viz-and-dashboards\dashboard-and-data-product-design\SKILL.md` (teacher dashboard)
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\design-ethics-and-anti-dark-patterns\SKILL.md` (monitoring of students must be proportionate and disclosed)

**Typeface decision:** existing stack; JetBrains Mono for fingerprints and addresses. No new fonts.

## 5. Scope

**In scope:** the join UI, TOFU presentation, persisted connection, streaming reader access, cache
encryption, expiry and purge, host-index search scope, sync UX, the minimal teacher dashboard, the role
picker (student or teacher on a client), and isolation tests.

**Out of scope:** Host-side publishing and sessions (Phase 19), AI for students beyond wiring the Phase 19
proxy into the unified search, and multi-host roaming.

## 6. Work breakdown

1. **Join UI.** A `ClassroomJoinView` with discovered Hosts, manual address, code/QR text entry, a TOFU
   confirmation dialog (Host name, fingerprint in JetBrains Mono, "Only continue if your teacher's screen
   shows the same code"), and a changed-fingerprint block.
2. **Persisted connection.** Add `FileClassroomHostConnectionService` (encrypted with the profile key)
   and register it instead of the in-memory service. Reconnect on start with backoff; expose the state to
   the shell chip.
3. **Streaming reader.** Add a range-backed stream to the PDF worker input path so the reader opens Host
   books without materialising a plaintext file. If PDFium requires a file, use an encrypted cache file
   decrypted into a job-scoped temp file that is deleted on close. Retire the full-copy path in
   `ClassroomBookFileMaterializer` or restrict it to the encrypted cache (per D-09).
4. **Cache policy.** Add expiry to `DiskOfflineCacheService` metadata, a purge on revoke, profile removal
   and expiry, and a Settings control (size, *Clear cache now*, usage display).
5. **Host-index search.** Implement a search source for the connected Host behind the Phase 13 unified
   search contract, with a scope chip (*Class library*, *Saved offline*) and an offline fallback.
6. **Sync UX.** Opt-in toggle, *Sync now*, last-sync time, a conflict list with per-item resolution, and
   bounded retries; all strings localised.
7. **Roles and entitlements.** A role picker at join (student or teacher, validated by the Host's
   enrolment token) and an entitlement display (CLIENT-003, CLIENT-013).
8. **Teacher dashboard (Host side).** A read-only view listing connected profiles and the currently open
   title, subject to the class policy, with an explicit notice to students that the teacher can see which
   book they have open.
9. **Isolation tests.** Two profiles on one machine: notes, cache, history and credentials are separated;
   switching profiles never shows the other's data; revoke purges.
10. **Protocols.** Extend `docs/qa/classroom-two-machine-protocol.md` (from Phase 19) with client steps,
    offline and reconnect, revocation, the two-student run and evidence capture.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Students cannot join | Join UI inside env-gated host VM | Dedicated join flow from Settings | Students connect in < 60 s | Join from UI: impossible → ≤ 60 s median | Harness timing | Wrong Host trusted | TOFU comparison step |
| Classroom forgotten on restart | In-memory connection | Encrypted persisted connection | Reconnect is automatic | Manual rejoin after restart: always → never | Test | Stale Host | Validation on reconnect |
| Plaintext full copies | Materializer writes whole PDFs | Streaming plus an encrypted, expiring cache | No plaintext copies on disk | Plaintext PDFs in client data dir: N → 0 | File-system scan test | PDFium needs files | Encrypted temp file, deleted on close |
| Local-only search | Ordinary search ignores the Host | Host search source in the unified contract | Students find class books | Class-library hits for a known title: 0 → ≥ 1 | Test | Host load | Debounce plus cache |
| No teacher view | Not built | Minimal dashboard | Teachers see who is connected | CLIENT-012 M → W | Screenshot | Surveillance concerns | Policy toggle; disclosure |

## 8. Test plan

- **Unit:** TOFU evaluation (first use, same, changed); connection persistence round-trip; cache expiry
  and purge; conflict resolution; entitlement parsing.
- **Integration (loopback):** a Host plus two client profiles in one process: join → stream → offline
  (Host stopped) → cached read → reconnect → sync → revoke → purge verified on disk. A scan asserts that
  no `*.pdf` plaintext file exists under the client data directory.
- **Headless UI:** join states; changed-fingerprint block; offline chip transitions; conflict dialog.
- **Real-window:** journey "join a loopback Host from Settings → open a class book → go offline → keep
  reading → reconnect".
- **Physical (NOT ASSESSED until run):** two machines on school-like Wi-Fi; two students on one client
  machine; network interruption mid-read; teacher dashboard reflects reality.

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "FullyQualifiedName~ClassroomClient|FullyQualifiedName~HostTrust|FullyQualifiedName~OfflineCache|FullyQualifiedName~ClassroomSync"
dotnet test tests/OgmaLibrary.Tests.Ui --configuration Release --no-build --filter "FullyQualifiedName~ClassroomJoin"
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Journey ClassroomJoinOffline
Get-ChildItem $env:OGMA_LIBRARY_DATA_DIR -Recurse -Filter *.pdf  # must be empty after the client journey
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Two-machine and two-student physical runs | Owner | Classroom client remains "beta" |
| Behaviour on Wi-Fi with client isolation | Pilot school IT | Manual address entry is required there |
| D-09 decision and DPIA wording for cached content | Owner, Phase 23 | Default to streaming; no full copies |
| Performance of range streaming for large scanned PDFs over Wi-Fi | Phase 24 | Show progress; allow "save offline" |

## 11. Risks and mitigations

- **PDFium needs random file access.** A job-scoped encrypted temp file is an acceptable fallback if it is
  deleted on close and covered by the plaintext scan test.
- **Students see a scary certificate prompt.** Plain-language TOFU copy, verified in the Phase 28
  usability test.
- **Monitoring overreach.** The teacher dashboard shows titles only, by policy, with student disclosure.

## 12. Execution prompt

```
## Prompt 20 - Let students join, read, work offline and sync safely
You are implementing Phase 20 in C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/phases/phase-20-classroom-client-and-sync.md
5. docs/plans/sept-23-kaizen/03-defect-register.md (K61, K62)
Read the section 4 SKILL.md files. Confirm Phases 08, 13 and 19 are COMPLETE and D-09/D-11 are recorded; if not, stop and ask.
Order: 2 (persisted connection), 1 (join and TOFU), 3-4 (streaming and cache), 5, 6, 7, 8, 9, 10.
All automated tests use loopback. Never write plaintext PDFs under the client data directory.
Run section 9 commands. Record physical journeys as NOT ASSESSED with owners.
Write docs/implementation/execution/phase-sept23-20-completion.md and update the README status register.
```
