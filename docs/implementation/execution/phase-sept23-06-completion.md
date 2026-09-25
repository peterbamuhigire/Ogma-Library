# Sept-23 Phase 06 completion: processing pipeline, jobs and identity promotion

Status: **IMPLEMENTED; journey proof pending the Phase 01 harness** (2026-09-25). Code, unit,
fault-injection, migration, UI and real-window checks below pass. G2 and `Category=Processing`
E2E journeys run once this branch is merged next to the Phase 01 harness (see NOT ASSESSED).
Rollback point: `f89f483` (the Phase 05 merge) plus the migrator's verified
`catalogue.db.<timestamp>.bak`.
Plan: [phase-06](../../plans/sept-23-kaizen/phases/phase-06-processing-pipeline-and-jobs.md).
Defects: K13, K23, K24, K25, K26, K28 (lease and shutdown parts). K42 (the provider itself) stays
with Phase 14.

## Commits

| Commit | Subject |
|---|---|
| `f21db7e` | perf(pdf): run a book's worker operations in one sandboxed session |
| `3bbc44a` | feat(jobs): count only real attempts and park jobs missing a capability |
| `e0223d1` | feat(identity): promote extracted ISBNs and attach identical copies |
| `c6a471d` | fix(workers): guard every job loop and report task progress apart |
| `d03cb1d` | feat(ui): show task progress and honest activity centre totals |
| `4c338f4` | fix(jobs): make processing progress disposal idempotent |
| `9548a00` | fix(identity): give password-protected books an edition too |

## Tasks

| Task | Result |
|---|---|
| T06.1 State model | `JobRuntimeStatus.WaitingForCapability` (7); `JobFailureKind` Retryable / Permanent / CapabilityMissing / Cancelled on `JobFailure`. Capability-missing parks the job, refunds the claim and records `WaitingCapability`; `ResumeWaitingAsync(capability)` requeues parked jobs. The embedding worker parks jobs when the provider is unavailable and resumes them (at most once a minute) when it is healthy, which is the hook Phase 14's setup will trigger. Metrics report waiting jobs separately. |
| T06.2 Real attempts | `RetryCount` grows only in the claim. `ReleaseAsync` (shutdown) refunds the claim; crash recovery refunds it too (poison guard: after 5 free requeues the claim stands); thumbnail repair and re-registration reset attempts instead of adding one. All of these count in the new `RequeueCount`. |
| T06.3 Backoff | `JobRetryPolicy`: 5 s, 30 s, 2 min, then 10 min, ±20 % jitter, 3 real attempts. |
| T06.4 Failure records | New `ExtractionIssues` table (book, page, content hash, code, occurrences, last message). The pipeline writes issues, never `ExtractionFailed` jobs; the migration moves existing rows (RetryCount 48 → Occurrences 49) and deletes them from `Jobs`. |
| T06.5 Loop guard | `ResilientJobLoop` (log `job.retry.scheduled`, back off 1 s → 30 s, reset on success; only the host stopping token ends it; `HttpClient` timeouts are ordinary failures). Used by BookIngestion, SearchExtraction, EmbeddingGeneration and OCR workers. A failing `FailAsync` or `ReleaseAsync` is logged and the loop continues. |
| T06.6 Progress split | Workers no longer touch `IScanProgressService`. `ProcessingProgressService` publishes a `ProcessingSnapshot` (queued, running, done, failed, waiting by capability) from the job table, coalesced and polled every 2 s while active. The status bar shows "Preparing books: {done} of {total} tasks" (en/fr), clamped, and returns to the finished library state when only capability-waiting jobs remain. The catalogue refreshes progressively (at most every 2 s) whenever covers or metadata complete. |
| T06.7 Leases | Claims record `LeaseOwnerPid` and the process start time. A lease whose owner is not alive neither blocks its resource group nor survives startup recovery; `RecoverExpiredAsync` reclaims it too. |
| T06.8 ISBN promotion | `IsbnPromotionService` runs after ISBN evidence is stored: re-validates the ISBN-10/13 checksum, writes `Books.IsbnNormalized` and an `ISBN` metadata field with source `extracted:<artifact>`, audits `IsbnPromoted`, and never overrides a user override or another authoritative source. The detail inspector already binds `Isbn`. |
| T06.9 Duplicates and editions | A byte-identical file becomes a second `BookFiles` occurrence of the same book (audit `BookOccurrenceAttached`); anything short of an exact hash stays a separate book. A book stays available while any occurrence is present; rescans do not re-identify multi-occurrence books. Every book gets an edition (and work); books sharing an ISBN (10 or 13 form) share one edition, grouped, never merged (audit `IdentityEditionAssigned`). The occurrence badge on the card is Phase 10. |
| T06.10 Throughput | `PdfDocumentBatch`: a book's metadata, page text, ISBN, outline, cover and spine run in one worker session with one sandbox copy. The session protocol gains `asset-cover`, `asset-embedded-cover` and `asset-spine` (the first-page render is shared by cover and spine). Sandbox copies hash while copying (was three full hashes). One-shot limits scale with file size (+1 s per 4 MB, cap 120 s). Orphaned sandboxes older than 1 h are removed at startup. Isolation is unchanged: one document per process, the same Job Object memory and kill-on-close limits. The reader session path is untouched (reader tests pass). |
| T06.11 Activity Centre | Headline: "Waiting for semantic search setup (n) · Needs attention (n) · Failed (n)"; "Queued · Running"; *Retry all*; no attempt totals. The *Set up* action is Phase 14 (string added). |

