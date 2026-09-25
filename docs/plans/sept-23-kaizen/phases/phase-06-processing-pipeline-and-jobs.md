# Phase 06: Processing pipeline, jobs and identity promotion

## 1. Header

| Field | Value |
|---|---|
| Wave | B: Core library |
| Size | 6–9 engineering days |
| Depends on | Phase 05 (roots, validity, per-root paths) |
| Owner decisions | none (D-05 affects only how the "waiting for provider" state is worded) |
| Primary defects | K23 (duplicates, no editions), K24 (ISBN not promoted), K25 (retry storm, misleading states), K26 (worker fragility, throughput), K13 (shared progress counters), K28 (leases, shutdown retries) |
| Requirement IDs | LIB-005 (background jobs), META-001 (ISBN detection), META-007 (quality), CAT-007 (work/edition), LIB-007 (scan health), NFR throughput |

## 2. Why this phase exists

After one scan of 17 files, the Activity Centre reports **26 failed jobs and 300 attempts**
(`evidence/screens/sheet2.png`). The measured job table shows:

- 16 `EmbeddingJob` rows failed three times each with `embedding_provider_unavailable`, because
  no embedding provider exists. That is an environment condition, not a job failure.
- 4 cover and 4 spine jobs failed after three attempts.
- One `ExtractionFailed` row with `RetryCount` 48. This row is not a job at all.
  `ExtractionPipelineService` (`src/OgmaLibrary.Infrastructure/Search/ExtractionPipelineService.cs:695-730`)
  stores page-extraction failures as a `Jobs` row and increments `RetryCount` for every failed
  page, which inflates the attempt totals.
- `RetryCount` is also incremented by crash recovery (`OgmaLibrary.Workers/JobRecoveryService.cs:64`),
  the thumbnail-repair startup task (`OgmaLibrary.App/Startup/StartupTasks.cs:89`) and the
  worker's re-queue path (`BookIngestionWorker.cs:274`). So closing the app or restarting it
  consumes attempts, and jobs reach terminal failure without ever really failing (K25, K28).

`BookIngestionWorker.ExecuteAsync` (`src/OgmaLibrary.Workers/BookIngestionWorker.cs:66-93`) has
no loop-level guard. One exception from `ClaimNextAsync` or `FailAsync` ends cover, spine and
metadata processing until restart (K26). The same loop also drives the shared scan progress
(`SetPhase(Complete)` at line 81, `SetPhase(GeneratingAssets)` at line 89), which is why the
status bar shows "59 / 17 files" (K13).

Every PDF operation copies the whole file into a sandbox and hashes it (`PdfWorkerClient.cs:510`
`File.Copy`, `:539`/`:555` `SHA256.HashData`) and spawns a process. That happens four to five
times per book (K26).

Identity work stops short of the user. Six valid ISBNs were extracted into
`ExtractedIsbnEvidence` with `IsBest=1`, but `Books.IsbnNormalized` stayed null for every book
(K24). A byte-identical duplicate is shown twice, and `EditionId` is null for all 17 books,
even though `IdentityGroupingService` exists (`Infrastructure/Catalogue/Repositories/IdentityGroupingService.cs`) (K23).

## 3. Objectives and exit criteria

1. **Honest job accounting:** after a clean scan of the corpus without an embedding provider,
   the Activity Centre shows 0 *Failed* jobs caused by missing capabilities. Those jobs are
   *Waiting for a capability* (for example "Waiting for semantic search to be set up"). Attempts
   ≤ 1.2 × jobs.
2. **Retry policy:** exponential backoff (5 s, 30 s, 2 min, 10 min, jitter ± 20 %), a maximum of
   3 *real* attempts, and a distinction between retryable, permanent (for example `NotAPdf`) and
   capability-missing failures. Shutdown, crash recovery and repair never consume an attempt.
3. **Worker resilience:** every background worker loop survives any non-fatal exception, backs
   off, logs, and resumes. A fault-injection test proves it for `BookIngestionWorker`,
   `SearchExtractionWorker`, `EmbeddingGenerationWorker` and `OcrWorker`.
