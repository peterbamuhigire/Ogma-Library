# Sept-23 Phase 17 completion: OCR and extraction quality

Status: **IMPLEMENTED; real-window proof pending** (2026-09-25). The code, unit, integration,
migration, headless UI and real-engine OCR checks below pass. The coordinator put a hold on
real-window runs while the owner is at the desk (the harness pins the app window topmost), so
the G2 and G6 runs and the `ScanOcrSearch` journey have not been run yet (see NOT ASSESSED).
Rollback point: `544f7ae` (the Phase 06 merge) plus the migrator's verified
`catalogue.db.<timestamp>.bak`.
Plan: [phase-17](../../plans/sept-23-kaizen/phases/phase-17-ocr-and-extraction-quality.md).
Defects: K21 (scanned books badged *Indexed*), K25 (OCR failures swallowed; OCR parts only), K04
(OCR test lock, verified as fixed).
Guide: [ocr-language-packs.md](../../developer-guide/ocr-language-packs.md).

## Tasks

| Task | Result |
|---|---|
| 1 Text status | `BookTextStatus` (Unknown, Searchable, PartlySearchable, ImageOnly, OcrInProgress, OcrText, NoText, OcrFailed) is derived by `BookTextStatusPolicy` from per-page evidence: native quality and word count, OCR rows, OCR selection and confidence, and the latest OCR job state. It is persisted in `Books.TextStatus` by `BookTextStatusService`. Once a status is known, `CatalogueProcessingProjection.IsIndexed`, `IsIndexing` and `IsIndexFailed` are false, so the text-status badge replaces the old *Indexed* badge. *Searchable* shows only when pages have usable text. |
| 2 Ingest-time detection | The extraction pipeline refreshes the status when every book finishes. A book is *Image only* when 80 % or more of its pages need OCR (`OcrPageQualityPolicy.ShouldProcess`). It is *Partly searchable* when at least one page is a scanned image without text; blank pages alone do not count, because illustrated and blank pages are normal. A document whose pages cannot be read at all is *No text could be read*. |
| 3 Suggestion and auto policy | `OcrAutoPolicyService` runs from the OCR worker's idle loop at most every 15 s. It assesses Unknown statuses left by older catalogues (50 per sweep). Then, if `AutoOcrScannedBooks` is on and the device is not on battery (`GetSystemPowerStatus`), it queues up to 10 image-only books that have never been through OCR. OCR stays one job at a time in the `document-render` group. A "Needs OCR" filter (`CatalogueFilterViewModel.NeedsOcrOnly`) and the Activity Centre banner "Make N scanned book(s) searchable" (`QueueBooksNeedingOcrAsync`) are in place. The `ocr-settings.json` hook (`IOcrPolicySettingsStore`) is ready for Phase 08. |
| 4 Failure reporting | `OcrJobProcessor` classifies every exception into `OcrFailureCodes`: timeout, resource limit, missing language data, unreadable page, file unavailable, invalid job, or unexpected. It logs `ocr.job.failed` (event 5020) through `InfrastructureLog` and records the code on the job. Missing language data, resource limits and invalid jobs are permanent; the rest follow the Phase 06 retry policy. The book becomes *OCR failed*. Book Detail shows "OCR did not finish: ‹reason›" and the Activity Centre row shows "Failed: ‹reason›" (en/fr). Neither shows a raw code. |
| 5 Language packs | Decision: **remove** `deu`, `fra`, `ita` and `spa` from `OcrLanguagePolicy`. Only `eng` ships (see *Licence facts*). `IOcrLanguageCatalog` lists only allowed packs that are present and match their checksum; Settings (Phase 08) must use it. Nothing is downloaded at run time. An unshipped or tampered pack fails with the typed `ocr_language_data_missing` code. The process for adding a pack is in the guide. |
| 6 K04 | Root cause (recorded in Phase 00, `f568bc6`): a production leak. `PdfWorkerClient.KillProcessTree` returned before the worker exited, so the sandbox was still locked during cleanup. Phase 17 re-verified the fix: `Phase24RealOcrCorpusTests` passed **20 of 20** runs, and a new 50-job OCR loop through the isolated worker and packaged Tesseract left no locked file and an empty worker sandbox (145 s, `Category=Benchmark`). [Loop log](evidence/sept-23-kaizen/phase-17/ocr-loops.md). |
| 7 Text quality | `TextQuality` = (pages with native text + Σ OCR confidence of the selected OCR pages) / pages. `OcrConfidence` = mean confidence of the selected OCR pages. Both are stored next to `QualityScore` in `Books` and are exposed in the catalogue and detail projections. Book Detail shows "Text quality: NN % of pages readable". The health dashboard and missing-field filters (META-007) are Phase 10. |
| 8 Search integration | Bug found and fixed (CODE, with a fail-first test). The `FtsReindexJob` that OCR queues re-ran extraction, which then hit a duplicate page key (native row plus OCR row) and marked the book *Index failed*. The pipeline now reuses only native rows and applies `OcrPageQualityPolicy.ShouldSelectOcr` per page, so OCR text is indexed with artifact and index-version stamps. `FtsSearchResult.IsOcrText` and `SemanticSearchResult.IsOcrText` mark hits from OCR text, and the search panel adds a "From OCR text" badge (en/fr). |

