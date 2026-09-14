# Detailed product and functionality audit

Audit date: 2026-09-14. Commit: `a93900a2bd7313a2948a29d2813ec3e14696f877`.
Evidence modes: source inspection, Windows execution, native window capture, UI Automation, regression tests, and complete extracted-text review of the 19 supplied references. See [test results](04-test-report.md), [visual evidence](02-ui-ux-audit.md), and [document ledger](evidence/reference-review-ledger.md).

## Decision

Ogma should remain in development. It has meaningful library and PDF infrastructure, but the current desktop experience does not reliably deliver its central promise: find a legally acquired book, open it, read comfortably, keep notes, and return later without losing context.

The first priority is restoring control of the interface. The command palette and book inspector remain visible despite attempts to close them; the toolbar overlaps even in the larger captured window. More features on top of that shell would increase friction. The right panel needs both a binding repair and a deliberate information design overhaul.

The next priority is closing the distance between service implementation and user access. Reader layout modes, in-document search, privacy settings, job operations and school administration must be traced from an ordinary user's entry point through persistence and recovery. A service registered in dependency injection, a test-created view, or a passing contract test is insufficient proof of that journey.

## Product purpose and scope

The user's brief is the primary product intent: a Windows and macOS e-library for PDFs obtained legally, including school collections, with a PDF viewer enhanced by optional AI. The SRS supports this through BG-001 visible library, BG-002 usable library, BG-003 intelligent library, BG-004 durable ownership and BG-005 private defaults (SRS P0050-P0054 in the [extraction](evidence/reference-extract.txt)).

The useful product hierarchy is:

1. **Read and retain:** open reliably; navigate, search, annotate and resume; protect originals and reading state.
2. **Find and organize:** identify books, browse covers or lists, curate metadata, search content and maintain collections.
3. **Understand and discover:** optional recommendations, cited local evidence and reading plans, with clear capability and privacy boundaries.
4. **Operate a school library:** publish a permitted subset, enroll users, preserve student privacy, recover from network loss and govern provider use.

3D browsing is an additional way to discover books. It must not obstruct the dependable 2D library or reader. The public website reference is a scope/control input, not authorization to build a website in this remediation programme. Mobile, PWA, EPUB/CBZ, cloud synchronization and a new LMS remain outside this task.

Legally obtaining a PDF and allowing every classroom copying, export or AI-processing use are separate deployment-policy questions. This audit assumes lawful acquisition as instructed. The plan asks the school owner to record intended permitted uses; it does not introduce DRM, piracy checks, or claim legal clearance.

## Baseline scoring

**Raw evidence-readiness index: 41/100. Published baseline: min(41, 40) = 40/100. Target: 95/100.**

This is an audit-defined release-readiness index, not the design engine's separate surface-quality rubric. Each category receives 0-4: 0 absent/unproved outcome, 1 weak, 2 partial, 3 solid within tested scope, 4 independently accepted across required environments. Weighted contribution = weight x score / 4. Evidence gaps remain visible and block release regardless of this index.

| Dimension | Weight | Score /4 | Contribution | Evidence and constraint |
|---|---:|---:|---:|---|
| Purpose and scope discipline | 8 | 3 | 6.0 | Clear local-first PDF purpose; README and requirements still disagree about delivered scope |
| Visual hierarchy and layout | 14 | 1 | 3.5 | F01-F05; fresh default, large and minimum-size native captures |
| End-to-end UX and discoverability | 14 | 1 | 3.5 | F06-F08, F15; overlapping controls and disconnected settings/operations |
| PDF reading and study tools | 14 | 2 | 7.0 | Synthetic PDF renders and turns pages; F09-F12 and platform/manual gaps remain |
| Catalogue, identity and metadata | 12 | 2 | 6.0 | Canonical identity, read models, curation/writeback code; F13-F14/F23 and E2E gaps |
| Search and AI value | 10 | 1 | 2.5 | Implemented retrieval/evidence pipelines; F15-F18, no human relevance acceptance |
| Security, privacy and recovery | 10 | 2 | 5.0 | Fail-closed provider, worker/path and audit foundations; F17-F22, operational proof open |
| Cross-platform operation | 6 | 1 | 1.5 | Windows native inspection; macOS, signing, native accessibility and two-machine gates open |
| Engineering verification | 6 | 3 | 4.5 | Substantial executable test suite; see fresh results and specific blind spots in test report |
| Documentation and traceability | 6 | 1 | 1.5 | All 19 references reviewed; conflicting mappings/statuses in reference audit |
| **Total** | **100** | | **41.0** | **Published score capped at 40.0** |