## Tests (fail before, pass after where stated)

- `Sept23Phase06JobAccountingTests` (13): capability parking and resume; 5 shutdown releases and 5
  crash recoveries leave `RetryCount` 0 (both fail before: +1 or +2 per restart); poison job still
  reaches 3; a dead owner's lease does not block the metadata-index group (fails before: blocked for
  the 5-minute lease); backoff schedule and jitter bounds; permanent and cancelled kinds;
  re-registration resets attempts.
- `Sept23Phase06WorkerResilienceTests` (6): DB-busy on claim ×2 plus an exception in `FailAsync`
  keep `BookIngestionWorker` alive and the next cover completes (fails before: the claim exception
  ended `ExecuteAsync`); shutdown releases instead of failing; Search, Embedding (parks with
  `CapabilityMissing`) and OCR workers survive injected faults; loop backoff sequence.
- `Sept23Phase06IsbnPromotionTests` (7): promotion with provenance, invalid `978-9970-02-123-4`
  never promoted, user override wins, ISBN-10/13 grouping into one edition, idempotence.
- `Sept23Phase06DocumentBatchTests` (7): a batched book uses **1** worker process (unbatched ≥ 4)
  and leaves no sandbox; single-pass copy hash equals the source hash; limit scaling.
- `JobAccountingMigrationRehearsalTests`: a Phase 05 catalogue copy with the audit's job shape is
  migrated by the production migrator; issues moved, provider jobs parked, completed jobs untouched;
  the verified backup restores the previous state. Schema freeze deliberately advanced 42 → 43.
- Updated: `IngestionPipeline_SameHashAtUnregisteredPresentPath_AttachesOccurrenceToSameBook` (the
  old test asserted a second book), the extraction-pipeline failure test (issues, not jobs),
  recovery (`RequeueCount`), Activity Centre wording, a Phase 05 test whose repaired file was a
  byte copy of another book.
- UI `Sept23Phase06ProcessingStatusTests` (3): tasks not files, never N > M, finished when only
  waiting jobs remain, French wording.

## Real window (Windows 11, Release, isolated data dir, synthetic corpus + scanned-image-only.pdf)

Driver: a copy of `Invoke-OgmaUia.ps1` scoped to this worktree's executables, folder dialog moved
topmost before the click, all runs inside `Use-DesktopLock.ps1`. Before = this branch's base
(`f89f483`), same corpus, same machine. Class: MEASURED.

