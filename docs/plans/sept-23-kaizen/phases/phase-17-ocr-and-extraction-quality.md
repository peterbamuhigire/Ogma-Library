# Phase 17: OCR and extraction quality

## 1. Header

| Field | Value |
|---|---|
| Wave | D, Search and AI |
| Size | 4–6 engineering days |
| Depends on | Phase 06 (job states, retry policy, file-validity outcomes from Phase 05) |
| Owner decisions | None required. OCR language packs beyond English are a licensing and size choice recorded in task 5. |
| Primary defects | K21, K25, K04 |
| Requirements | READ-010 (P), META-001 (W), META-007 (P), SEARCH-002 (W), LIB-007 (P) |

## 2. Why this phase exists

The audit library contained two image-only PDFs (`Scanned Pamphlet (image only).pdf` and the
golden-corpus `scanned-image-only.pdf`). Both were badged **Indexed** in the catalogue even though
they contain no text layer, and a search for the phrase printed in the scanned image
(`amber library lantern`) returned nothing (K40 run, K21). The user is told the book is searchable
when it is not.

The pieces to fix this already exist but are not connected to the user's view:

- `OcrPageQualityPolicy.ShouldProcess` classifies pages as `Scanned`, `Empty` or `Partial`
  (`src/OgmaLibrary.Application/Ocr/IOcrProvider.cs:57-75`; the enum is at
  `src/OgmaLibrary.Application/Reader/IPdfRenderer.cs:241`).
- `CatalogueProcessingProjection.IsIndexed` (`src/OgmaLibrary.Application/Catalogue/Projections.cs:72`)
  is true whenever the index job finished, even if it produced zero chunks.
- OCR starts only from the Book Detail action (`BookDetailViewModel`), and `OcrWorker` swallows every
  failure (`src/OgmaLibrary.Workers/Ocr/OcrWorker.cs:42`).
- `OcrLanguagePolicy` allows `deu`, `eng`, `fra`, `ita` and `spa` (`IOcrProvider.cs:28-31`), but only
  `eng.traineddata` ships, with a pinned SHA-256 in `TesseractTrainingDataVerifier.cs:17-37`. The other
  four always fail.
- The one failing test in the suite, `Phase24RealOcrCorpusTests.PackagedTesseract_RecognizesExpectedWordsFromGeneratedScannedFixture`,
  failed with `IOException ... being used by another process` during temp-directory cleanup (K04).
  That is either a test cleanup race or an OCR handle leak, and it must be diagnosed, not retried.

## 3. Objectives and exit criteria

1. Every book has an honest **text status**: *Searchable text*, *Partly searchable*, *Image only —
   needs OCR*, *OCR in progress*, *OCR text (confidence NN %)*, or *No text could be read*. The status is
   derived from page classification and chunk counts, never from job completion alone.
2. Image-only books are detected at ingest. By default Ogma **suggests** OCR: a catalogue filter "Needs
   OCR" and a banner action "Make N scanned books searchable". The Settings → Reading option
   *Automatically OCR scanned books* (off by default, bounded by the resource guards) queues them.
3. After OCR, the audit phrase `amber library lantern` is found by search in the scanned fixture, with a
   page jump. The badge shows *OCR text* and its confidence.
4. OCR language packs: English ships. Each additional allowed language is either shipped with a pinned
   SHA-256 and licence record, or removed from `OcrLanguagePolicy` so the UI never offers a language that
   cannot work.
5. OCR failures surface in the Activity Centre and the Book Detail panel with a localised reason (timeout,
   resource limit, missing language data, unreadable page). Nothing is silently swallowed.
6. `Phase24RealOcrCorpusTests` passes 20 consecutive runs locally and in CI, with the root cause of the
   lock documented.
