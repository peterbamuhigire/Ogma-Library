# Ogma Library August 39-Phase Plan

This directory is the canonical execution plan for the C#/.NET Avalonia Ogma Library application on Windows and macOS. It contains exactly 39 implementation phases. There are no mobile, PWA or separate web-application phases. The Three.js bookshelf is an embedded renderer owned by the desktop application.

Start with the [roadmap overview](./00-master-roadmap.md), then execute the phase files in dependency order. Phase numbers are stable traceability identifiers; renaming a phase requires updating the requirement and module matrices.

## Phase files

| Phase | File |
| ---: | --- |
| 1 | [Evidence Baseline and Scope Freeze](./phase-01-evidence-baseline-and-scope-freeze.md) |
| 2 | [Composition, Configuration and Startup](./phase-02-composition-configuration-and-startup.md) |
| 3 | [Canonical Library Identity Model](./phase-03-canonical-library-identity-model.md) |
| 4 | [Identity Schema and Data Migration](./phase-04-identity-schema-and-data-migration.md) |
| 5 | [Library Roots and Path Security](./phase-05-library-roots-and-path-security.md) |
| 6 | [Processing State Machine and Scan Sessions](./phase-06-processing-state-machine-and-scan-sessions.md) |
| 7 | [Discovery and Incremental Scanning](./phase-07-discovery-and-incremental-scanning.md) |
| 8 | [Filesystem Reconciliation and Recovery](./phase-08-filesystem-reconciliation-and-recovery.md) |
| 9 | [Duplicate and Bibliographic Resolution](./phase-09-duplicate-and-bibliographic-resolution.md) |
| 10 | [PDF Validation and Containment](./phase-10-pdf-validation-and-containment.md) |
| 11 | [PDF Extraction and ISBN Primitives](./phase-11-pdf-extraction-and-isbn-primitives.md) |
| 12 | [Canonical Metadata and Provenance](./phase-12-canonical-metadata-and-provenance.md) |
| 13 | [Bibliographic Provider Gateway](./phase-13-bibliographic-provider-gateway.md) |
| 14 | [Metadata Review and Manual Curation](./phase-14-metadata-review-and-manual-curation.md) |
| 15 | [Safe Writeback and Override Protection](./phase-15-safe-writeback-and-override-protection.md) |
| 16 | [Cover, Thumbnail and Spine Assets](./phase-16-cover-thumbnail-and-spine-assets.md) |
| 17 | [Worker Reliability and Observability](./phase-17-worker-reliability-and-observability.md) |
| 18 | [Ogma Design System and Application Shell](./phase-18-ogma-design-system-and-application-shell.md) |
| 19 | [Production 2D Catalogue](./phase-19-production-2d-catalogue.md) |
| 20 | [Book Detail, Organisation and Reading State](./phase-20-book-detail-organisation-and-reading-state.md) |
| 21 | [Reader Completion and Portability](./phase-21-reader-completion-and-portability.md) |
| 22 | [Structured and Fuzzy Catalogue Search](./phase-22-structured-and-fuzzy-catalogue-search.md) |
| 23 | [Full-Text Pipeline and Search](./phase-23-full-text-pipeline-and-search.md) |
| 24 | [Selective OCR and Extraction Quality](./phase-24-selective-ocr-and-extraction-quality.md) |
| 25 | [Versioned Embeddings and Vector Lifecycle](./phase-25-versioned-embeddings-and-vector-lifecycle.md) |
| 26 | [Semantic and Hybrid Retrieval](./phase-26-semantic-and-hybrid-retrieval.md) |
| 27 | [AI Gateway, Privacy and Cost Runtime](./phase-27-ai-gateway-privacy-and-cost-runtime.md) |
| 28 | [Advisor Intent, Candidates and Reranking](./phase-28-advisor-intent-candidates-and-reranking.md) |
| 29 | [Grounded Explanations and Answer Mode](./phase-29-grounded-explanations-and-answer-mode.md) |
| 30 | [Advisor UX and Quality Evaluation](./phase-30-advisor-ux-and-quality-evaluation.md) |
| 31 | [Native 3D Host and Catalogue Contract](./phase-31-native-3d-host-and-catalogue-contract.md) |
| 32 | [Virtual Bookshelf Visuals and Interaction](./phase-32-virtual-bookshelf-visuals-and-interaction.md) |
| 33 | [3D Scale, Accessibility and Performance](./phase-33-3d-scale-accessibility-and-performance.md) |
| 34 | [Classroom Host Security and Read Model](./phase-34-classroom-host-security-and-read-model.md) |
| 35 | [Classroom Client, Offline and Sync](./phase-35-classroom-client-offline-and-sync.md) |
| 36 | [School Administration and Managed AI](./phase-36-school-administration-and-managed-ai.md) |
| 37 | [Security, Privacy and Data Protection Hardening](./phase-37-security-privacy-and-data-protection-hardening.md) |
| 38 | [Performance, Reliability, Packaging and Beta](./phase-38-performance-reliability-packaging-and-beta.md) |
| 39 | [Cross-Platform Release Acceptance and Handover](./phase-39-cross-platform-release-acceptance-and-handover.md) |

## Appendices

- [Requirement Phase Matrix](./appendices/01-requirement-phase-matrix.md)
- [Module Phase Matrix](./appendices/02-module-phase-matrix.md)
- [Database Roadmap](./appendices/03-database-roadmap.md)
- [Pdf Processing Roadmap](./appendices/04-pdf-processing-roadmap.md)
- [Search Ai Roadmap](./appendices/05-search-ai-roadmap.md)
- [Design System Roadmap](./appendices/06-design-system-roadmap.md)
- [3d Bookshelf Roadmap](./appendices/07-3d-bookshelf-roadmap.md)
- [Testing Roadmap](./appendices/08-testing-roadmap.md)
- [Security Privacy Roadmap](./appendices/09-security-privacy-roadmap.md)
- [Risk Register](./appendices/10-risk-register.md)
- [Architecture Decisions Required](./appendices/11-architecture-decisions-required.md)
- [Data Flows](./appendices/12-data-flows.md)

## Source audit

The plan is derived from the repository audit under [docs/audit](../../audit/) and the latest controlled SDLC corpus under [docs/references](../../references/). The audit estimated current functional completion at 40–48% and issued a public-beta NO-GO pending the remediation and release gates in these phases.
