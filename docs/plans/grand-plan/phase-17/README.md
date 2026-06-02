# Phase 17 â€” Client / Classroom Mode & Multi-User

Enable Ogma Library to connect to a LAN Host as a classroom client; introduce
per-student profiles and roles; guarantee private, durable, offline-capable
per-student reading state and annotations; and keep the standalone product
entirely unaffected.

---

## 1. Title & one-line mission

**Phase 17 â€” Client / Classroom Mode & Multi-User**
A student launches Ogma, discovers or enters a Host address, selects their
profile, and immediately browses and reads the school library â€” with their own
private annotations, reading progress, and AI history stored locally and
optionally synced to the Host, even if the LAN link drops mid-session.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Release tier** | V2 |
| **Estimate** | 4 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | New (LAN classroom expansion) |
| **Platforms** | Windows 10/11 (WebView2) + macOS 13+ (WKWebView) |
| **Status** | In progress â€” WP1 ADR/scaffold started locally 2026-06-02 |
| **Depends on** | Phase 16 (Host endpoints, session tokens, cert TOFU), Phase 03 (design system), Phase 09 (annotation model) |
| **ADRs introduced** | ADR-0012 (proposed â€” see Â§7) |

---

## 3. Objectives

When this phase is done, all of the following are true:

1. An Ogma installation can switch to Client mode: it discovers a Host via mDNS
   or manual entry, completes the certificate TOFU flow, and receives a session
   token â€” all within a single onboarding UI flow.
2. Three roles are fully functional: **student** (browse + read + private
   annotations), **teacher** (browse + read + curate shelves), **guest** (browse
   + read, no persistent private state).
3. Every student's reading progress, annotations, bookmarks, reading memory, and
   AI query history are stored in that student's **private** local database,
   invisible to other students and to the Host by default.
4. Offline-first: a client caches the catalogue projection and opened book assets
   so that a dropped LAN link degrades to "read from cache" without interrupting
   the session. Core reading (already-cached books) never requires a live Host
   connection.
5. Optional sync: a student can choose to sync their private reading state to the
   Host (under their own identity, not visible to other students); last-write-wins
   per field with conflict surfacing; schema is forward-compatible with the
   OQ-08 cloud-sync direction.
6. The standalone product (Standalone mode, local library) is byte-for-byte
   unaffected: no regressions, no UI changes, no schema changes to the standalone
   catalogue.
7. ADR-0012 is authored, recording the identity model, role taxonomy, private-
   state storage strategy, and sync design.

---

## 4. Scope

### In scope

- New bounded context: **Classroom Client** (`OgmaLibrary.ClassroomClient`
  namespace within `Infrastructure`).
- Mode switcher: Settings > Library Mode (`Standalone` / `Connect to Host`);
  the mode is a runtime configuration, not a reinstall.
- Host discovery UI: mDNS-discovered Host list + manual address entry; QR-code
  scanner (using device camera or clipboard paste of the QR join URL).
- Certificate TOFU client side: on first connection, present the Host CA
  fingerprint to the student for verification (screen shows fingerprint, teacher
  confirms verbally or via QR); pin on accept.
- Enrollment: on successful TOFU, the client receives a student profile selection
  or creation flow; the session token is stored securely (OS credential store).
- Profiles: `student`, `teacher`, `guest`. Profile is a local construct (a row
  in the student's local SQLite DB) linked to a Host-side profile ID (from Phase
  18 admin enrollment â€” stub in this phase via a self-issued identity for Phase
  17 testing).
- Private per-student SQLite database: separate from the main catalogue DB;
  tables: `StudentProfile`, `StudentReadingProgress`, `StudentAnnotations`,
  `StudentBookmarks`, `StudentAiHistory`, `StudentSyncState`.
- Client catalogue view: grid/list views populated from Host's catalogue
  projection API; book detail; availability status.
- Client reader: opens books from Host using page-render or file-stream mode
  (per Host settings); uses existing Phase 08/09 reader surfaces.
- Offline cache: LRU cache of catalogue projections and rendered page images
  (configurable size limit, default 500 MB). Cache entry includes `eTag` for
  conditional refresh. On LAN drop: show "Offline â€” reading from cache" chip;
  core reader functions continue.
- Sync: on reconnect, per-student state diffs are pushed to Host (Host stores
  per-student opaque blob by profileId); conflicts surfaced to student with
  "Keep local / Keep server" choice.