7. The extraction quality score (`MetadataQualityService`, `BookRow.QualityScore`) is split into a
   **text-quality** component (share of pages with usable text, OCR confidence) that feeds the text
   status and the health dashboard (Phase 10).

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\systematic-bug-diagnosis\SKILL.md` (for the K04 lock)
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md` (flaky-test policy: a flake is a defect with an owner)
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\error-empty-and-system-messaging\SKILL.md`
- `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md`
  (Tesseract version, tessdata licence and checksums are time-sensitive claims)

**Typeface decision:** status badges use Public Sans at the Caption token (raised to 12 px in Phase 09).
No new fonts.

## 5. Scope

**In scope:** text-status derivation, ingest-time detection, the OCR suggestion and auto policy, OCR
failure reporting, language-pack policy and packaging, the K04 test fix, and the text-quality score.

**Out of scope:** macOS and Linux native Tesseract packaging (Phase 27; stays `NOT ASSESSED` until then),
handwriting recognition, and layout reconstruction.

## 6. Work breakdown

1. **Text-status projection.** Add `TextStatus` to `CatalogueProcessingProjection`, computed from the
   page classifications stored by extraction (`ExtractedPages`), the chunk count and OCR provenance.
   Replace the *Indexed* badge semantics with *Searchable* only when chunks > 0.
   *Acceptance:* the audit corpus yields *Image only* for both scanned files, *No text could be read* for
   the 0-byte, HTML and truncated files (after the Phase 05 validity change), and *Searchable* for the rest.
2. **Ingest-time detection.** When extraction finishes, record the pages whose `ShouldProcess` is true.
   If they are ≥ 80 % of the pages, mark the book *Image only*; if some are, mark it *Partly searchable*.
3. **Suggestion and auto policy.** Add a "Needs OCR" catalogue filter and an Activity Centre banner
   action. Add the Settings → Reading toggle *Automatically OCR scanned books* (default off). Auto mode
   enqueues through `OcrJobQueueService` under existing resource guards and pauses on battery power
   (Windows) when the OS reports it.
4. **Failure reporting.** Replace the swallowing `catch (Exception)` in `OcrWorker.cs:42` with a typed
   failure recorded on the job (`FailureCode`), logged via the Phase 02 logger and surfaced in Book Detail.
   Map codes to localised messages.
5. **Language packs.** For each of `deu`, `fra`, `ita` and `spa`, decide *ship* (add trained data with
   SHA-256 in `TesseractTrainingDataVerifier`, a licence entry in the third-party notice, and a size
   budget) or *remove* from `OcrLanguagePolicy`. Show only installable languages in Settings. Record the
   decision in the completion record.
6. **K04 root cause.** Reproduce with a loop (`dotnet test --filter Phase24RealOcrCorpusTests` × 20).
   Check for undisposed `TesseractEngine`, `Pix` or `FileStream` handles and for the worker process still
   exiting. Fix the leak if one exists. Otherwise make cleanup wait for process exit with bounded retry,
   and document which it was.
7. **Text-quality score.** Compute `TextQuality = usablePages / totalPages`, weighted by OCR confidence
   for OCR pages. Store it alongside `QualityScore` and expose it to Phase 10's health dashboard and
   missing-field filters (META-007).
8. **Search integration check.** After OCR completes, confirm the FTS index is refreshed for that book
   (`SearchExtraction` follow-up) and that result snippets are labelled "from OCR text".

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Scanned books shown as Indexed | Badge reflects job completion, not text | `TextStatus` from pages and chunks | Users trust the badges | Mislabelled books 2/2 → 0 | Catalogue screenshot | Status churn during processing | Keep the old badge behind a flag for one build |
| Scanned text unsearchable | OCR is manual and hidden in Detail | Detection, filter, banner, optional auto | Scanned books become searchable | `amber library lantern` hits 0 → ≥ 1 | Search screenshot | CPU load | Auto off by default |
| Silent OCR failures | `catch (Exception)` swallows | Typed failure codes | Failures become diagnosable | Unreported failures → 0 | Activity Centre screenshot | Noisy UI | Group failures per book |
| Languages offered but unusable | Policy lists packs that are not shipped | Ship or remove | No dead options | Unusable languages 4 → 0 | Settings screenshot | Installer size | Remove rather than ship |
| Flaky OCR test | Unknown lock owner | Diagnose leak vs race | Suite is deterministic | 1 failure per run → 0 in 20 runs | Loop log | Masking a real leak | Keep a diagnostic assertion |

## 8. Test plan

- **Unit:** `TextStatus` derivation for each classification mix; the image-only threshold; failure-code
  mapping; the language policy returning only installed packs.
- **Integration:** ingest of the synthetic corpus (Phase 01 generator, including image-only, mixed and
  rotated scans) asserts statuses; OCR then search finds the embedded phrase; the OCR worker handle-leak
  test (open and close 50 jobs, then assert no locked files remain).
- **Headless UI:** the "Needs OCR" filter; banner action; Book Detail failure message.
- **Real-window:** journey "scan folder → see *Image only* → Make searchable → search the phrase → open
  the page", at 1280×800.
- **Flake gate:** `Phase24RealOcrCorpusTests` × 20 with zero failures.

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
1..20 | ForEach-Object { dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "FullyQualifiedName~Phase24RealOcrCorpusTests"; if ($LASTEXITCODE) { throw "flake on run $_" } }
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "FullyQualifiedName~Ocr|FullyQualifiedName~TextStatus"
dotnet test tests/OgmaLibrary.Tests.Ui --configuration Release --no-build --filter "FullyQualifiedName~Ocr"
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Journey ScanOcrSearch
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Representative real mixed scanned corpus (not synthetic) | Owner, to supply 20–50 of their own scanned PDFs locally (never committed) | Accuracy claims limited to synthetic fixtures |
| macOS Tesseract native packaging | Phase 27 | OCR on macOS unavailable until then; the UI must say so |
| OCR accuracy per extra language | Language owner per shipped pack | Language labelled "beta" until measured |

## 11. Risks and mitigations

- **OCR is CPU-heavy and slow on school machines.** Auto mode is off by default, with bounded concurrency,
  pause and resume, and progress per book.
- **Threshold misclassifies illustrated books as scanned.** Use the 80 % page threshold plus word counts,
  and let the user dismiss the suggestion per book.
- **Language packs inflate the installer.** Consider an optional download with a verified checksum as a
  Phase 26 follow-up.

## 12. Execution prompt

```
## Prompt 17 - Make scanned books honest and searchable
You are implementing Phase 17 in C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/phases/phase-17-ocr-and-extraction-quality.md
5. docs/plans/sept-23-kaizen/03-defect-register.md (K04, K21, K25, K40)
Load the section 4 skills by reading their SKILL.md files. Confirm Phase 06 is COMPLETE.
Start with task 6 (diagnose the K04 lock using systematic-bug-diagnosis) because a flaky suite hides regressions. Then tasks 1-2, 3-4, 5, 7-8.
Use the Phase 01 synthetic corpus generator for fixtures; never commit real book content.
Run the section 9 commands. Record macOS and real-corpus items as NOT ASSESSED.
Write docs/implementation/execution/phase-sept23-17-completion.md and update the README status register.
```
