# Phase 25 — Extensibility and imports

## 1. Header

| Field | Value |
|---|---|
| Wave | G. Ship |
| Size | 4–8 engineering days (recommended scope), or a recorded deferral only (about 1 day) |
| Depends on | 10 (catalogue experience: shelves, bulk edit, tags), D-10 |
| Owner decisions | **D-10** Extensibility scope for v1 (recommended: defer the plugin loader and the local API; ship Calibre and Zotero import only) |
| Primary defects | K90 (the EXT group has 0 wired, 1 partial and 2 missing FRs) |
| Requirements | FR-EXT-001 (extension points), FR-EXT-002 (local read API), FR-EXT-003 (Zotero, Calibre and Goodreads import; theme packs) |

## 2. Why this phase exists

The SRS promises three kinds of extensibility. Today none of them is usable
([04-inventory-and-requirements.md](../04-inventory-and-requirements.md)):

- **FR-EXT-001, partial.** `[ExtensionPoint]` attributes exist on `IAiCatalogueReader`,
  `IRecommendationSource` and `IOcrProvider`
  (`src/OgmaLibrary.Application/Extensions/ExtensionPointAttribute.cs`), but there is no loader, no
  manifest, no isolation and no UI.
- **FR-EXT-002, missing.** There is no local read API.
- **FR-EXT-003, missing.** There are no importers and no theme packs.

The requirement matrix (`docs/plans/aug-39/appendices/01-requirement-phase-matrix.md:100`) says
FR-EXT-002 and FR-EXT-003 "may be deferred from the first public release only through an approved
SRS change/risk acceptance … they may not disappear silently". This phase either delivers each item
or records that approved deferral.

**Recommendation under D-10.** Most prospective users arrive with an existing collection in
**Calibre** or references in **Zotero**. Import removes the largest adoption barrier: it carries
over titles, authors, tags, collections and reading state. By contrast, a plugin loader or a local
HTTP API adds a code-execution or network surface just after Phase 23 closed the threat model, and
it serves few users in v1. So: ship the importers, keep `[ExtensionPoint]` as internal compile-time
seams guarded by architecture tests, and defer the loader, local API, Goodreads import and theme
packs through an approved SRS change with a dated revisit.

## 3. Objectives and exit criteria

1. D-10 is decided and recorded in `docs/adrs/` as a new ADR (extensibility scope for v1).
2. **Calibre import:** a user selects a Calibre library folder (`metadata.db` plus book folders).
   Ogma previews what it will import (counts, conflicts, non-PDF formats skipped) and imports
   PDF-bearing entries: title, authors, series, tags, publisher, ISBN, rating, comments and the
   cover (as a metadata source with provenance "Calibre"). Custom columns are ignored. The import is
   staged, reversible and never modifies the Calibre library.
3. **Zotero import:** from a Zotero RDF or CSL-JSON export (no live Zotero database access), match
   items to Ogma books by DOI, ISBN or title plus author; propose metadata merges through the
   existing review flow; import collections as shelves; and link attached PDFs that are inside an
   Ogma library root.
4. Every import runs under the Phase 05 file-validity rules and the Phase 06 identity resolution. A
   re-run is idempotent: no duplicates.
5. FR-EXT-001: architecture tests assert that extension-point interfaces live in Application and
   have no Infrastructure dependency. No runtime loading of third-party code exists in v1.
6. FR-EXT-002, the Goodreads import and theme packs are either delivered or covered by an approved
   SRS change and risk acceptance, with the matrix and `Test-RequirementAccountability.ps1` updated
   so the accountability gate still passes.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\architecture\system-architecture-design\SKILL.md` (ports and adapters for importers, ADR; the former `hexagonal-architecture` skill is an inactive alias routed here)
- `C:\wamp64\www\chwezi-dev-engine\skills\architecture\api-design-first\SKILL.md` (only if FR-EXT-002 is kept)
- `C:\wamp64\www\chwezi-dev-engine\skills\backend-databases\database-design-engineering\SKILL.md` (staging tables, idempotency)
- `C:\wamp64\www\chwezi-dev-engine\skills\security\vibe-security-skill\SKILL.md` (untrusted import files)
- `C:\wamp64\www\srs-skills\09-governance-compliance\01-traceability-matrix\SKILL.md` (matrix update or deferral)
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\error-empty-and-system-messaging\SKILL.md` (import preview and conflict copy)
- Currentness gate: `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md`,
  for the current Calibre `metadata.db` schema and Zotero export formats. Record the versions tested.

## 5. Scope

**In (recommended D-10):** ADR; Calibre importer; Zotero export importer; import preview, conflict
review and undo; architecture tests for extension points; SRS change and deferral records for the rest.