- ADR-0012: identity, roles, private-state storage, sync strategy.
- i18n: all Client mode and profile strings in en + fr.
- Accessibility: all discovery, enrollment, and profile flows fully keyboard + SR.

### Explicitly out of scope

- Admin console, enrollment of profiles by admin (Phase 18).
- School-managed AI keys and managed AI search (Phase 18).
- AI search in classroom mode (Phase 18).
- Phase 17 does not change or extend the Host â€” Host endpoints from Phase 16 are
  consumed as-is.
- Cloud sync (OQ-08 â€” schema-ready but not wired).
- Linux.

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-CLIENT-001 | V2 | Mode switcher: Standalone â†” Connect to Host | Integration test: mode toggle persists across restart; correct DB is loaded per mode |
| FR-CLIENT-002 | V2 | Host discovery via mDNS list + manual entry + QR join URL | Integration test: mDNS-discovered host appears in list; manual entry connects; QR join URL parsed |
| FR-CLIENT-003 | V2 | Certificate TOFU client flow: pin Host CA on accept | Unit test: pinned cert accepted; different cert rejected with warning |
| FR-CLIENT-004 | V2 | Profile selection/creation: student / teacher / guest | Integration test: profile persists across session; role is included in session token |
| FR-CLIENT-005 | V2 | Per-student private SQLite database; other students cannot read it | Architecture test: student DB path includes profileId; no cross-profile DB access |
| FR-CLIENT-006 | V2 | Catalogue grid/list populated from Host projection API | Integration test: grid shows books matching Host catalogue state; sort/filter work |
| FR-CLIENT-007 | V2 | Reader opens book from Host (page-render or file-stream) | Integration test: page renders appear; NFR-OGMA-005 page-turn budget on cached pages |
| FR-CLIENT-008 | V2 | Offline cache: catalogue + rendered pages; reader works on LAN drop | Fault-injection test: LAN disconnected mid-session â†’ reader continues from cache |
| FR-CLIENT-009 | V2 | Per-student annotations/progress/bookmarks stored privately | Integration test: annotation written by student A not visible to student B on same Host |
| FR-CLIENT-010 | V2 | Optional sync: private state pushed to Host under student identity | Integration test: sync â†’ Host stores blob by profileId; restore on new device |
| FR-CLIENT-011 | V2 | Conflict surfacing: last-write-wins + student chooses on collision | Integration test: conflicting annotation timestamps â†’ conflict dialog â†’ student choice persists |
| FR-CLIENT-012 | V2 | Guest profile: browse + read, no persistent private state | Integration test: guest session â†’ no DB row written; next guest session starts clean |
| FR-CLIENT-013 | V2 | Standalone mode unchanged: no regression on any Phase 00â€“15 flow | Golden-corpus regression suite passes with mode = Standalone |
| NFR-CLIENT-001 | V2 | Catalogue load from Host â‰¤ 2 s P95 (2,000 books, warm LAN) | Load test: 2,000-book projection, P95 â‰¤ 2 s |
| NFR-CLIENT-002 | V2 | Offline cache hit: page render â‰¤ 100 ms P95 (NFR-OGMA-005) | Performance test: 10 cache-hit page requests, P95 â‰¤ 100 ms |
| NFR-CLIENT-003 | V2 | Session token stored in OS credential store, not plain text | Architecture test: token storage uses ICredentialStore, not a plain file |
| CTRL-OGMA-001 | V2 | Session token in OS credential store (DPAPI/Keychain) | Unit test: token persists via ICredentialStore; not visible in plain-text config |
| CTRL-OGMA-016 | V2 | Student private data is not transmitted off-device without consent | Architecture test: ClassroomClient has no direct network call with student annotation data unless sync is explicitly triggered |
| ADR-0012 | V2 | Identity, roles, private state, sync strategy documented | ADR-0012 authored and owner-ratified |

---

## 6. Dependencies

### Depends on

- **Phase 16**: Host HTTPS endpoints (catalogue, assets, page-render, auth
  session); certificate provisioning and TOFU anchor.
- **Phase 03**: design system, design tokens, icon system â€” profile switcher and
  discovery UI must match the Phase 03 language.
- **Phase 09**: annotation model; client annotations mirror the same schema so
  Phase 09 reader surfaces work in Client mode without modification.
