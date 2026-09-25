# Ogma Library: product audit, analysis and verdict (Kaizen cycle 2026-09-23)

Audit date: 25 September 2026. Requested by Peter Bamuhigire. Audited commit `0ad3c0c` on `main`,
built from a clean HEAD worktree. The owner's working tree was also built as-is.
Scope: build, run and test every reachable corner; analysis, inventory and audit; remediation plan.
No product source in the repository was changed by this audit.

## 1. Verdict

**Ogma Library does not work as a product today, even though its engineering foundations are
substantial and its automated tests are green.**

Four measured faults explain most of what the owner experiences:

1. **The library is invisible.** Since 4 September (`822e760`), an opaque pager panel has been
   drawn over the whole catalogue area. New users see a blank screen with a lone "Page 1 of 1"
   bar. After a scan, "17 books" appears in the toolbar while the grid stays empty. With that one
   layout error corrected in a scratch copy, the full catalogue appears (compare
   [`j2-scanned.png`](evidence/screens/j2-scanned.png) with [`j3-grid.png`](evidence/screens/j3-grid.png)).
2. **Reading crashes the application.** About twenty page turns into a 120-page book, the
   whole app terminated with an unhandled `IOException`, after a UI freeze of 11.4 s. The reader's
   worker process is killed by a CPU limit designed for one-shot jobs. The next page turn makes a
   blocking call on the UI thread, and there is no global exception handler.
3. **Key actions and features cannot be reached.** At the default window size, *Choose library
   folder*, *Open PDF*, *Advisor* and *Reading plan* are off-screen. The Advisor always answers
   "AI features are disabled". The 3D shelf claims the device is unsupported. The classroom Host,
   online metadata and 3D can be enabled only with environment variables. There is no settings
   screen and no way to change language.
4. **The data shown is wrong or incomplete.** Generated covers are looked up in the wrong
   folder, so every book shows a placeholder. Empty, non-PDF and truncated files are listed as
   "Indexed" books. A duplicate is listed twice. Extracted ISBNs never reach the book record.
   Search misses phrases that are in the text. The status bar tells a user with 17 books to
   "choose a library folder to begin".

Underneath, a lot works: discovery handles Unicode and deep paths, text extraction and the FTS
index were filled correctly (1,302 pages, 1,432 chunks), the PDF renderer is isolated in a
worker process, AI fails closed, shutdown is clean, and the Release build has zero warnings and no
vulnerable packages. **The gap is integration, wiring and verification in the real window, not
missing engineering.**

The working tree also has a repository-level emergency: **both main test projects (210 files) are
deleted but uncommitted**, and the solution no longer builds (K01). That must be resolved before
any remediation starts (Phase 00, decision D-01).

## 2. Score

The published scoring follows the engines' Kaizen doctrine: `reported = min(raw, 65)`, 95 target,
`NOT ASSESSED` never counted as a pass. To stay comparable with the 14 September astra audit
(published 40/100), the same 10-dimension weighted index is used.

| Dimension | Weight | Score 0–4 | Points | Basis (evidence class) |
|---|---:|---:|---:|---|
| Purpose and scope fit | 8 | 3 | 6.0 | The product concept, requirements and local-first privacy stance are sound (CODE, HEURISTIC). |
| Visual design | 14 | 1 | 3.5 | Four button styles, off-palette selection, oversized wrapped tabs, placeholder covers, blurry pages (MEASURED). |
| UX and navigation | 14 | 0 | 0.0 | The catalogue and first-run call to action are invisible; primary actions are off-screen; panels stack (MEASURED). |
| Reading | 14 | 1 | 3.5 | The reader renders, then crashes the app after about 20 page turns with an 11.4 s freeze (MEASURED). |
| Catalogue and library | 12 | 1 | 3.0 | Scanning is fast and correct, but covers are missing, invalid files are books, and duplicates and ISBNs are unhandled (MEASURED). |
| Search and AI | 10 | 1 | 2.5 | Single-word search works; phrases, typos, Unicode and fields fail; the Advisor never works (MEASURED). |
| Security and privacy | 10 | 2 | 5.0 | Isolation and fail-closed AI are sound; the independent containment review, DPIA and single-instance DB safety are open (CODE). |
| Cross-platform | 6 | 1 | 1.5 | Windows only; macOS NOT ASSESSED; no installers (NOT ASSESSED). |
| Engineering quality | 6 | 2 | 3.0 | Clean build and a strong test count, but the tests are deleted in the working tree, the format gate fails, there is no logging and the tests are blind to the window (MEASURED). |
| Documentation and evidence | 6 | 1 | 1.5 | Large volume, but status claims contradicted the running product (HEURISTIC). |
| **Total** | **100** | | **29.5 → 30** | |