Other fixes: the OCR queue no longer creates a duplicate row for a paused job (it collided with
the idempotency key). A user re-queue resets attempts and counts a requeue (Phase 06 T06.2). The
Tesseract provider throws typed failures instead of `ArgumentException` or
`InvalidOperationException`.

## Tests

Fail-first checks were run against the old code where the plan asks for them:

- `Sept23Phase17TextStatusTests` (22 tests):
  - Policy for every classification mix: 80 % threshold (8 of 10 pages is image only, 7 of 10 is partly searchable), blank pages, OCR text with confidence, OCR that reads nothing, and the in-progress, failed and cancelled overlays.
  - Projection: a scanned book is not *Indexed* (fails before).
  - Pipeline: image-only book gets *Image only*, text book gets *Searchable*.
  - OCR then re-index keeps the text searchable and labels hits `IsOcrText`. Fails before: verified by restoring the old dictionary code, which failed the test.
  - Typed, permanent, logged failure leads to *OCR failed* (fails before: `ocr_processing_failed`, retried, no log).
  - Exception-to-code mapping; every failure code and status has en and fr text.
  - Auto policy: AC power, battery, off, pause-on-battery off, failed attempts never re-queued, 10-per-sweep bound, Unknown backfill.
  - Worker sweep interval; bulk queue and count; paused job not duplicated.
- `Sept23Phase17LanguagePackTests` (4 tests): the packaged tessdata offers only `eng` (the policy allowed four unshipped languages before); a missing or tampered file offers nothing; an unshipped language gives a typed `ocr_language_data_missing`; the policy store defaults to auto OCR, round-trips, never stores an unshipped language and recovers from corruption.
- `Sept23Phase17RealOcrSearchTests`:
  - Fast suite, Windows: a generated image-only PDF with `amber library lantern` rendered into the picture goes through the real isolated worker, extraction (*Image only*, 0 hits), packaged Tesseract OCR and re-indexing. The phrase is then found with `IsOcrText`, a page-0 jump target and *OCR text* at confidence ≥ 0.75.
  - `Category=Benchmark`: the 50-job handle-leak loop.
- `TextStatusMigrationRehearsalTests`: a Phase 06 catalogue copy with a scanned and a text book, both *Indexed*, is migrated by the production migrator. Status starts Unknown, and the first sweep assesses *Image only* and *Searchable*. The verified backup restores the previous schema. The schema freeze was deliberately advanced from 43 to 44 migrations (`20260925132534_Sept23Phase17TextStatus`).
- Updated deliberately:
  - `OcrJobProcessorTests`: a resource limit is now permanent `ocr_resource_limit`.
  - `OcrJobQueueServiceTests`: a re-queue resets attempts and uses `eng`, because `fra` no longer ships.
  - `Phase24OcrQualityTests`: the policy allows only `eng`.
- UI `Sept23Phase17OcrStatusUiTests` (5 tests):
  - The grid at 1280×800 shows *Scanned, needs OCR*, *OCR text* + *91 %* and *Searchable*, with no *Indexed* (fails before).
  - The "Needs OCR" filter selects image-only, partly searchable and failed books.
  - The Activity Centre banner text and action.
  - The Activity Centre failure reason in English and French.
  - Book Detail text status, quality and failure reason.