| Check | Before | After |
|---|---|---|
| Jobs / attempts / failed (DB) | 84 / 269 / 17 (audit: 300 / 26) | 77 / 65 / **0**; 12 waiting for semantic search (0 attempts); 3 cancelled (locked PDF) |
| `ExtractionFailed` rows | 1, RetryCount 154 | 0 rows; 0 extraction issues |
| Status busy after choosing the folder | never finished: "Preparing covers and search index…" at 150 s | "Preparing books: N of M tasks" until 66 s, finished summary from 67.7 s; N > M in 0 samples ([timeline](evidence/sept-23-kaizen/phase-06/after-status-timeline.csv)) |
| Covers appear | only after the queue drained | progressively: 3 at 8 s, 12 by 28 s ([8 s](evidence/sept-23-kaizen/phase-06/after-progressive-covers-8s-1280.png)) |
| Time to all covers (first claim → last cover) | 83.6 s | **27.3 s (−67 %)** |
| Time to all processing (covers, spines, metadata, enrich, search) | 163 s | **64 s (−61 %)** |
| Worker processes launched during the run | 129 (14 books, retries) | 34 (13 books; ≈ 2.6 per book including OCR of the two image-only PDFs) |
| ISBNs in `Books.IsbnNormalized` | 0 of 6 evidence rows | 5 of 5: 9780306406157, 9781861978769, 9780131103627, 9783161484100, 9780198526636 (the duplicate's evidence is now the same book) |
| Duplicate Lantern Keeper | 2 books (14 total) | 1 book with 2 occurrences (13 books) |
| `EditionId` null | 14 of 14 | 0 of 13 |
| Kill the app mid-processing, relaunch | retry burned, lease blocks 5 min | the interrupted job was requeued (`RequeueCount` 1, attempt refunded); 36 jobs complete 20 s after relaunch; final 77 jobs / 66 attempts / 0 failed |

Screens: [before](evidence/sept-23-kaizen/phase-06/before-status-busy-150s-1280.png),
[after, finished](evidence/sept-23-kaizen/phase-06/after-finished-1280.png).
Typeface: unchanged theme tokens; no new visual components beyond one Activity Centre button.

## Gates

`dotnet restore --locked-mode` pass; `dotnet format --verify-no-changes` 0 changes (CRLF verified
with `git ls-files --eol`); `dotnet format analyzers --severity warn` 0; Release build 0 warnings,
0 errors; `Test-RequirementAccountability.ps1` pass (101 FRs, 29 NFRs, 32 controls);
`Test-Fast.ps1`: Architecture 48/48, core 1,083/1,083, UI 183/183 (1,314 tests, 0 failures;
was 1,277). `PdfWorkerSessionStabilityTests.IsolatedPdfRenderer_WorkerKilledMidRead_NextPageRendersWithinTwoSeconds`
failed once under full-suite load (5 s) and passed in isolation and in the final full run (timing-sensitive; watch in CI).

## Deviations

- Lease identity is stored in two new `Jobs` columns rather than encoded in `LeaseOwner`.
- The per-book batch covers the BookIngestion worker's jobs and each search-extraction run; the
  search job still runs in its own session because it belongs to another worker, so a text book
  uses two worker processes, and image-only books add OCR processes (T06.10's ≤ 2 target is met
  per book for text PDFs, not in the corpus aggregate).
- The processing total can shrink while embedding jobs move from queued to waiting; it never
  falls below the done count.
- `IngestionFailure` scan records remain `Jobs` rows (terminal, 0 attempts); they are excluded
  from processing progress. Moving them to `FileIssues` is left for Phase 10/11.
- Duplicate grouping is exact-hash only; same-ISBN, different-file books are editions of one
  work, not merged. The occurrence count badge and merge/split UI are Phase 10.

## NOT ASSESSED

| Item | Owner | Why |
|---|---|---|
| G2 and `Category=Processing` E2E journeys | Phase 01 harness | This lane predates the harness merge; covered by the prototype driver above |
| ISBN shown in the detail inspector in the real window | Phase 01 / Phase 21 | Catalogue cards expose no accessible name, so the prototype driver cannot select a card; DB values and the `Isbn` binding are verified |
| Processing resumes within 5 s of relaunch (exact) | Phase 01 | Sampled at 20 s only |
| Throughput at 2k/50k books | Phase 24 | Local corpus only |
| Real embedding provider becoming available (resume) | Phase 14 | No provider on this machine; resume is unit-tested |
| Online metadata provider timeouts | Phase 08/15 | Providers disabled by default; `HttpClient` timeout is classified as retryable in code |