Do not interpret the score as 40% of requirements implemented. Physical macOS, user research and production operations were not assessed. The score is intentionally conservative about evidence, while recognizing that the implementation contains much more than a prototype. Re-score only after outcomes change; reformatting the report earns no points.

## What is worth preserving

- The composition modules and inward dependency rules are a sound basis for a single desktop application. `Composition/ReaderModule.cs` wires isolated rendering, progress, cache, bookmarks, annotations and portability through interfaces.
- Catalogue identity is more developed than the README suggests: canonical identity repositories, occurrence/asset/work distinctions, durable jobs, source precedence and derived-asset services should be retained.
- The app exposes immediate startup feedback and a degraded recovery surface (`App.axaml.cs`, `StartupShellViewModel`, `DesktopShellWindow`). Startup work is moved off the initial UI frame.
- Reader persistence, annotation layers, citation generation, import safety and cache behavior have executable coverage. The synthetic native test reached page 2 of 3; do not call the entire PDF viewer nonfunctional.
- `AiGateway` checks provider identity, tier, preview, consent and local-only declarations before dispatch. `AiPayloadBuilder` rejects content chunks in metadata-only requests. These are valuable boundaries.
- `LocalEvidenceAnswerPipeline` explicitly describes its response as local excerpts, labels evidence source and returns no-evidence text. Preserve this honesty; relevance scores still need calibration.
- Explicitly unresolved physical and release gates in the execution ledger are useful. The error is treating other completion labels as stronger evidence than those limitations.
- Public Sans, Spectral and JetBrains Mono roles, local font files, licenses, theme resources and focus styles already exist. Repair actual rendering and component use before replacing the brand indiscriminately.

## Findings register

P0 means a current critical-path or release-gate blocker. P1 means substantial functional or experience debt. P2 is controlled polish. A source-only risk is distinguished from an observed failure. Phase numbers below refer to the new 21-phase programme; skill keys resolve to exact paths in [the routing register](06-skills-sources-and-kaizen.md).

