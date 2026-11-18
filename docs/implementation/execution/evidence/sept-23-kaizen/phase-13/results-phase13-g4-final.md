# Golden journey results

Run `phase13-g4-final`, re-summarised against baseline.json (filter, sizes and exe are those of the original run).

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1280x800 | PASS |  | 93.7 | artifacts\e2e\phase13-g4-final\A11yNames\catalogue |
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1920x1080 | PASS |  | 60.6 | artifacts\e2e\phase13-g4-final\A11yNames\catalogue |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1280x800 | FAIL |  | 64.7 | artifacts\e2e\phase13-g4-final\A11yNames\search |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1920x1080 | PASS (baseline cleared) | K40, K41 (Phase 13) | 39.2 | artifacts\e2e\phase13-g4-final\A11yNames\search |
| G4 | G4_Search_ExpectedBookInTopThree | 1280x800 | PASS (baseline cleared) | K40, K41 (Phase 13) | 73.4 | artifacts\e2e\phase13-g4-final\G4 |
| G4 | G4_Search_ExpectedBookInTopThree | 1920x1080 | PASS (baseline cleared) | K40, K41 (Phase 13) | 188 | artifacts\e2e\phase13-g4-final\G4 |
| ScanOcrSearch | ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText | 1280x800 | FAIL |  | 35 | artifacts\e2e\phase13-g4-final\ScanOcrSearch |
| ScanOcrSearch | ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText | 1920x1080 | FAIL |  | 382.5 | artifacts\e2e\phase13-g4-final\ScanOcrSearch |

## New failures
- **ScanOcrSearch 1280x800** (ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText): TimeoutException: The main window did not appear within 30 s.
- **ScanOcrSearch 1920x1080** (ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText): TrueException: Scanned-book OCR journey: 'Scanned Handout' never showed the OCR text badge (seen: NoText) [K21]; search 'rareocrkeyword' did not list 'Scanned Handout' in the top three [K21]
- **A11yNames 1280x800** (SearchResults_HaveReadableAccessibleNames): COMException: Unexpected HRESULT has been returned from a call to a COM component.