- E2E: the new `ScanOcrSearch` journey (`tests/OgmaLibrary.Tests.E2E/Journeys/ScanOcrSearchTests.cs`) has been written but not run (hold).

## Licence facts relied on (checked 2026-09-25 from the restored NuGet packages)

| Item | Fact | Class |
|---|---|---|
| `Tesseract.Data.English` 4.0.0 | Licence file is Apache-2.0 text; projectUrl github.com/tesseract-ocr/tessdata; `eng.traineddata` 23,466,654 bytes, SHA-256 `DAA0C97D…FC047` (matches the pinned value) | MEASURED (local package) |
| `Tesseract` 5.2.0 | Licence expression Apache-2.0; Windows native binaries only | MEASURED (nuspec) |
| Upstream tessdata licence for other languages; current Tesseract release | Not checked against the live upstream | NOT ASSESSED (currentness gate before shipping any new pack) |

## Typeface and design

The status badges reuse the existing badge pattern: Public Sans (body token) at the Caption
size token. Backgrounds use the theme tokens (Sage, Oak, Slate, Clay) and text uses
`Brush.Accent.OnAccent`. The new badges do not hard-code a foreground. No new fonts or icons were
added; the OCR search badge reuses `ic_filter_chip_page`.

## Gates (Windows 11, Release)

`dotnet restore --locked-mode` pass (the Release restore's change to `src/OgmaLibrary.App/packages.lock.json`,
K06, was reverted and not committed); `dotnet format --verify-no-changes` 0 changes (CRLF);
`dotnet format analyzers --severity warn` 0; Release build 0 warnings, 0 errors;
`Test-RequirementAccountability.ps1` pass (101 FRs, 29 NFRs, 32 controls); no vulnerable
packages; `Test-Fast.ps1`: Architecture 52/52, core 1,111/1,111, UI 188/188 (1,351 tests, 0 failures; was 1,320). OCR loops: `Phase24RealOcrCorpusTests` 20/20, 50-job leak loop pass.

## Deviations

- **Auto OCR is on by default.** The plan said "off by default". The coordinator's Phase 17
  brief asked for auto-queue by default on this local-only engine. Recorded mitigations: it only
  queues *Image only* books, runs one job at a time, pauses on battery, never retries a failed
  attempt automatically, and can be turned off in `ocr-settings.json` (Phase 08 UI). Rollback:
  set the default in `OcrPolicySettings` to `false`.
- The "Needs OCR" filter is implemented in the view model and tested, but it has no toolbar
  control yet. The filter bar lives in the shell that Phase 07 is rewriting, so the control
  belongs there or in Phase 08. The Activity Centre banner is the visible entry point.
- Text status is a stored column refreshed by events, not a live read-time join, so a
  50k-book list does not pay per-row subqueries. Unknown rows from older catalogues are
  backfilled by the idle sweep, not inside the EF migration.
- The Kaizen row "Keep the old badge behind a flag for one build" is covered without a flag: the
  old badges still appear whenever the text status is Unknown.
- Partly searchable books are suggested, not queued automatically (illustrated books).

## NOT ASSESSED

| Item | Owner | Why |
|---|---|---|
| Real-window G2 and G6 at 1280×800, `ScanOcrSearch` journey, and a manual UIA check that both corpus scans become searchable and are labelled OCR-derived | Coordinator re-dispatch | Real-window hold while the owner is at the desk. Covered meanwhile by the real-engine integration test and headless UI |
| G6 Locked-card contract | Coordinator (after Phase 07) | Known harness/product contract gap from the Phase 01 re-baseline |
| Representative real scanned corpus (20 to 50 of the owner's own PDFs) | Owner | Accuracy claims are limited to synthetic fixtures |
| macOS/Linux Tesseract packaging | Phase 27 | Windows native binaries only |
| Battery pause on a real laptop on battery | Engineering | Desktop on AC; the power check is unit-tested through `IPowerSource` |
| Accuracy per extra language | Language owner | No extra language ships |
| Third-party notice entry | Phase 26 | No notice file exists in the repo yet; the licence facts are recorded here and in the guide |