| ID | Priority / evidence | Finding, consequence and source | Remediation / acceptance | Phase; skills |
|---|---|---|---|---|
| F01 | P0; native + source + owner | Command palette appears without invocation and remains after Escape or executing Return to library. `DesktopShellWindow.axaml` binds `MainShell.IsCommandPaletteOpen` on a Border whose DataContext is already MainShell. No visible close affordance. Blocks reading and first use. | Closed on launch, visibly dismissible, Escape/outside click/command completion dismiss; focus restored; optional power-user feature only. Test rendered visibility, not just VM boolean. | 02; E1,D1,D2 |
| F02 | P0; native + source + owner | Empty right inspector remains visible and its Close action does not remove it. `CatalogueShellView.axaml:687` changes DataContext to BookDetail then binds BookDetail.IsVisible again. A 320px inspector consumes space without a selected book. | Single visibility owner; selection-only presentation; close restores catalogue width; overhaul specified in phase 07. | 02,07; E1,D1,D2 |
| F03 | P0; native + source | Fourteen-column toolbar plus a star-sized view toggle overlaps. `CatalogueShellView.axaml:116`; default UIA grid starts x606 while Search starts x614. Layout still collides in 1600px capture. | Adaptive task hierarchy, minimum-width contracts, overflow menu and real selected-view states; zero overlap at all tested sizes. | 03,19; D1,D3,E1 |
| F04 | P1; native + source | Collection create/delete/rename are permanently displayed in a 220px sidebar and overflow it. Empty collection CRUD outranks reading. | Collection list first; contextual creation/menu; destructive action secondary; long names and translations fit. | 03,06; D2,D3 |
| F05 | P1; native + source | Light-theme control text is extremely thin and faint; display tabs are oversized relative to body. The body uses bundled variable Public Sans; actual weight selection is unverified. Token-name tests cannot detect this. | Verify resolved face/weight and licenses; test static weights as a bounded experiment if necessary; readable enabled/disabled states in both themes and scaling. Do not claim a measured contrast violation from this image alone. | 04,19; D1,E1 |
| F06 | P1; source + native | Standalone shell displays AI Smart Search even without a classroom connection because visibility tests only whether its VM exists (`MainShellViewModel:275`, unconditional construction in `ShellModule`). Sharing button is present when its VM is null and method returns. | Mode/capability-aware destinations with meaningful unavailable states; no inert routes. | 03,17,18; E1,D2 |
| F07 | P1; source | General settings/help/privacy/operations are not a coherent reachable shell. `SettingsLabel` exists without a settings route. PrivacyCenter and ActivityCentre views exist; `ShellModule` and active shell do not mount them. | Route inventory from menus to view/service; settings, privacy, help and processing actions reachable by pointer and keyboard. | 03,11,13; E1,D2 |
| F08 | P1; source + native | AI is correctly disabled in DI, but Advisor presents a ready state and Recommend action. Provider-profile services exist without a demonstrated ordinary-user activation journey. | Explain unavailable capability with local fallback and settings action; show provider/model/privacy status; no false ready language. | 13,14; E3,D2 |
| F09 | P0 for reader acceptance; source | `ReaderView.axaml`/code-behind expose navigation, zoom and annotation tools but no visible layout-mode or full-screen controls; no in-document search UI was found. Ctrl+F is handled by the catalogue shell. SRS FR-READ-004/005/006 claim these outcomes. | Complete reader modes, full-screen and scoped document search, verified through actual controls and persistence. | 08; E1,E4 |
| F10 | P1; native + source | Reader retains library toolbar/sidebar/inspector and adds duplicate navigation rows plus an annotation panel. At minimum window size virtually no useful page remains visible. | Reading workspace allocates space to the PDF; collapsible study rail; one navigation/zoom row; advanced actions secondary. | 08,09; D1,D3 |
| F11 | P1; source risk | `RenderCurrentPageAsync` catches generic render exceptions and leaves PageImage unchanged (`ReaderViewModel:1817`). A page turn followed by render failure can leave stale content or a placeholder without a useful diagnosis. | Explicit page-loading/error state, retry and stale-image handling tied to book/page/render generation; injected failure proves no mislabeled stale page. | 08,20; E1,E4 |
| F12 | P1; source + test gap | Annotation and citation logic is rich, but synthetic service tests do not establish selection geometry, clipboard, rotated/scanned pages, screen-reader reading or crash durability on both OSes. | Real PDF fixture matrix; select, annotate, reopen, export/import and verify coordinates/text without altering originals. | 09,19,20; E1,E4 |
| F13 | P1; source | Grid uses ListBox with ordinary WrapPanel and 160x230 fixed cards; badges compete with title/author (`CatalogueGridView.axaml`). Paging bounds results, so this is not evidence of rendering 50k cards at once. | Measure current page realization first; responsive cover grid/list with readable badges and maintained focus; add virtualization only where measured needed. | 06; E1,D1 |
| F14 | P1; native + source | Inspector gives seven wrapped tab headings and a large blank cover precedence over title/read context. File is first, showing empty size and unavailable-file copy when nothing was selected. | Reading-first summary, structured metadata and progressive disclosure; different no-selection/missing-file states; sticky primary read/resume action. | 07; D1,D2,D3 |
| F15 | P1; source + evidence gap | Metadata filtering, semantic/global search, student AI search, Advisor and Index Manager have overlapping entry points. Local evidence answers are excerpt aggregation, not established generative question answering. | One library search entry, separate in-book find; explicit retrieval scope/match location; contextual AI actions; honest extractive labels. | 03,11,12,14; E2,E3,D2 |
| F16 | P0 before AI-quality claims; evidence gap | Existing synthetic retrieval and structural answer tests cannot demonstrate educational relevance, supported answers, calibrated confidence or useful reading plans. Execution ledger phases 25-30 leaves reference/human evaluation open. | Versioned representative corpus, independently labeled holdout, relevance/citation/abstention measures and subgroup results; no tuning on holdout. | 12,14,21; E2,E3 |
| F17 | P0 before paid AI; source | `AiCostCalculator:29-33` returns zero cost for an unknown price when usage exists; production DI supplies no populated price collection. Budget reservation checks spent cost, not prospective/reserved cost. Concurrent or expensive next calls can exceed a promised hard monetary limit. | Unknown != free; effective-dated price table; conservative cost reservation and actual reconciliation; test concurrency, stale prices, missing usage and restart. Current paid path is disabled by default, so live overspend was not observed. | 13,18; E3,F1 |
| F18 | P1; source risk | `AiGateway` remembers preview once for the session via a boolean. Later payload/provider/tier/scope changes do not inherently invalidate that flag. Consent still runs separately. | Define preview-memory scope and invalidation; test changed payload and consent; make policy understandable. Do not label this a proven data leak. | 13; E3,E5 |
| F19 | P0 for classroom contract; source | `ClassroomBookFileMaterializer` fetches resource.Content, hashes it and writes all bytes to `classroom/files`. ReaderModule uses it through ClassroomBookFileLocator. Conflicts with FR-CLIENT-004 no complete local copy for reading. | Choose actual range-backed reading or formally revise storage/retention promise; verify disk, network, memory, profile access and erasure. | 17; E1,E5 |
| F20 | P0 before institutional release; source risk / NOT ASSESSED | Materialized files are keyed by host/resource, not profile; cleanup on revocation/logout and shared-device access must be proved. School-key custody, host publication and sync exist but physical two-user/two-machine proof is open. | Explicit cache threat model and permitted offline retention; revoke/unpublish/erasure and cross-profile tests including derived files. | 16-18,20; E5,E4 |
| F21 | P0 release gate; NOT ASSESSED | Worker isolation, hostile PDF, backup, signing, clean install and rollback need actual target-platform verification; functional Windows tests do not certify containment or installers. | Independent security review and Windows/macOS fault drills on the release artifact; evidence-bound sign-off. | 20,21; E4,E5,E6 |
| F22 | P0 release gate; native/source/NOT ASSESSED | Overlaid content impairs keyboard work; most shortcut handlers check Control explicitly. macOS command-key parity and native accessibility are unproved. | Accessible name/role/value, focus order/restore, keyboard-only journeys, Narrator and VoiceOver, OS-appropriate shortcuts. | 19; D4,E1 |
| F23 | P1; observed, cause unconfirmed | Native status said Scanned 12 books while catalogue showed 3. `StatusText` formats `_filesCompleted`; processing may count stages rather than unique books. | Define discovered/imported/processed/failed units, reconcile status against records; retain this as a measurement mismatch until traced. | 05,11; E4,D2 |
| F24 | P1; source | README describes obsolete project paths, phase status, DM Sans/Core architecture and network SQLite placement; current repository has different modules and local-authoritative design. | One current quickstart/status/supported-platform contract; put historical promises in history; verify commands in a clean checkout. | 01,21; E7,K1 |
| F25 | P0 governance gate; document inspection | Aug-39 requirement-phase mappings and refreshed DOCX status/versions disagree. A refreshed appendix does not reconcile old body statements. See detailed reference audit. | Correct semantic mapping, distinguish obligation/code/test/native/owner status, ensure all 162 IDs have real outcome evidence owners. | 01,21; E7,K1 |
| F26 | P1; source + test gap | Several UI tests assert screenshot existence/dimensions and VM state rather than containment, visibility, dismissal or usable page area. `ReaderViewRenderTests:43-90` is a concrete example. | Whole-shell real-composition tests, binding-error gate, geometry and accessibility assertions, reviewed native captures. | 02,19,21; E4,D1 |
| F27 | P1; source + NOT ASSESSED | 3D has a native adapter, bridge and TypeScript implementation; environment flag gates it and physical WebView/GPU recovery remains open. A dim emoji button does not explain that contract. | Optional, clearly labeled 3D mode with equivalent 2D route; benchmark actual native hosts, reduced motion and failure recovery. | 15; E1,D4 |
| F28 | P1; source + partial test evidence | OCR fixture README explicitly uses a deterministic test renderer. Blank native rendering of that tiny fixture is not proof of OCR failure or success. Fresh tests exercised the Windows packaged Tesseract path with generated scanned content; representative mixed/scanned corpus and macOS native acceptance remain open. | Genuine scanned/raster corpus, native Tesseract assets and language checksums; accuracy, resource, cancellation and searchable-page evidence on both OSes. | 11,20; E4 |
| F29 | P1; native + source | Header controls mix emoji/glyphs and SVGs; primary Read competes with colored Enrich/OCR, while AI and add-file buttons have equal emphasis. State does not consistently match color. | Licensed icon set, selected-view indicators, primary/secondary/danger semantics and tooltip/name parity; no font-glyph navigation. | 03,04,07; D1,D2 |
| F30 | P1; source risk | `AiUsageBudgetStore.Load` returns null on corrupt/unreadable state; normalization starts zero usage. Persist failures retain an in-memory gate only, and reservations survive restart without matching in-memory IDs. | Distinguish first use from corruption, recover durable reservations, fail safely on unknown paid budget, surface recovery to admin. | 13,20; F1,E4 |