- **Phase 12**: `ICredentialStore` abstraction (session token storage).
- **Phase 04**: catalogue DB schema (Client mode student DB schema is a private
  mirror subset, not the full catalogue).

### Unblocks

- **Phase 18**: Admin console and managed AI require the profile/role model from
  this phase; Admin mode is a teacher/admin profile elevated further.
- **Phase 19**: Security hardening of the client-side certificate store,
  credential storage, and offline cache.
- **Phase 20**: LAN client performance benchmarks at 40 concurrent students.

---

## 7. Architecture & approach

### ADR-0012 (proposed)

**Title:** Classroom identity, roles, and per-student private state model.

**Context:** Introducing multi-user classroom operation requires an identity and
role model that: (a) preserves student privacy from peers and optionally from
the school; (b) keeps the standalone product unchanged; (c) is forward-
compatible with the cloud-sync direction (OQ-08) without committing to it;
(d) enables the school admin (Phase 18) to enroll and manage profiles.

**Decision:**

1. **Identity:** a `ProfileId` (UUID v4, generated locally on first enrollment)
   is the stable identity for a student. The Host knows this UUID; the Host never
   stores the student's reading content â€” only the profile metadata (display name,
   role) and, if the student opts into sync, an opaque compressed blob of the
   student's private state.

2. **Roles:** `student` (default) â€” browse/read/annotate privately; `teacher` â€”
   browse/read/annotate + initiate shelf curation (Phase 18 for curation approval);
   `guest` â€” browse/read, no persistent state, no sync. Role is embedded in the
   session token issued by the Host and enforced server-side.

3. **Private-state storage:** each enrolled student on a client machine has a
   dedicated SQLite database file at
   `<sidecar>/classroom/profiles/<profileId>/private.db`. The main catalogue DB
   is untouched. Schema tables: `StudentReadingProgress`, `StudentAnnotations`,
   `StudentBookmarks`, `StudentAiHistory`, `StudentSyncState`. The file is
   readable only by the OS user running Ogma (file permissions + optional
   at-rest encryption per Phase 19 CTRL-OGMA-015).

4. **Offline cache:** LRU on-disk cache at `<sidecar>/classroom/cache/`; keyed
   by `(hostId, bookId, pageNumber, eTag)`. Cache size limit in settings (default
   500 MB). Cache is not private-state â€” it contains Host-served rendered images,
   safe to evict. Cache is not synced.

