# OCR language packs and text status

Ogma reads scanned (image-only) PDFs with the local Tesseract engine. Nothing is downloaded at
run time: a language can be used only when its trained data ships with the app and matches a
pinned SHA-256. Sept-23 Phase 17 made this rule visible in the policy and the UI.

## What ships

| Language | File | Source | SHA-256 | Size | Licence |
|---|---|---|---|---|---|
| English (`eng`) | `tessdata/eng.traineddata` | NuGet `Tesseract.Data.English` 4.0.0 (copied to the output folder) | `DAA0C97D651C19FBA3B25E81317CD697E9908C8208090C94C3905381C23FC047` | 23,466,654 bytes | Apache-2.0 (`license.txt` in the package; `projectUrl` github.com/tesseract-ocr/tessdata) |

The engine is NuGet `Tesseract` 5.2.0 (Apache-2.0, Windows native binaries only; macOS and Linux
packaging is Phase 27).

`deu`, `fra`, `ita` and `spa` were listed in `OcrLanguagePolicy` but never shipped, so every job
that asked for them failed. Phase 17 removed them. `OcrLanguagePolicy.AllowedLanguages` is now
`["eng"]`, and `IOcrLanguageCatalog.GetInstalledLanguages()` returns only the allowed languages
whose file is present and passes `TesseractTrainingDataVerifier`. Settings (Phase 08) must list
languages from the catalogue, never from a hard-coded list.

## Add a language pack

1. Choose the pack and record the licence. The upstream `tessdata`, `tessdata_fast` and
   `tessdata_best` repositories use Apache-2.0. Confirm this for the exact release you ship and
   record it in the table above with the date you checked. It is a time-sensitive fact, so apply
   the currentness gate (`digital-research-engine/docs/continuous-improvement/kaizen-currentness-gate.md`).
2. Add the file to the build. Prefer a pinned NuGet package. Otherwise, commit the file under
   `src/OgmaLibrary.App/tessdata/` with `CopyToOutputDirectory` and note the added installer size.
   Each `*.traineddata` file is roughly 1 to 15 MB (`fast`) or 10 to 25 MB (`best`), so agree the
   size budget with the Phase 26 installer owner.
3. Pin the checksum. Add `["xxx"] = "<SHA-256>"` to `ExpectedSha256ByLanguage` in
   `src/OgmaLibrary.Infrastructure/Ocr/TesseractTrainingDataVerifier.cs`, using
   `Get-FileHash -Algorithm SHA256` on the file as built.
4. Allow it. Add the key to `SupportedLanguages` in `OcrLanguagePolicy`
   (`src/OgmaLibrary.Application/Ocr/IOcrProvider.cs`). Then update
   `Sept23Phase17LanguagePackTests` and `Phase24OcrQualityTests.LanguagePolicy_OnlyAllowsKnownLocalPacks`.
5. Measure it. Until a language owner measures accuracy on a representative corpus, label the
   language "beta" in Settings.
6. Add the pack to the third-party notice when that file exists (Phase 26).

A tampered or missing file never runs. The job fails with `ocr_language_data_missing`, which the
UI shows as "the OCR language pack is not installed", and it is not retried.

## Text status

Each book has a `Books.TextStatus` value that says whether its text can really be searched
(`BookTextStatusPolicy`):

| Status | Meaning | Badge |
|---|---|---|
| Unknown | Not extracted yet | the old Indexing / Index failed badges |
| Searchable | Every page with content has a text layer | Searchable |
| PartlySearchable | Some pages are scanned images without text | Partly searchable |
| ImageOnly | 80 % or more of the pages need OCR | Scanned, needs OCR |
| OcrInProgress | An OCR job is queued or running | OCR in progress |
| OcrText | Scanned pages are searchable from OCR text | OCR text NN % |
| NoText | Nothing could be read, even after OCR | No readable text |
| OcrFailed | OCR failed; `Jobs.FailureCode` says why | OCR failed |

Blank pages, which have no text and no image, do not make a text book "partly searchable".
`Books.TextQuality` is the share of pages with usable text, with OCR pages weighted by their
confidence. `Books.OcrConfidence` is the mean confidence of the selected OCR pages. Phase 10's
health dashboard reads both values.

The extraction pipeline writes the status when a book finishes. The OCR queue and processor
update it when OCR is queued, runs, completes or fails. An idle sweep (`OcrAutoPolicyService`,
every 15 s) assesses books from older catalogues whose status is still Unknown.

## OCR policy

`ocr-settings.json` in the data folder (`IOcrPolicySettingsStore`) holds three settings:

- `AutoOcrScannedBooks`, default **on**: the idle sweep queues up to 10 image-only books at a
  time that have never been through OCR. It never queues a failed or cancelled attempt again.
- `PauseOnBattery`, default on: no automatic OCR while Windows reports battery power.
- `Language`, default `eng`, normalised to an allowed language.

OCR jobs run one at a time in the job runtime's `document-render` group, with the page limit
(10,000) and rendered-image limit (64 MB) of `OcrJobProcessor`. Partly searchable books are
suggested, not queued automatically. The Activity Centre shows "Make N scanned book(s)
searchable" for image-only, partly searchable and OCR-failed books. Phase 08 puts the toggle in
Settings → Reading.