**Out (deferred unless D-10 says otherwise):** a dynamic plugin loader; a loopback read API;
Goodreads CSV import; theme packs; live synchronisation with Calibre or Zotero; non-PDF formats (EPUB, MOBI).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 25.1 | Obtain D-10; write the ADR with options, the security rationale and the revisit date. | `docs/adrs/0017-extensibility-scope-v1.md` | ADR accepted by the owner |
| 25.2 | Define the import port: `ILibraryImportSource` (read-only), producing a normalised `ImportCandidate` stream (identifiers, fields, tags, collections, cover stream, file locator). | `src/OgmaLibrary.Application/Import/` | Architecture tests pass; no Infrastructure types in the port |
| 25.3 | Calibre adapter: open `metadata.db` read-only (`Mode=ReadOnly`, immutable URI) on a temporary copy; map books, authors, tags, series, identifiers, ratings and comments; locate PDF files; stream `cover.jpg`. | `src/OgmaLibrary.Infrastructure/Import/Calibre/` | Fixture Calibre library (synthetic, generated by a test script) imports with exact counts |
| 25.4 | Zotero adapter: parse CSL-JSON and Zotero RDF exports; map DOI, ISBN, title, creators, tags and collections; resolve attachments by path. | `src/OgmaLibrary.Infrastructure/Import/Zotero/` | Fixture exports parse; malformed files produce a localised error, never a crash |
| 25.5 | Staged import service: a preview (new, matched, conflicting, skipped with reason), apply in batches, the metadata review queue for conflicts, collections to shelves, and a single undo via the existing command history (`ICommandHistory`). | Application and Infrastructure import service; `ICatalogueWriteService` | Re-run is idempotent; undo restores the pre-import catalogue exactly |
| 25.6 | Import UI in Settings → Library: choose source, preview table, conflict counts, *Import* and *Undo last import*; progress in the Activity Centre. | Settings view (Phase 08), Activity Centre | Real-window journey: import a 50-item Calibre fixture, undo, re-import |
| 25.7 | Provenance: imported fields carry source "Calibre" or "Zotero" with timestamp and confidence, shown in the detail inspector provenance tab. | Metadata provenance | Detail inspector shows the source per field |
| 25.8 | Extension-point hygiene: architecture tests for `[ExtensionPoint]` interfaces; document them in the developer guide as internal seams. | `tests/OgmaLibrary.Tests.Architecture/`, `docs/developer-guide/` | Tests green |
| 25.9 | Deferral records for FR-EXT-002, Goodreads import and theme packs: an SRS change note, risk acceptance, and matrix update. | `docs/plans/aug-39/appendices/01-requirement-phase-matrix.md`, SRS change log | `Test-RequirementAccountability.ps1` passes; the owner has signed the deferral |
| 25.10 | Localise all import strings (en, fr) and add help topics (Phase 28 help system). | Localisation resources | No hard-coded strings (UIA and resource check) |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| New users re-enter their library by hand | No importers (FR-EXT-003 missing) | Calibre and Zotero importers | Import cuts onboarding time for existing collectors | Manual only → 500-item Calibre import ≤ 2 min with ≥ 98 % field fidelity | Fixture and timing | Schema drift across Calibre versions | Version check; refuse unsupported schemas cleanly |
| Extensibility promised but ambiguous | Attributes without a loader | ADR plus internal seams plus approved deferral | Clear scope avoids unsafe half-features | 3 FRs unaccounted → 3 accounted | ADR, matrix | Owner wants plugins in v1 | Plugin design becomes a v1.1 Kaizen cycle |
| Imports could corrupt the catalogue | New write path | Staged preview and single undo | Users trust bulk changes they can reverse | — → undo is exact in tests | Undo test | Undo misses side tables | Snapshot backup before apply |

## 8. Test plan

- **Unit:** field mapping per adapter; identifier normalisation (ISBN-10 to ISBN-13, DOI case);
  idempotency keys; conflict classification.
- **Integration:** a synthetic Calibre library generated by a script (original content only) and
  synthetic Zotero exports; full import, undo and re-import; a read-only guarantee (hash the Calibre
  `metadata.db` before and after).
- **Real window:** the import journey from Settings, including preview, conflict review and undo.
- **Negative:** a locked or corrupt `metadata.db`; a missing PDF on disk; a 10 MB CSL-JSON file;
  hostile strings in titles (script injection into UI text); paths outside library roots.

## 9. Acceptance commands

```powershell
./scripts/Test-RequirementAccountability.ps1
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "FullyQualifiedName~Import"
dotnet test tests/OgmaLibrary.Tests.Architecture --configuration Release --no-build
# Real-window: import journey from the Phase 01 harness
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence while open |
|---|---|---|
| D-10 decision | Owner | Phase cannot start |
| Import against a real Calibre library of the owner's | Owner, run locally; no content leaves the machine | Fidelity measured on synthetic fixtures only |
| Calibre and Zotero format currentness | Implementer, via the currentness gate | Supported-version statement unverified |

## 11. Risks and mitigations

- **Calibre schema changes.** Detect the schema version; support a tested range; show a clear
  "unsupported version" message.
- **Duplicate books after import.** Run the Phase 06 identity resolution before insert, and keep the
  preview honest about matches.
- **Scope creep toward plugins.** The ADR limits v1; plugin ideas go into the next Kaizen cycle backlog.

## 12. Execution prompt

```
## Prompt 25 - Extensibility and imports
You are delivering the v1 extensibility scope in C:\wamp64\www\Ogma-Library. Read first, in order:
C:\wamp64\www\Ogma-Library\CLAUDE.md; docs/plans/sept-23-kaizen/README.md; docs/plans/sept-23-kaizen/AGENT_BRIEF.md;
docs/plans/sept-23-kaizen/08-master-plan.md (D-10); docs/plans/sept-23-kaizen/phases/phase-25-extensibility-and-imports.md.
Stop and ask for D-10 if it is not recorded. Read the SKILL.md files in section 4 directly.
A. Serial: 25.1 ADR -> 25.2 port -> 25.5 staged import service.
B. Parallel after 25.2: 25.3 Calibre adapter, 25.4 Zotero adapter.
C. Then: 25.6 UI, 25.7 provenance, 25.8 architecture tests, 25.9 deferral records, 25.10 localisation.
Never modify the source Calibre library or Zotero data. Use synthetic fixtures only in the repo. Record the
completion at docs/implementation/execution/phase-sept23-25-completion.md.
```