5. **Sync:** opt-in per student. On trigger (manual or reconnect), the client
   serializes the private DB to a compressed, encrypted blob (AES-256-GCM, key
   derived from the student's session); uploads to `PUT /api/v1/profile/sync`.
   On restore, download + decrypt + merge. Last-write-wins per row by
   `UpdatedAt`; conflicts (same row, different content, same `UpdatedAt`) are
   surfaced to the student. Schema is append-only for forward compatibility with
   OQ-08.

6. **Standalone unaffected:** ClassroomClient context is inactive when mode =
   Standalone. The main catalogue DB is never opened in shared/write mode by the
   ClassroomClient context.

**Consequences:**

- Student reading state is private by default; sync requires explicit opt-in
  per the local-first principle.
- The Phase 18 admin can see profile metadata (name, role) but not content of
  the private blob without the student's session key.
- OQ-08 (cloud sync) is schema-compatible: the private DB format and sync blob
  format are stable surfaces.

**Status:** Proposed 2026-05-30. Ratify in Phase 17 start.

---

### Bounded context: Classroom Client

Location: `OgmaLibrary.Infrastructure.ClassroomClient`.

Interfaces consumed:
- `ILibraryHostClient` (new) â€” typed HTTP client wrapping Phase 16 Host API.
- `ICredentialStore` â€” session token storage (CTRL-OGMA-001).
- `IOfflineCacheService` â€” LRU page/catalogue cache.
- `IStudentPrivateRepository` â€” CRUD on the per-student SQLite DB.
- `IMdnsResolver` â€” discover Host mDNS records.

Interfaces owned:
- `IClassroomModeService` â€” switch modes; current mode observable.
- `IProfileService` â€” create/select/delete profiles; role resolution.
- `ISyncService` â€” trigger sync; conflict resolution callback.

### Cross-platform notes

| Concern | Windows | macOS |
| --- | --- | --- |
| mDNS client (discovery) | Same cross-platform library as Phase 16 (`Manatee.Dns`/`ZeroconfSharp`) | Same |
| Camera QR scan | WinRT `MediaCapture` or clipboard-paste fallback | AVCaptureSession or clipboard-paste fallback |
| OS credential store (session token) | DPAPI via `ICredentialStore` (Phase 12 abstraction) | Keychain via same abstraction |
| File permissions on private DB | NTFS ACL: `SDDL` deny-other-users | POSIX: `chmod 600` |

### Data / schema changes

New per-student SQLite file (not a migration of the main catalogue):

```sql
-- In <sidecar>/classroom/profiles/<profileId>/private.db

CREATE TABLE StudentReadingProgress (
    BookId      TEXT NOT NULL,
    HostId      TEXT NOT NULL,
    LastPage    INTEGER NOT NULL DEFAULT 1,
    LastOffsetY REAL NOT NULL DEFAULT 0,
    UpdatedAt   TEXT NOT NULL,
    PRIMARY KEY (BookId, HostId)
);

CREATE TABLE StudentAnnotations (
    Id          TEXT PRIMARY KEY,
    BookId      TEXT NOT NULL,
    HostId      TEXT NOT NULL,
    PageNumber  INTEGER NOT NULL,
    Type        TEXT NOT NULL,   -- 'Highlight' | 'Note'
    Color       TEXT,
    Body        TEXT,
    CreatedAt   TEXT NOT NULL,
    UpdatedAt   TEXT NOT NULL,
    IsDeleted   INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE StudentBookmarks (
    Id          TEXT PRIMARY KEY,
    BookId      TEXT NOT NULL,
    HostId      TEXT NOT NULL,
    PageNumber  INTEGER NOT NULL,
    Label       TEXT,
    CreatedAt   TEXT NOT NULL,
    UpdatedAt   TEXT NOT NULL,
    IsDeleted   INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE StudentAiHistory (
    Id          TEXT PRIMARY KEY,
    HostId      TEXT NOT NULL,
    Query       TEXT NOT NULL,
    ResponseSummary TEXT,
    Tier        TEXT NOT NULL,
    CreatedAt   TEXT NOT NULL,
    IsDeleted   INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE StudentSyncState (
    HostId      TEXT NOT NULL PRIMARY KEY,
    LastSyncedAt TEXT,
    LastSyncBlobHash TEXT,
    ConflictCount INTEGER NOT NULL DEFAULT 0
);
```

Main catalogue DB: no changes.

---

## 8. Work breakdown (summary)

Full task detail in `tasks.md`.

| Work package | Key tasks | Est. |
| --- | --- | --- |
| **WP1 â€” ADR-0012 & architecture** | Author ADR-0012; ClassroomClient bounded context scaffold; interfaces; DI wiring; architecture tests | 2 d |
| **WP2 â€” Host discovery & TOFU client** | mDNS resolver; manual entry; QR join URL parser; certificate TOFU client-side pinning | 3 d |
| **WP3 â€” Profile management** | Profile create/select/switch; role assignment; guest mode; OS credential store for session token | 2 d |
| **WP4 â€” Catalogue client view** | Grid/list populated from Host API; sort/filter; book detail; availability status | 3 d |
| **WP5 â€” Reader integration** | Client reader opens book from Host (page-render + file-stream modes); resume position | 2 d |
| **WP6 â€” Per-student private DB** | Schema creation; StudentPrivateRepository; annotation/progress/bookmark CRUD | 2 d |
| **WP7 â€” Offline cache** | LRU cache for catalogue projections + rendered pages; `eTag` conditional refresh; "Offline" chip | 3 d |
| **WP8 â€” Sync** | Serialize/compress/encrypt private DB blob; `PUT /api/v1/profile/sync`; conflict surfacing UI | 3 d |
| **WP9 â€” Client mode UI** | Mode switcher; discovery screen; enrollment flow; profile switcher; sync settings | 2 d |
| **WP10 â€” Testing & CI** | Unit, integration, fault-injection, architecture, performance tests; CI | 3 d |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons + manifest**: `icons.md` defines `ic_connect_to_library`,
  `ic_profile_student`, `ic_profile_teacher`, `ic_profile_guest`, `ic_sync`,
  `ic_offline`, `ic_mode_standalone`, `ic_mode_classroom` â€” all `â¬œ to procure`.
- [x] **i18n (en/fr)**: all discovery, enrollment, profile, sync, and offline
  strings externalized; `fr` translations in same PR; pseudolocale CI check.
- [x] **Accessibility**: discovery list is keyboard-navigable (arrow keys +
  Enter); TOFU fingerprint panel has ARIA description; profile selector has ARIA
  `listbox` role; "Offline" chip has `aria-live="polite"`.
- [x] **Privacy/egress**: student private state is not transmitted without explicit
  sync opt-in (CTRL-OGMA-016); session token in OS credential store
  (CTRL-OGMA-001); cache entries are rendered images (not raw PDFs) unless Host
  is in file-stream mode; AI history stored only locally unless sync enabled.
- [x] **Reversibility**: profile deletion deletes private DB and clears session
  token; operation requires confirmation; no data loss of other profiles.
- [x] **Performance budgets**: NFR-CLIENT-001 (catalogue load â‰¤ 2 s),
  NFR-CLIENT-002 (cached page â‰¤ 100 ms) instrumented; offline cache eviction
  policy tested.
- [x] **Bounded-context tests**: ClassroomClient has no dependency on LanHost
  server internals; architecture tests enforce separation.
- [x] **Documentation**: ADR-0012 authored; XML doc comments on all new public
  interfaces; `SOURCE-SUMMARY.md` updated.

---

## 10. Definition of Done

### Global DoD

- [ ] Every in-scope FR/NFR/CTRL ID has a passing test or a tagged gap.
- [ ] Golden-corpus suite green (Standalone mode); Client mode integration tests
      green; no open R1/R2 defect.
- [ ] `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`,
      architecture tests â€” all pass.
- [ ] Builds and tests pass on **both Windows and macOS** CI runners.
- [ ] New user strings externalized and present in **en + fr**; pseudolocale CI
      check passes.
- [ ] Every new control has a colorful icon **and** an accessible label;
      keyboard + SR walkthrough passes; `icons.md` complete.
- [ ] ADRs/decisions recorded; reference docs updated.
- [ ] Performance budgets instrumented and within budget (or trend).
- [ ] `/code-review` and `/security-review` done; findings resolved.

### Phase-17-specific exit criteria

- [ ] ADR-0012 authored and owner-ratified; identity/role/sync decisions recorded.
- [ ] Mode switch (Standalone â†” Client) persists across app restart; correct DB
      loaded per mode; no Standalone catalogue DB modified by Client mode.
- [ ] mDNS discovery lists a running Phase 16 Host within 5 s on same subnet.
- [ ] Certificate TOFU client flow: pinned cert accepted; mismatched cert shows
      warning and blocks connection.
- [ ] Three roles (`student`, `teacher`, `guest`) all authenticate and receive
      correct permissions as verified by integration test against Phase 16 Host.
- [ ] Per-student private DB: annotation written by student A not readable by
      student B profile (separate file, OS permissions).
- [ ] Offline fault-injection test: LAN drop mid-session â†’ reader continues from
      cache for the open book â†’ "Offline" chip visible â†’ reconnect â†’ sync prompt.
- [ ] Conflict surfacing: integration test produces a conflict; student chooses
      "Keep local"; local value persists; Host value discarded.
- [ ] Guest mode: no DB row written during session; app state clean on guest
      logout.
- [ ] Standalone golden-corpus regression: all Phase 00â€“15 tests pass unchanged.

---

## 11. Skills to use

Full guidance in `skills.md`. Key skills:

- `mobile-cross:pwa-offline-first` â€” offline cache design and sync patterns (WP7,
  WP8).
- `security:dual-auth-rbac` / `mobile-rbac` â€” role-based access control for
  student/teacher/guest (WP3).
- `architecture:event-driven-architecture` â€” sync event design, conflict
  resolution (WP8).
- `documentation-generation:architecture-decision-records` â€” ADR-0012 (WP1).
- `frontend-ux:enterprise-ux-process` â€” multi-user enrollment and profile
  switching flows (WP9).
- `superpowers:test-driven-development` â€” private DB isolation and fault-injection
  tests (WP6, WP7, WP10).
- `/security-review` â€” WP2 (TOFU), WP3 (credential store), WP8 (sync blob
  encryption).

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| ADR-0012 | `docs/adrs/0012-classroom-identity-roles-private-state.md` |
| `OgmaLibrary.Infrastructure.ClassroomClient` namespace | `src/OgmaLibrary.Infrastructure/ClassroomClient/` |
| `IClassroomModeService`, `IProfileService`, `ISyncService`, `IOfflineCacheService`, `IStudentPrivateRepository` | `src/OgmaLibrary.Application/ClassroomClient/` |
| Per-student SQLite schema (in-code, no EF Core migration â€” separate DB file) | `src/OgmaLibrary.Infrastructure/ClassroomClient/Data/StudentDbContext.cs` |
| Architecture isolation tests | `src/OgmaLibrary.Tests/Architecture/ClassroomClientIsolationTests.cs` |
| Integration tests | `src/OgmaLibrary.Tests/Integration/ClassroomClient/` |
| Fault-injection offline tests | `src/OgmaLibrary.Tests/Integration/ClassroomClient/OfflineFaultTests.cs` |
| Client mode UI views (discovery, enrollment, profile, sync) | `src/OgmaLibrary.App/Views/Classroom/` |
| `icons.md` | `docs/plans/grand-plan/phase-17/icons.md` |

---

## 13. Risks

| Risk | R-tier | Mitigation |
| --- | --- | --- |
| mDNS discovery fails on managed school networks (mDNS blocked) | R5 | Manual IP entry always available; document network requirements for IT admins |
| Offline cache grows unbounded and fills student drive | R4 | LRU eviction with configurable size limit; warning when cache > 80% of limit |
| Sync blob encryption key loss (student forgets password / device loss) | R1 | Sync is opt-in; local copy always authoritative; no "server is the only copy" scenario |
| Conflict resolution UX confusing for young students | R5 | Teacher can advise; guest mode skips this entirely; UI tested with Phase 21 UX review |
| Private DB file accidentally backed up / synced via OS cloud drive | R2 | Document to admins; Phase 19 adds at-rest encryption (CTRL-OGMA-015) |
| Role escalation: student obtains teacher session token | R2 | Role in token is server-issued and verified; teacher enrollment requires admin approval (Phase 18) |

---

## 14. Owner asks

1. **ADR-0012 sign-off**: Peter must ratify the identity and private-state model
   before Phase 17 build begins â€” this sets the student-data boundary that the
   school's DPIA relies on (CTRL-OGMA-024).
2. **Icon procurement**: Please procure the 8 icons listed in `icons.md`:
   `ic_connect_to_library`, `ic_profile_student`, `ic_profile_teacher`,
   `ic_profile_guest`, `ic_sync`, `ic_offline`, `ic_mode_standalone`,
   `ic_mode_classroom`.
   Sizes: 16/24/32/48 px @1x, @2x, @3x. Light + dark variants.
3. **Sync opt-in scope**: confirm whether sync is opt-in per student (proposed)
   or opt-in per school admin (Phase 18). This affects the Phase 17 settings UI.
4. **Guest mode retention**: confirm whether a "guest" session's reading progress
   should be offered to carry over if the guest creates a profile in the same
   session, or strictly discarded.
5. **Camera QR scan**: confirm whether camera-based QR scanning is required for
   Phase 17 or clipboard-paste of the QR URL is sufficient.

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Planning agent | Initial v1.0 draft |
| 2026-06-02 | Implementation | Started WP1: added ADR-0012 draft, ClassroomClient Application contracts, inactive Infrastructure scaffold, DI registration, default-Standalone guardrails, and focused scaffold tests. |
| 2026-06-02 | Implementation | Advanced WP2: added the Client-mode join payload parser for Phase 16 `ogma-lan://` QR/manual links and legacy `ogma://host?addr=...&fp=...` plan links. |
| 2026-06-02 | Implementation | Advanced WP2: added the Client-mode mDNS resolver for `_ogma-library._tcp` records, with observable Host discovery, bounded scans, join-payload validation, and fake-backend tests. |
| 2026-06-02 | Implementation | Advanced WP2: added the Client-mode TOFU trust-pin seam with first-use evaluation, explicit accept/pin, mismatch rejection, and focused tests. |
| 2026-06-02 | Implementation | Started WP3: added file-backed profile persistence, active profile selection, transient guest sessions, credential-store session token keys, private-state cleanup on delete, and focused tests. |
| 2026-06-02 | Implementation | Advanced FR-CLIENT-001: added file-backed Standalone/Connect-to-Host mode persistence with restart tests. |
