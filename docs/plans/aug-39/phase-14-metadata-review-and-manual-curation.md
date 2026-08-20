# Phase 14 — Metadata Review and Manual Curation

> [Roadmap index](./README.md) · [Previous](./phase-13-bibliographic-provider-gateway.md) · [Next](./phase-15-safe-writeback-and-override-protection.md)

## Objective
Deliver a complete possible-match, field proposal and manual metadata editor.

## Business/Product Rationale
Human curation is the final authority when documents and providers disagree.

## SDLC Requirements
FR-META-003/004/007/008, FR-CAT-004/005 and user-correction requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Metadata/BookMetadataEnrichmentService.cs` and `src/OgmaLibrary.App/Views/Catalogue/BookDetailView.axaml` expose enrichment pieces, but no complete review queue/editor/undo journey.

## Gap Analysis
Users cannot compare alternatives, protect fields, or safely batch edit.

## Architectural Impact
Application commands mediate accept/reject/edit/lock/undo; UI never writes EF directly.

## Database Work
Review queue, decision batch, before/after values, actor and undo token.

## Backend Work
Validated DTOs, optimistic concurrency, batch preview and reversible commands.

## Frontend Work
Metadata editor, side-by-side provenance, confidence explanations, match alternatives and bulk preview.

## PDF Processing Impact
Re-extract creates proposals without disturbing edits.

## Metadata Impact
Primary deliverable.

## Search Impact
Accepted edits enqueue targeted index updates.

## AI/RAG Impact
AI-inferred proposals are visibly labeled and never auto-accepted.

## 3D Bookshelf Impact
Accepted display changes refresh shelf assets/labels.

## External Integrations
Provider alternatives link to source identifiers.

## Privacy Requirements
Editor shows whether a value came from an external service or AI.

## Security Requirements
Sanitize rich text/URLs; concurrency protects against stale overwrite.

## Performance Requirements
Open detail/review without loading full PDF text or raw responses.

## Error & Recovery Behaviour
Failed save preserves draft; batch is atomic or itemized with retry.

## Logging/Observability
Decision counts/reasons and undo outcomes, excluding private notes.

## Testing
Unit validators/precedence; DB concurrency/undo; provider/extraction proposal integration; API commands; UI keyboard/accessibility/E2E; batch performance; filesystem unaffected tests.

## Skills Engines Applied
`design-system-skills` review UX; `srs-skills` acceptance; `skills-web-dev` command/concurrency patterns.

## Dependencies
Phases 12–13.

## Parallelisation
Editor and command layer can proceed against frozen DTOs.

## Migration Considerations
Existing auto-applied provider fields are flagged for provenance review where confidence is unclear.

## Definition of Done
- [ ] Every canonical field is editable or explicitly read-only.
- [ ] Source/confidence/override are visible.
- [ ] Low-confidence matches require review.
- [ ] Bulk preview and undo work.
- [ ] Keyboard/screen-reader journeys pass.

## Kaizen Review
1. Complexity: reversible editing. 2. Reuse proposal cards. 3. Simplify detail actions. 4. Remove direct writes. 5. Document curation rules. 6. Pattern: previewable command. 7. Debt decreases.
