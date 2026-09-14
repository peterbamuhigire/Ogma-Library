# Kaizen remediation execution record — 2026-09-14

This record covers the first implementation tranche following the September
audit. It is evidence of completed changes and tests, not a release approval.

## Commits and phase mapping

| Phase | Commit | Delivered |
|---:|---|---|
| 01 | `57fd674` | Accountability gate defaults to the refreshed canonical SRS; 101 FRs, 29 NFRs and 32 controls validate. |
| 02 | `157e49f` | Window-level palette binding repaired; explicit close control; inspector visibility binding repaired. |
| 03 | `036c95a` | Toolbar gets horizontal overflow handling so narrow windows do not overlap controls. |
| 04 | `287aee9` | Catalogue controls use readable labels instead of ambiguous glyph-only presentation. |
| 05 | `6bb2fad` | Scan completion status correctly says files, matching the progress service's unit. |
| 06 | `61fd522` | Grid book cards expose author context to assistive technology. |
| 07 | `bbb1ae0` | Right inspector widened, title wraps, cover is compacted, and primary Read action is surfaced in the header. |
| 08 | `5a13d0e` | Reader render failures clear stale imagery and expose localized retry state. |
| 09 | `bd1db17` | Reader study-action status is announced politely to assistive technology. |
| 10 | `ad09020` | Metadata write-back errors no longer expose raw exception/path details in the panel. |
| 11 | `77a4fd0` | Search result rows receive an accessible name. |
| 12 | `d402afc` | Search scores are presented as relevance signals, not truth confidence. |
| 13 | `a5e56c7` | Unknown AI model pricing remains nullable; a missing price cannot display as free. |
| 14 | `3d515c9` | Advisor status changes are announced politely. |
| 15 | `521f46f` | 3D fallback state is announced and its fallback list has single-selection semantics. |
| 16 | `6ab3ebc` | Classroom host/client connection status is announced. |
| 17 | `fa9bb69` | Offline-cache status changes are announced. |
| 18 | `b8336cd` | School administration and managed-AI status changes are announced. |
| 19 | `e7d83ab` | Ctrl/Cmd modifier parity added for shell, reader and palette shortcuts. |

Phases 20–21 remain acceptance/documentation gates in this tranche. Their
release work is recorded below and does not imply signing, notarization or
cross-platform completion.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`: PASS, 0 warnings, 0 errors.
- `dotnet test OgmaLibrary.sln --configuration Release --no-build --verbosity minimal -m:1`: PASS, 1,161 tests (42 architecture, 956 core, 163 UI), 0 failed, 0 skipped.
- Focused catalogue, reader, search, AI and scan suites: PASS.
- `npm run typecheck` in `src/shelf3d`: PASS.
- Native Windows UI Automation: explicit palette close control found and invocation removed the palette; fresh startup no longer displayed it by default.
- `git diff --check`: PASS for tracked changes at the audit baseline. Generated developer-guide screenshot output was restored and is not part of this tranche.

## Unresolved gates and bugs

The following remain open and are deliberately not converted to passes:

- The toolbar uses horizontal overflow at narrow widths; the full information architecture/overflow-menu redesign remains phase 03 follow-up work.
- In-document reader find, full-screen modes and complete reader workspace restructuring remain phase 08 work.
- macOS physical execution, native accessibility (VoiceOver/Narrator), signed installers, notarization, Gatekeeper and reference-machine performance remain unassessed.
- Classroom materialization still requires the phase 17 no-full-copy/storage-contract decision.
- Live provider terms, credentials, network behavior and school-controller approval were not exercised.
- Repository-wide `dotnet format --verify-no-changes` remains a known existing failure; no broad formatter rewrite was authorized.

No production remediation is claimed for these items. Re-run the phase 21
independent acceptance matrix after phases 20 and 21 close their evidence.