4. **Progress:** scan progress (files) and processing progress (tasks) are separate snapshots
   with separate UI text. The completed count never exceeds its total.
5. **Identity:** every book with a best ISBN shows it in detail and search. Byte-identical files
   group as one book with two occurrences. Same-ISBN different-file items group as editions of
   one work. The corpus expectations in `expected.json` (Phase 01) are met.
6. **Throughput:** at most one sandbox copy and one hash per book per processing batch. Time to
   process the 17-book corpus (scan to all covers) drops by ≥ 50 % from the Phase 01 baseline.
7. **Leases:** after a crash and restart, jobs held by a dead process are reclaimed immediately
   (the owner PID is not alive), not after the 5-minute lease.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\reliability-engineering\SKILL.md`: retry, backoff, poison messages, idempotency.
- `C:\wamp64\www\chwezi-dev-engine\skills\backend-databases\database-reliability\SKILL.md`: SQLite contention, migrations, leases.
- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\observability-monitoring\SKILL.md`: job metrics and events.
- `C:\wamp64\www\chwezi-dev-engine\skills\architecture\system-architecture-design\SKILL.md`: identity ownership (the catalogue stays authoritative).
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`: fault injection and property-based tests.

## 5. Scope in / scope out

**In:** job state model, retry policy, capability-wait state, failure-record separation,
worker loop guards, progress split, lease recovery, shutdown semantics, ISBN promotion,
duplicate and edition grouping on real scans, sandbox and hash reuse, Activity Centre wording.

**Out:** the UI for merging and splitting editions (Phase 10); embedding provider setup
(Phase 14); OCR quality (Phase 17); large-scale performance budgets (Phase 24).

## 6. Work breakdown

**T06.1: Job state model.** Extend `JobRuntimeStatus` with `WaitingForCapability` and
`Cancelled`. Keep `Failed` and `DeadLetter`. Add a `FailureKind` (`Retryable`, `Permanent`,
`CapabilityMissing`, `Cancelled`) to the failure contract used by `JobRuntimeService.FailAsync`
(`Infrastructure/Ingestion/JobRuntimeService.cs:174-215`). `CapabilityMissing` parks the job
without consuming an attempt. A `CapabilityAvailable(kind)` event (for example when an
embedding provider becomes healthy) requeues parked jobs.
*Acceptance:* unit tests for each transition; a migration maps existing rows.

**T06.2: Attempts are real attempts.** Remove `RetryCount += 1` from crash recovery
(`JobRecoveryService.cs:64`), thumbnail repair (`StartupTasks.cs:89`) and re-registration.
Record those events as `RequeueCount` or audit events instead. Increment `RetryCount` only in
`ClaimNextAsync` when a worker actually starts executing. On shutdown, the stopping token
releases the lease back to `Pending` (`ReleaseAsync`), with no failure recorded.
*Acceptance:* 5 app restarts during processing leave `RetryCount` unchanged for interrupted jobs.

**T06.3: Backoff.** Replace `DefaultRetryDelay = 5 s` (line 16) with an exponential schedule
plus jitter, stored in `NextAttemptUtc`.
*Acceptance:* a unit test on the schedule; E2E shows no retry storm in logs (≤ 3 attempts per job).

**T06.4: Separate failure records.** Move the page-extraction failure record written by
`ExtractionPipelineService` (lines 695-730, `JobType = "ExtractionFailed"`) into a dedicated
`ExtractionIssues` table (book, page, code, count, last message) and migrate existing rows.
Activity Centre totals then count only real jobs. Per-book extraction issues appear in the
detail inspector (Phase 11) and in Needs attention (Phase 05).
*Acceptance:* the corpus scan produces 0 `ExtractionFailed` job rows.

**T06.5: Worker loop guard.** In `BookIngestionWorker.ExecuteAsync` (lines 66-93), wrap each
iteration in `try/catch`: `OperationCanceledException` when stopping means exit; any other
exception means log, back off (1 s → 30 s, capped) and continue. Apply the same template to
`SearchExtractionWorker`, `EmbeddingGenerationWorker` and `OcrWorker` through a shared
`ResilientJobLoop` helper. Treat `TaskCanceledException` from `HttpClient` timeouts as
`Retryable`, not as shutdown.
*Acceptance:* fault injection (a DB busy exception on claim; an exception in `FailAsync`) keeps
the worker alive; covers still complete.

**T06.6: Progress split (K13).** `ScanProgressService` publishes a `ScanSnapshot` (files
discovered, registered, invalid, missing). A new `ProcessingProgressService` publishes a
`ProcessingSnapshot` (tasks queued, running, done, waiting, failed, grouped by kind). Remove
`_progress.SetPhase` calls from `BookIngestionWorker` (lines 81, 89 and around 157-188). Refresh
the catalogue progressively (throttled to 1 per 2 s) as covers complete, not only on
`Complete`. Update the Phase 03 status rules to use both snapshots.
*Acceptance:* G2 samples never show N > M; covers appear while processing continues.

**T06.7: Lease recovery.** Record `LeaseOwnerPid` and a process start time. At startup,
`JobRecoveryService` reclaims `Running` jobs whose owner process is not alive, regardless of
lease expiry (lines 54-67). `HasResourceCapacityAsync` ignores leases of dead owners.
*Acceptance:* kill the app mid-processing and relaunch; processing resumes within 5 s.

**T06.8: ISBN promotion (K24).** After metadata extraction, promote the best
`ExtractedIsbnEvidence` (`Infrastructure/Metadata/IsbnEvidenceStore.cs`) into canonical
metadata (`Books.IsbnNormalized` and the ISBN field with provenance `extracted:<artifact>`),
unless the user has overridden it. Validate the checksum again (ISBN-10 and -13) and never
promote invalid ones.
*Acceptance:* the 6 valid corpus ISBNs appear in detail and match `expected.json`; the invalid
`978-9970-02-123-4` is not promoted.

**T06.9: Duplicate and edition grouping (K23).** At registration, when the content hash matches
an existing book, attach a new occurrence to that book instead of creating one. After ISBN
promotion, run `IdentityGroupingService` to assign `EditionId` or work groups for same-ISBN
items, and record the decision in the identity decision log. The catalogue projection shows
one card per book, with an occurrence count badge (UI detail in Phase 10).
*Acceptance:* the corpus shows 16 books; "The Lantern Keeper" has 2 locations.

**T06.10: Sandbox and hash reuse (K26).** Introduce a per-book processing batch: one sandbox
copy and one SHA-256 (reusing the scan's hash when size and mtime are unchanged). Run
metadata, cover, spine and search extraction against the same sandboxed copy in one worker
process where the operations are compatible, keeping the Job Object limits per batch. Delete
the sandbox at batch end, and clean `%TEMP%\OgmaLibraryPdfWorker` orphans at startup. Scale
per-operation timeouts by page count and file size (for example 15 s + 50 ms × pages, capped at
120 s) for one-shot jobs.
*Acceptance:* process launches per book ≤ 2 (was 4–5); the corpus processing time halves; the
900-page book gets its cover.

**T06.11: Activity Centre wording.** Show "Waiting for semantic search setup (16)" with a
*Set up* action (Phase 14 target), "Needs attention (4)" and "Failed (n) · Retry all". Never
show raw attempt totals as a headline.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| 300 attempts / 26 failed (K25) | Missing capability counted as failure; attempts burned by restarts; failure records as jobs | Capability wait, real-attempt counting, issues table | Accounting becomes honest | 26 failed/300 → 0 capability failures, ≤ 1.2× attempts | Activity Centre screenshot, DB | Parked jobs never resume | Capability event plus a periodic 10-min probe |
| Worker dies silently (K26) | No loop guard | ResilientJobLoop | Processing always resumes | unguarded → survives injected faults | fault tests | Hot error loop | Capped backoff and an error-rate breaker |
| "59 / 17 files" (K13) | Shared counters | Split snapshots | Truthful progress | N > M → never | E2E sampling | UI churn | Throttled updates |
| ISBN invisible (K24) | No promotion step | Promote best evidence | Users see ISBNs | 0/6 → 6/6 | detail screenshot | Wrong ISBN promoted | User override wins; provenance shown |
| Duplicate shown twice (K23) | Hash match not used at registration | Occurrence attach + grouping | One book per content | 17 → 16 books | DB | Merging distinct books | Only exact hash auto-merges; others are proposals |
| Slow ingestion (K26) | Copy + hash + spawn per op | Per-book batch | ≥ 50 % faster | baseline → ≤ 50 % time | timings | Batch failure blocks all outputs | Per-operation fallback on batch error |

## 8. Test plan

- **Unit:** state transitions, backoff schedule, attempt accounting, ISBN validation and
  promotion precedence, hash-duplicate attach, timeout scaling.
- **Fault injection:** DB busy on claim; exception in `FailAsync`; worker process killed
  mid-batch; `HttpClient` timeout with metadata providers enabled.
- **Integration:** the corpus through the full pipeline (DB assertions against `expected.json`).
- **E2E:** G2 (progressive covers, honest status); restart during processing; Activity Centre wording.
- **Negative:** a book removed mid-processing (job completes as `Cancelled`); a disk full during
  sandbox copy (Retryable with a clear message).

## 9. Acceptance commands

```powershell
./scripts/Test-Fast.ps1
dotnet test tests/OgmaLibrary.Tests -c Release --no-build --filter "FullyQualifiedName~JobRuntime|FullyQualifiedName~Worker|FullyQualifiedName~Isbn|FullyQualifiedName~IdentityGrouping"
dotnet test tests/OgmaLibrary.Tests.E2E -c Release --no-build --filter "Journey=G2|Category=Processing"
sqlite3 "$data\catalogue.db" "select Status, count(*), sum(RetryCount) from Jobs group by Status;"
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Throughput at 2k/50k books on the reference machine | Phase 24 | Local corpus timing only |
| Online metadata provider timeouts under real network | Phase 08/15 (provider toggle) | Fault-injected only |