## Architecture and functional seams

### Import to reading

Trace: folder/direct-file action -> MainShellViewModel -> ingestion/direct-open services -> catalogue identity and jobs -> isolated renderer -> ReaderSessionService -> ReaderViewModel -> ReaderView -> progress/annotation repositories. Native execution proved a synthetic three-page PDF opens and advances to page 2. It did not prove representative performance, all PDF variants, source-file crash safety or useful study interactions while overlays obscure the page.

The plan should preserve canonical identity and processing leases. UI restructuring must not create a second book store, move originals implicitly, or make AI a dependency of opening a file. Test renamed/missing roots, password refusal, cancellation, corrupt PDF, read-only source and duplicate import before closing this seam.

### Search to evidence

Trace: SearchViewModel -> ISemanticSearchService and underlying lexical/hybrid adapters -> result with book/page/source -> reader navigation; Advisor adds candidate selection and either provider calls or local excerpt answers. The crucial acceptance question is whether the returned page supports the user's question, not whether a result object has a citation field.

Current answer confidence is derived from ranking values (or fallback constants) in LocalEvidenceAnswerPipeline.ToCitation. It must be presented as a relevance signal until calibrated against human labels; it is not a probability of factual correctness. Keep page, note, description and TOC evidence visibly distinct. Do not imply reader notes are statements from the book.

