# Executive decision

## Verdict

Ogma is not yet PDF-standard compliant as a release claim. It is an
architecturally promising PDF reader prototype with several production-minded
controls, but its conformance boundary is undefined and material processor
responsibilities remain either partial or unassessed.

The correct next move is a 12-phase, evidence-led PDF capability programme,
not a blanket “support PDF 2.0” promise. The first deliverable must be a
declared profile covering the features Ogma supports, the features it safely
degrades, and the features it intentionally refuses.

## What is already valuable

- The application targets PDFium through an adapter and uses a persistent
  worker session for production rendering.
- A broker validates root bounds, extension, magic bytes and input size before
  worker entry; password transport was moved to a one-shot stdin handshake.
- Worker outputs are sandbox-root checked, size limited and hashed.
- Extraction artifacts, ISBN evidence, TOC results, OCR and FTS have increasing
  content-hash/version provenance.
- The reader has page navigation, a page-only scroll surface, zoom modes,
  cache/prefetch and persisted reader state.
- Database-first annotations and deferred write-back protect original files in
  principle.

## What prevents the claim today

1. **No declared capability profile.** The code does not expose a versioned
   statement of supported PDF 2.0, PDF/UA, PDF/A input and interactive features.
2. **Boundary leakage.** `PdfTableOfContentsService`, metadata extraction,
   ISBN detection and write-back contain direct PdfPig/PDFsharp file opens;
   this conflicts with the plan’s statement that all parse/render/OCR work
   uses the broker and containment boundary.
3. **Incomplete containment proof.** Windows process limits and worker tests are
   useful, but actual Windows/macOS filesystem/network/child-process escape
   evidence and independent security approval remain open.
4. **Rendering model is incomplete.** `PdfiumAdapter` loads the whole file,
   disables annotations and form fill, does not use tiling, and does not expose
   page box, crop origin, color, optional-content or render-intent choices.
5. **Page geometry is split.** Actual renderer dimensions coexist with a UI
   720×960 fallback and normalized overlays; rotated, landscape, cropped and
   unusual page boxes can therefore misalign fit, selection and annotation.
6. **Text is treated too heuristically.** PdfPig words are useful, but the
   current quality heuristic does not prove font encoding/`ToUnicode` fidelity,
   reading order, vertical text, ligatures, tagged semantics or confidence.
7. **Navigation is narrower than PDF navigation.** Bookmark page numbers are
   retained, while named/explicit destinations, target coordinates, page
   labels, links, attachments and structure are largely discarded.
8. **Evidence is uneven.** The documented 1,071-test green run is strong source
   evidence, but it does not close real mixed-PDF, malformed/encrypted,
   physical Windows/macOS, accessibility, signing or release gates.

## Baseline scorecard

This is a directional audit score, not a product KPI. The research engine’s
audit rule caps published baseline audit scores at 65 until evidence quality is
adequate. The raw scores below expose the reasoning; the published baseline is
therefore **65/100 capped**, with high uncertainty in corpus and physical
dimensions.

| Dimension | Raw / 100 | Evidence judgement |
|---|---:|---|
| Standards scope and conformance declaration | 25 | No capability profile or conformance report |
| Safe input, isolation and active-content policy | 52 | Strong worker direction; real OS proof open |
| File structure and document model | 38 | Libraries parse common files; effective object model not owned by Ogma |
| Rendering fidelity and page geometry | 48 | PDFium path works; geometry/options/features incomplete |
| Text semantics and extraction | 45 | Versioned pipeline exists; mapping/quality coverage incomplete |
| Navigation and interchange | 35 | Basic outlines/page indices; destinations/labels/links absent or partial |
| Reader responsiveness | 50 | Cache/session and UI slices exist; target-scale and continuous flow open |
| Annotations/forms/signatures | 20 | Database annotations; PDF feature support not demonstrated |
| Assets/OCR/search/AI provenance | 58 | Good derived-artifact direction; assets and corpus quality remain open |
| Cross-platform, accessibility and release proof | 25 | Mostly NOT_ASSESSED |
| **Raw average** | **39.6** | Not a completion percentage |

The target at the end of Phase 12 is **≥95/100 on the evidence scorecard**,
with no unresolved P0/P1 blocker and every intentionally unsupported feature
represented in the profile and UI policy. A high score still does not mean
every PDF feature is implemented; it means the declared product profile is
accurate, safe and evidenced.

## Immediate decisions

- Freeze the words “PDF-standard compliant” until Phase 1 produces the profile.
- Keep source PDFs read-only by default; keep write-back opt-in and gated by
  backup, diff, verification, restore and conformance tests.
- Make the broker/worker the only parser and renderer entry point.
- Treat PDFium as the visual engine and PdfPig as a candidate extraction
  component, not as a complete PDF object model or conformance oracle.
- Prioritise page geometry, actual scroll/render pipeline, text provenance and
  hostile corpus evidence before AI, 3D or classroom polish.
