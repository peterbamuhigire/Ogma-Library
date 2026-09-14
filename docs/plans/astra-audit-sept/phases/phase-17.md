# Phase 17: classroom Client, remote reading and offline privacy

Status: planned. Owner: client/data engineer; reviewer: security QA and two student-profile testers.
Dependencies:08/09/16. Estimate:8-14 person-days. Findings:F19/F20/F22.
Requirements:CLIENT001-013, especially FR-CLIENT-004; old phase35.
Skills:E1/E4/E5/D9; [routes](../06-skills-sources-and-kaizen.md).

## Outcome and blocking decision

Students join, browse and read a permitted school library with private notes and predictable offline behavior. Resolve the present full-file materialization conflict before implementation: FR-CLIENT-004 says reading must not require a complete local copy, while ClassroomBookFileMaterializer writes the full resource to disk.

## Work slices

1. Produce an ADR comparing true bounded/range-backed PDF access with an explicitly permitted encrypted/local-cache model. Evaluate PDFium access needs, memory/disk/network budgets, retention, revocation and school policy. The default plan is to satisfy no-full-copy reading; changing the promise requires an explicit requirement/DPIA decision.
2. Implement the chosen stream/materialization contract through ClassroomBookFileLocator/Materializer and LibraryHostHttpClient without bypassing isolated rendering. Avoid fetching whole content into memory before checking limits.
3. Make join/discovery/manual address/fingerprint/enrollment flow usable through real UI. Profile/role selection and current host are visible, with safe logout/switch behavior.
4. Scope notes, progress, caches, files, thumbnails, embeddings and diagnostics to the approved host/profile model. Define retention and cleanup on logout, revoke, unpublish, account deletion and expired offline permission.
5. Test offline reading eligibility separately from cached catalogue browsing. Clearly state what remains available, which changes are pending and when reconnect will sync. Do not promise access revocation can erase an unreachable machine instantly.
6. Verify conflict-safe sync, private-key isolation, retry/idempotency and host identity changes. Never merge two students' stores on the same computer.

## Acceptance

- Actual disk/network inspection proves the approved reading/caching contract, including first-page behavior and bounded memory on large PDFs.
- Two profiles on one machine cannot read each other's notes, cache or exported state; host/profile switching does not reuse unauthorized material.
- Join and changed-certificate rejection work across Windows/Mac, with accessible code entry/paste and manual discovery fallback.
- Drop network mid-read and mid-sync, restart, reconnect: permitted state survives once, conflicts are visible and no unauthorized content appears.
- Erasure/removal covers materialized files and derived assets, with precise residual/offline limitations documented.
- Student core tasks remain understandable under offline, revoked and expired-session states.

## Recovery

Before cache migration, preserve private-state backup and encryption/key custody. Do not delete old profiles until migration and erasure semantics are verified. Fall back to local standalone library on host failure. Block institutional release if the storage contract is unresolved or cross-profile access is observed.
