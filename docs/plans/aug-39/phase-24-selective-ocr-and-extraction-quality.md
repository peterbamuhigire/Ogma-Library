# Phase 24 — Selective OCR and Extraction Quality

> [Roadmap index](./README.md) · [Previous](./phase-23-full-text-pipeline-and-search.md) · [Next](./phase-25-versioned-embeddings-and-vector-lifecycle.md)

## Objective
Use local OCR only for image-based/low-quality pages and expose reviewable quality.

## Business/Product Rationale
Scanned books need searchability, but indiscriminate OCR wastes resources and degrades text.

## SDLC Requirements
FR-READ-008, ADR local Tesseract, PDF/OCR policy requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Ocr/TesseractOcrProvider.cs` and `src/OgmaLibrary.Workers/Ocr/` exist; detection, language packs, quality and packaging evidence are incomplete.

## Gap Analysis
No reliable page classification, confidence thresholds, correction/retry policy or native asset packaging proof.

## Architectural Impact
OCR is an optional extraction stage producing page alternatives, never overwriting primary text blindly.

## Database Work
Page classification, OCR run/model/language/confidence and selected text source.

## Backend Work
Image/quality detector, language policy, deskew/render controls, selective OCR and quality comparison.

## Frontend Work
OCR-needed/progress/quality/retry/language controls and correction guidance.

## PDF Processing Impact
Runs inside containment with stricter CPU/memory limits.

## Metadata Impact
OCR-derived ISBN/title proposals are labeled lower-confidence.

## Search Impact
Selected OCR text indexes by version.

## AI/RAG Impact
Low-quality OCR evidence carries confidence and may be excluded.

## 3D Bookshelf Impact
None.

## External Integrations
No cloud OCR in baseline.

## Privacy Requirements
OCR remains local.

## Security Requirements
Packaged trained data is checksum-verified; sandbox/resource limits apply.

## Performance Requirements
OCR only qualifying pages; cancellable; benchmark CPU/memory and batch impact.

## Error & Recovery Behaviour
OCR failure leaves book readable and original extraction intact.

## Logging/Observability
Pages selected, confidence, language, duration and resource-limit failures.

## Testing
Unit classifier; DB version; image/mixed PDF pipeline; language-pack packaging; UI quality states; E2E cancel/retry; OCR accuracy fixtures; resource performance.

## Skills Engines Applied
`skills-web-dev` AI/content pipeline and packaging; `srs-skills` selective policy.

## Dependencies
Phases 10–11 and 23.

## Parallelisation
Classifier and OCR adapter improvements can proceed against page artifact contract.

## Migration Considerations
Legacy OCR outputs marked unversioned and regenerated only when needed.

## Definition of Done
- [ ] Text PDFs are not OCRed.
- [ ] Image/low-quality pages are detected and versioned.
- [ ] OCR failure is non-blocking.
- [ ] Accuracy/resource corpus meets approved gates.
- [ ] Tesseract assets ship on both platforms.

## Kaizen Review
1. Complexity: dual text sources. 2. Reuse page quality. 3. Simplify downstream selected-text view. 4. Delete blanket OCR paths. 5. Document language policy. 6. Pattern: confidence-selected artifact. 7. Debt decreases.