### AI configuration to charge

Trace: provider profile -> selected runtime provider -> privacy tier -> exact preview -> consent -> budget reservation -> provider completion -> usage/cost/history/audit -> erasure. Source supports substantial boundary logic but not a complete user-reachable configuration journey. Unknown-price behavior and budget recovery require explicit correction before paid school use. Framework: not applicable to statutory reporting; this is operational usage estimation and spending control, not a new accounting module.

### School publication to private study

Trace: host opt-in -> published scope -> TLS/trust/enrollment -> authenticated catalogue/asset/PDF access -> local renderer/cache -> private profile notes -> offline/reconnect/sync -> revocation/erasure. The materialization mismatch changes the privacy and storage promise. Resolve it before claiming streamed/no-copy reading, and distinguish access revocation from deletion of previously permitted offline material.

## Release-blocking gaps

The initial audit run passed 1,160 automated tests (955 core, 42 architecture, 163 UI), locked restore, Release build, analyzer checks and the shelf3d gates. Its requirement-accountability invocation failed only because the old non-refreshed SRS path was absent; phase 01 corrected that input contract and the rerun now passes all 162 IDs. Format verification still fails on existing diagnostics. See the [exact commands and logs](04-test-report.md). Resolve the formatting gate in a separately reviewed phase 20 change; neither failure was hidden by substituting a document or reformatting application source during the audit.

Windows observation does not establish macOS behavior. Hosted historical CI does not establish fresh signed installers. No learner/librarian usability cohort was recruited in this audit; Peter's feedback is real qualitative evidence, not a statistical sample. No cloud calls, real student data or paid-provider acceptance was exercised. Native screen readers, large real-world collections, reference hardware, filesystem fault injection, independent penetration testing and institutional privacy sign-off remain NOT ASSESSED.

These limitations do not prevent a useful audit; they define the remaining work. Each has an owner, test and closure condition in the plan. No release claim may conceal them behind the 40-point baseline or a future 95-point average.

## Recommended programme

Execute the [21 phases](05-remediation-master-plan.md) as bounded, testable changes. Restore visibility and dismissal before redesigning navigation. Complete the reading experience before expanding AI presentation. Validate school privacy and file-materialization behavior before classroom rollout. Reassess the score only against preserved baseline journeys and actual release artifacts.
