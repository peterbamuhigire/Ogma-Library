# Phase 16 — Cover, Thumbnail and Spine Assets

> [Roadmap index](./README.md) · [Previous](./phase-15-safe-writeback-and-override-protection.md) · [Next](./phase-17-worker-reliability-and-observability.md)

## Objective
Resolve, generate, cache and invalidate deterministic visual assets at multiple sizes.

## Business/Product Rationale
Covers and spines are central to a beautiful, recognizable library and the 3D shelf.

## SDLC Requirements
Cover-art domain, FR-CAT-001/004, 3D asset requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Assets/ThumbnailService.cs` generates one 200×300 first-page JPEG; spine jobs are not enqueued; `Catalogue/CatalogueReadModel.cs` returns null cover paths.

## Gap Analysis
No source precedence, variants, embedded/external/custom flow, manifest or read-model connection.

## Architectural Impact
Create a shared `VisualAssetResolver` consumed by 2D and 3D.

## Database Work
Asset source, dimensions, format, hash, generation version, status, custom lock and variants.

## Backend Work
Embedded/first-page/provider/custom precedence, safe download, deterministic resize/crop, spine texture generation and garbage collection.

## Frontend Work
Reusable cover component, placeholders, replace/crop/regenerate and processing/error states.

## PDF Processing Impact
First-page render is versioned input.

## Metadata Impact
External cover identifiers retain provenance; custom cover wins.

## Search Impact
None.

## AI/RAG Impact
None.

## 3D Bookshelf Impact
Stable cover/spine texture URIs and low/high resolution variants.

## External Integrations
Approved cover endpoints only, cached with license/source metadata where required.

## Privacy Requirements
Remote cover fetch reveals identifier; disclose/cache/minimize.

## Security Requirements
Image decode limits, fixed endpoints, content validation and no arbitrary URL fetch.

## Performance Requirements
Lazy variants, bounded disk/GPU sizes and no high-res catalogue flood.

## Error & Recovery Behaviour
Corrupt/missing source falls through to deterministic Ogma placeholder.

## Logging/Observability
Source, cache hit, generation time, dimensions and failure code.

## Testing
Unit precedence; DB manifest; PDF/provider/image pipeline; API asset authorization; UI snapshots; filesystem cache invalidation; 2D E2E; batch/memory performance; 3D URI contract tests.

## Skills Engines Applied
`design-system-skills` visual assets; `skills-web-dev` caching/security/performance.

## Dependencies
Phases 6, 11–14.

## Parallelisation
Resolver/generator and cover component can proceed against manifest DTOs.

## Migration Considerations
Import existing JPEGs as legacy variants or regenerate without deleting until success.

## Definition of Done
- [ ] 2D catalogue displays real generated/resolved covers.
- [ ] Multiple deterministic variants and spines exist.
- [ ] Custom cover is protected.
- [ ] Invalidation/garbage collection is safe.
- [ ] Large library asset budget passes.

## Kaizen Review
1. Complexity: source/variant matrix. 2. One resolver for 2D/3D. 3. Simplify thumbnail callers. 4. Remove orphan spine path. 5. Document precedence/licenses. 6. Pattern: versioned asset manifest. 7. Debt decreases.
