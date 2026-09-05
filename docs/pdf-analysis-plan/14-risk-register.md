# PDF programme risk register

| ID | Risk | Severity | Current signal | Mitigation / trigger |
|---|---|---:|---|---|
| PDF-P0-01 | Direct parser bypass reads hostile input outside containment | Critical | Direct PdfPig/PDFsharp callers found | Architecture test; route through document context before further features |
| PDF-P0-02 | Claimed compliance has no declared supported subset | Critical | No profile found | Phase 1 profile required before public claim |
| PDF-P0-03 | OS sandbox is weaker than documented | Critical | Worker/Job Object evidence; platform proof open | Real Windows/macOS escape corpus and independent review |
| PDF-P0-04 | Source changes during copy create mixed artifacts | Critical | Broker then copy flow | Snapshot/hash and retry/fail-closed policy |
| PDF-P0-05 | A malformed PDF is hidden as empty/zero/fallback | High | Broad catches in adapter/services | Typed diagnostics; strict/lenient dual outcomes |
| PDF-P1-01 | Page boxes/rotation drift from UI overlays | High | 720×960 fallback and separate rotation | Canonical effective geometry and transform tests |
| PDF-P1-02 | Whole-file/duplicate parsing causes slow open/page turn | High | `File.ReadAllBytes`, multiple PdfPig opens | Warm context, bounded stream/random access, cache benchmark |
| PDF-P1-03 | Unsupported annotation/form/OCG behavior is silent | High | Annotations/forms disabled in rendering options | Explicit feature policy and visible degraded status |
| PDF-P1-04 | Unicode/reading-order errors pollute search and AI | High | Heuristic word extraction; OCR fallback | Font/ToUnicode corpus, confidence and provenance |
| PDF-P1-05 | Navigation loses destinations/page labels/links | Medium | TOC limited to page numbers | Full navigation model with safe fallback |
| PDF-P1-06 | Thumbnail gaps look like empty dialogs/cards | Medium | Generated first-page path only | Embedded/generated/failure state contract |
| PDF-P1-07 | Write-back corrupts or invalidates source/signature | Critical | PDFsharp path exists; separate writer proof absent | Keep off by default; backup/diff/verify/restore/signature gate |
| PDF-P1-08 | Physical AT/platform behavior differs from headless tests | High | Physical evidence open | Windows Narrator, macOS VoiceOver, wheel/packaged tests |
| PDF-P2-01 | Dependency upgrade changes render/extraction behavior | High | PdfPig 0.1.9 vs newer upstream | Pin, benchmark and record compatibility before upgrade |
| PDF-P2-02 | AI repeats derived text as authoritative PDF truth | High | Downstream consumers depend on extraction | Evidence envelope and uncertainty propagation |

No risk is closed by a plan sentence. Closure requires linked evidence and an
owner decision recorded in the phase gate.
