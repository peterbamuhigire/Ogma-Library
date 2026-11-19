# Phase 08 real-window evidence (Phase CO slice 5, 2026-11-19)

All runs used `Use-DesktopLock.ps1 -Owner phase-CO`, the E2E build of this worktree, a fresh
temporary `OGMA_LIBRARY_DATA_DIR` for every test and the synthetic corpus only.

| Run | Head | Journeys | Sizes x themes | Result |
|---|---|---|---|---|
| `co-nav-1` | `181e150b` + new journeys | G1, G4, G7, Navigation, Settings, A11yNames, ScanOcrSearch | 1280x800, 1920x1080 x Light, Dark | 52/52 PASS (ScanOcrSearch "PASS (baseline cleared)" x4) |
| `co-nav-2` | `1fc796ea` (K113 fix) | G4, Settings, ScanOcrSearch, A11yNames | same | 32/32 PASS |

## Settings journeys (each row: both sizes, both themes, both runs: PASS)

- J-SET-1 `Settings_ThemeDensityAndLanguage_ApplyLiveAndPersistAcrossRestart`.
- Environment override `Settings_EnvironmentOverride_IsShownDisabledWithTheReason`.
- J-SET-3 `Settings_SecondFolderAddedFromSettings_JoinsCatalogueAfterRescan` (new). Settings >
  Library > *Add folder…* opened the real native dialog (technique `keyboard+offset-click`;
  the Win32 route found no `cmb13` box), *second-shelf* was listed with its own Rescan control,
  *Rescan library* ran and the catalogue went from 13 to 14 books, including *Second Shelf Primer*.
- Every section `Settings_EverySection_OpensAndIsCaptured` (new): all eight sections open and
  show their title.

## Screenshots

`co-nav-2-<theme>-section-<id>-<size>.png`: library, appearance, language, online, features,
privacy, ocr and diagnostics, Light and Dark, at 1280x800 and 1920x1080 (32 files).
`co-nav-2-light-library-two-folders-1280x800.png` and `co-nav-2-light-catalogue-two-folders-1280x800.png`
show J-SET-3.

## K113 (found here)

`co-nav-1-dark-section-diagnostics-1920x1080-before-K113.png`: the capability status text was
`#2552A0` on the `#1B2440` card (2.03:1, MEASURED from pixels and tokens). After `1fc796ea`, the
same pixel in `co-nav-2` is `#9FBFF2` (8.2:1 by the WCAG formula).

## NOT ASSESSED

- J-SET-2 (mock provider endpoint): Phase 11 (G9); the harness has no mock provider endpoint.
- 200% text, `fr` and keyboard-only variants of the Settings journeys; screen reader.
- J-SET-3 through the Win32 picker route: the dialog had no `cmb13`, so the foreground
  keyboard route ran. It needs the foreground, which the desktop lock provides.
