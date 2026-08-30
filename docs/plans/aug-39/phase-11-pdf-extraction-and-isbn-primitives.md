# Phase 11 — PDF Extraction and ISBN Primitives

> [Roadmap index](./README.md) · [Previous](./phase-10-pdf-validation-and-containment.md) · [Next](./phase-12-canonical-metadata-and-provenance.md)

## Objective
Produce versioned, page-aware metadata, text, TOC and render outputs robustly.

## Business/Product Rationale
Every downstream quality promise depends on trustworthy extraction.

## SDLC Requirements
FR-META-001, FR-READ-001/004/007, FR-SEARCH-002, PDF pipeline requirements.

## Current Repository State
The `src/OgmaLibrary.Infrastructure/Pdf/` adapters and PDF/ISBN tests contain PdfPig/PDFtoImage primitives; TOC handling and extraction quality/versioning are weak.

## Gap Analysis
Sparse fields, naive page output, Unicode/large-file/TOC quality and deterministic version manifests incomplete.

## Architectural Impact
Define `ExtractionArtifact` contracts with parser/config/source hashes.

## Database Work
Extraction runs, page quality, detected identifiers/evidence, TOC nodes and artifact manifests.

## Backend Work
Page-preserving text, embedded metadata normalization, ISBN evidence and quality scoring.

## Frontend Work
Extraction status, detected identifiers and reprocess controls.

## PDF Processing Impact
Primary deliverable; all work idempotent by asset+extractor version.

## Metadata Impact
Emit proposals, never direct canonical overwrites.

## Search Impact
Page/TOC inputs become stable.

## AI/RAG Impact
Evidence can cite page and extraction quality.

## 3D Bookshelf Impact
First-page render is an asset source, not the only cover policy.

## External Integrations
None.

## Privacy Requirements
Extracted text remains local until explicit AI tier.

## Security Requirements
Outputs are size-limited and sanitized through Phase 10.

## Performance Requirements
Stream large documents; per-book ceilings and batch throughput benchmark.

## Error & Recovery Behaviour
Partial page failures are recorded; usable metadata can survive text failure.

## Logging/Observability
Pages, bytes, quality, duration, parser version and failure codes.

## Testing
Unit normalization/ISBN; DB artifact version; licensed PDF pipeline matrix; API status; filesystem large file; E2E reprocess; Unicode/TOC snapshots; throughput/memory tests.

## Skills Engines Applied
`skills-web-dev` pipeline/versioning; `srs-skills` evidence/acceptance.

## Dependencies
Phase 10.

## Parallelisation
Embedded metadata, text/TOC and render adapters can proceed against shared contracts.

## Migration Considerations
Legacy pages/chunks receive legacy version and are scheduled for controlled regeneration.

## Definition of Done
- [ ] Outputs are page-aware and versioned.
- [ ] ISBN evidence is retained.
- [ ] Mixed/malformed/Unicode corpus is isolated.
- [ ] Reprocessing is idempotent.
- [ ] Resource budgets pass.

## Kaizen Review
1. Complexity: artifact variants. 2. Reuse one manifest. 3. Simplify downstream extraction assumptions. 4. Remove empty TOC replacement. 5. Document quality scores. 6. Pattern: versioned derived artifact. 7. Debt decreases.
