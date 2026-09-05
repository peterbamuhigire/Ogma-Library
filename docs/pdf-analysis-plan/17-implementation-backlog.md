# Prioritised implementation backlog

This is the actionable queue behind the 12 phase briefs. Priority is based on
user harm, security, architectural leverage and evidence dependency.

## P0 — resolve before expanding PDF features

| ID | Change | Primary paths | Phase |
|---|---|---|---:|
| PDF-B-001 | Add capability profile/result contracts | `src/OgmaLibrary.Application/Reader/` | 1 |
| PDF-B-002 | Ban direct production PDF opens by architecture test | `PdfTableOfContentsService`, `MetadataExtractionService`, `IsbnDetectionService`, `PdfWriteBackService` | 2 |
| PDF-B-003 | Implement stable source snapshot/hash and TOCTOU policy | `PdfInputBroker`, `PdfWorkerClient`, extraction entities | 2/4 |
| PDF-B-004 | Complete physical OS sandbox evidence and independent review | worker/sandbox/platform adapters | 2/11 |
| PDF-B-005 | Create one worker-owned document context | `PdfWorkerCommand`, `PdfWorkerClient`, renderer factory | 4 |
| PDF-B-006 | Replace broad empty/zero/fallback error swallowing with typed diagnostics | `PdfiumAdapter`, metadata/TOC services | 4 |
| PDF-B-007 | Canonicalise page boxes/rotation/transforms | `IPdfRenderer`, `PdfiumAdapter`, `ReaderViewModel`, overlays | 5 |

## P1 — reader quality and semantic fidelity

| ID | Change | Primary paths | Phase |
|---|---|---|---:|
| PDF-B-008 | Add full render policy to request/cache key | `RenderRequest`, `PageRenderCache` | 5 |
| PDF-B-009 | Add preview/full/tile scheduling and focal-point zoom | reader cache/session/view model | 6 |
| PDF-B-010 | Finish continuous virtualised scroll decision and implementation | `ReaderView`, `ReaderViewModel` | 6 |
| PDF-B-011 | Add font/ToUnicode/reading-order quality fields | text layer/application and extraction persistence | 7 |
| PDF-B-012 | Fix false scanned-placeholder search matches | `InDocumentSearchService` | 7 |
| PDF-B-013 | Add page labels/destinations/links/history | TOC/navigation contracts and reader UI | 8 |
| PDF-B-014 | Add embedded-thumbnail/source precedence/failure states | `ThumbnailService`, visual manifests/catalogue | 10 |
| PDF-B-015 | Propagate selected text/OCR uncertainty to FTS/vectors/AI | extraction/FTS/embedding/advisor contracts | 10 |

## P1 — safety and release proof

| ID | Change | Primary paths | Phase |
|---|---|---|---:|
| PDF-B-016 | Define annotation/form/signature/active-content policy | reader/application/worker | 9 |
| PDF-B-017 | Separate writer validation from reader reopen check | `PdfWriteBackService`, ADR-0008 | 9/12 |
| PDF-B-018 | Build lawful mixed real-PDF corpus and visual/text oracle | tests/fixtures/evidence | 3/7/11 |
| PDF-B-019 | Run reference Windows/macOS wheel, AT and performance journeys | test/evidence harness | 11 |
| PDF-B-020 | Publish profile, known limits, native/OCR/license manifest | release docs/CI | 12 |

## Definition of done for each backlog item

The item has a code change (if needed), a focused test, corpus coverage or a
recorded `NOT_ASSESSED`, an evidence file with environment/version metadata,
updated profile/docs, and a rollback/recovery note where the change can alter
source files, caches or security behavior.
