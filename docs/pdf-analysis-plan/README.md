# Ogma Library PDF conformance and reader-quality programme

Date: 2026-09-04
Status: Baseline and plan; implementation has not started from this document.
Owner: Ogma engineering owner
Review cadence: at every PDF-related increment, then at each release candidate

## Purpose

This document set critically compares the current Ogma codebase and its
authoritative 39-phase plan with the actual responsibilities of a PDF 2.0
processor. It turns the result into a 12-phase Kaizen programme for a safer,
smoother and more honest PDF reader.

The programme is deliberately layered over `docs/plans/aug-39/`. It does not
renumber or replace that roadmap. The existing roadmap remains the execution
authority; this directory supplies the PDF capability model, evidence gates,
missing controls and cross-phase sequencing that the existing plan needs.

## Start here

- [Decision and executive assessment](./00-executive-decision.md)
- [Scope, method and evidence rules](./01-scope-method-currentness.md)
- [PDF standards model](./02-pdf-standard-model.md)
- [Codebase architecture audit](./03-codebase-architecture-audit.md)
- [Rendering and page geometry audit](./04-rendering-page-geometry-audit.md)
- [File structure and security audit](./05-file-structure-security-audit.md)
- [Text, navigation and asset audit](./06-text-navigation-assets-audit.md)
- [Whole-app downstream audit](./07-whole-app-downstream-audit.md)
- [Plan alignment and Kaizen scorecard](./08-plan-alignment-kaizen-scorecard.md)
- [12-phase roadmap](./09-12-phase-roadmap-overview.md)
- [Source and claim register](./13-source-and-claim-register.md)
- [Risk register](./14-risk-register.md)
- [Acceptance corpus and release gates](./15-acceptance-corpus-and-gates.md)
- [Open decisions](./16-open-decisions.md)
- [Prioritised implementation backlog](./17-implementation-backlog.md)

Phase briefs are in [`phases/`](./phases/):

1. [Scope and capability profile](./phases/01-scope-and-capability-profile.md)
2. [Safe input and containment](./phases/02-safe-input-and-containment.md)
3. [Engine alignment and capability telemetry](./phases/03-engine-alignment-and-capability-telemetry.md)
4. [Document structure and identity](./phases/04-document-structure-and-identity.md)
5. [Page geometry and rendering contract](./phases/05-page-geometry-and-rendering-contract.md)
6. [Responsive reader pipeline](./phases/06-responsive-reader-pipeline.md)
7. [Text semantics and extraction](./phases/07-text-semantics-and-extraction.md)
8. [Navigation and interchange](./phases/08-navigation-and-interchange.md)
9. [Annotations, forms and active content](./phases/09-annotations-forms-and-active-content.md)
10. [Derived assets, OCR, search and AI provenance](./phases/10-derived-assets-ocr-search-and-ai.md)
11. [Cross-platform quality and accessibility](./phases/11-cross-platform-quality-and-accessibility.md)
12. [Release conformance and continuous improvement](./phases/12-release-conformance-and-re-audit.md)

## The governing conclusion

“PDF standard compliant” must mean a declared and tested processor capability
profile, not an unbounded promise to implement every PDF 2.0 feature. PDF
processors may support a subset of the specification; Ogma must state its
supported subset, fail safely for unsupported features, and distinguish file
conformance from reader/processor conformance.

The present app is a promising but incomplete PDF processor: its strongest
areas are adapter isolation, persistent worker sessions, page-aware derived
artifact versioning, bounded OCR policy, and reader cache/session foundations.
Its most important gaps are direct parser bypasses, incomplete actual sandbox
evidence, whole-file/duplicate parsing, incomplete page geometry, limited text
semantics, incomplete navigation, disabled annotation/form rendering, weak
thumbnail fallback, and missing physical/corpus conformance evidence.

## Status vocabulary

- `IMPLEMENTED`: code and focused tests support the claim.
- `DOCUMENTED`: a plan or progress note claims the work, but this audit did not
  independently prove the complete behavior.
- `PARTIAL`: a meaningful slice exists, with known gaps.
- `OPEN`: planned or started, but the acceptance gate is not closed.
- `NOT_ASSESSED`: evidence was not available, especially physical platform,
  real OS sandbox, accessibility, signing, or licensed corpus evidence.
- `OUT_OF_PROFILE`: intentionally unsupported in the declared reader profile;
  the user experience must explain this rather than silently misrendering.

## Evidence discipline

Every future claim in this programme should identify the code path, test or
corpus fixture, commit/build, OS/hardware, dependency versions, dataset hash,
date and owner. A mock, synthetic PDF or headless Avalonia render is useful
evidence but does not prove real-document, physical-platform or release
behavior. See [the claim register](./13-source-and-claim-register.md).