## 11. Risks and mitigations

- *Changing the job schema on live data.* Migrate with a backup and a rehearsal on the audit DB copy.
- *Batching weakens isolation.* Keep one document per process and the same Job Object limits;
  never mix books in one worker.
- *Auto-grouping merges wrong items.* Only exact content hashes auto-merge; ISBN grouping creates
  editions under one work but keeps the books separate, and users can split them (Phase 10).

## 12. Execution prompt

```
## Prompt 06 - Make background processing honest, resilient and fast
You are reworking the job pipeline of Ogma Library in C:\wamp64\www\Ogma-Library.
Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/03-defect-register.md (K13, K23, K24, K25, K26, K28)
5. docs/plans/sept-23-kaizen/phases/phase-06-processing-pipeline-and-jobs.md
Load skills (read SKILL.md): reliability-engineering, database-reliability, observability-monitoring,
system-architecture-design, advanced-testing-strategy.
Work plan: A. T06.1-T06.3 (state model and accounting, serial). B. T06.4-T06.7 in parallel.
C. T06.8-T06.9 (identity). D. T06.10 (throughput) last, measured against the Phase 01 baseline. E. T06.11.
File scope: src/OgmaLibrary.Infrastructure/{Ingestion,Search,Metadata,Catalogue,Pdf}/**, src/OgmaLibrary.Workers/**,
src/OgmaLibrary.App/Startup/StartupTasks.cs, Activity Centre view and view model, migrations, tests.
The SQLite catalogue stays authoritative for identity. Never auto-merge books that are not byte-identical.
Acceptance: section 9 commands; before/after job table and timings recorded in
docs/implementation/execution/phase-sept23-06-completion.md.
Recovery point: the last Phase 05 commit plus a DB backup.
```