```
raw_diagnostic_score: 30
reported_audit_score: 30          # min(30, 65)
target_score: 95                  # conditional on evidence, not a claim
confidence: high for Windows standalone; low for macOS, classroom LAN and assistive technology
hard_gates: blocked (P0: K01, K05, K10, K11, K20, K30, K31, K50)
```

**Why the score is lower than the astra baseline (40):** this audit ran a real library through
the real window and turned pages until failure. The blank-catalogue defect (K10) predates the
astra audit but was not caught then, and the reading crash (K30) needs sustained use to appear.
Nothing has regressed since 14 September. There has been no product commit since then, and the
lower score reflects deeper measurement.

## 3. What was tested

The full commands, timings and results are in [02-test-report.md](02-test-report.md).

- **Gates on HEAD:** accountability, locked restore, Release build, vulnerable packages, format,
  analyzers, and all three test projects (1,161 tests).
- **The as-is working tree build**, which fails (K01).
- **The real application** on Windows 11 (2560×1440), driven through Windows UI Automation with no
  focus stealing ([`evidence/tools/Invoke-OgmaUia.ps1`](evidence/tools/Invoke-OgmaUia.ps1)), using
  isolated data directories and a synthetic 17-file library generated by
  [`evidence/tools/New-SyntheticCorpus.py`](evidence/tools/New-SyntheticCorpus.py). The library
  covers normal books with metadata, ISBNs and TOCs, a 900-page file, Unicode names, a deep path,
  a byte-identical duplicate, an encrypted PDF, a truncated PDF, an HTML file named `.pdf`, a
  0-byte file and image-only scans.
- **Journeys:** first run, folder choice through the native picker, scan and ingest, catalogue in
  grid, list and directory views, selection and detail, reader and a page-turn stress test, a
  search battery of 11 queries, 3D shelf, AI Smart Search, Advisor, Reading plan, Index Manager and
  Activity Centre, filter, sharing, relocation reviews, split view, window sizes from the 860 px minimum to
  2200 px, repeated cold starts and graceful shutdown.
- **Post-run inspection** of the catalogue database (books, files, metadata fields, ISBN evidence,
  jobs) and of the files the app wrote.
- **Static analysis** by four parallel audits (inventory and requirements, code-level journey
  tracing, prior-plan reconciliation, skills-engine doctrine), spot-checked against the running
  product. One agent claim was corrected: derived assets are written under `<library>/.ogma`, not
  the data folder.

## 4. Analysis: why the product drifted

1. **Proof moved away from the product.** The tests exercise view models and headless controls.
   None checks that content is visibly painted and unobstructed in the composed window. A
   one-line XAML nesting error therefore hid the entire library for three weeks while 1,161 tests
   passed and phase records reported catalogue work as delivered.
2. **Features were built behind gates that were never opened.** The AI Privacy Center, provider
   profiles, bulk edit, smart shelves, edition merge/split, batch enrichment, library health,
   in-document search, password unlock, publishing and session management exist in the backend
   with no UI path (20 backend-only FRs). Environment flags stood in for settings.
3. **Integration seams were untested.** Root handling (the chosen folder vs the startup option),
   worker resource limits (one-shot vs persistent sessions) and progress counters (scan vs jobs)
   each work in isolation and fail when combined.
4. **Async and error discipline is inconsistent.** `async void` handlers, fire-and-forget tasks,
   off-thread view-model mutation and no logging mean failures either crash the app or vanish.
5. **Plans multiplied faster than fixes.** Four overlapping plans, 238 evidence files and
   phase-mapping of small commits produced confident status tables that the running app
   contradicts ([06-prior-plans-reconciliation.md](06-prior-plans-reconciliation.md)).

## 5. Inventory in one paragraph

Seven .NET projects (about 90k lines of hand-written C# plus a 29k-line bundled 3D script), a
TypeScript 3D source, 68 SQLite tables, and 1,161 tests. Of 101 functional requirements, 47 are
wired to the UI (21 of those only with an environment flag), 28 are partial, 20 are backend-only
and 6 are missing. The full inventory, runtime bindings and the per-FR table are in
[04-inventory-and-requirements.md](04-inventory-and-requirements.md).

## 6. Remediation

Twenty-nine phases in seven waves are defined in [08-master-plan.md](08-master-plan.md), with
detailed documents in [`phases/`](phases/). Wave A (phases 00–04) removes every measured P0 and
should be complete before any new feature work. The first measurable owner-visible outcome is at the
end of Phase 03: the library appears, and the first-run and status texts are truthful.

Twelve owner decisions (D-01..D-12) are listed in the master plan. **D-01 (the deleted test
projects) and D-02 (plan authority) are needed before Phase 00 can start.**
