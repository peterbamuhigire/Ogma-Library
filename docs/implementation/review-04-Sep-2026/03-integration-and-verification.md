# Integration and Verification Audit

## Verified local slices

| Area | Result |
| --- | --- |
| Requirement accountability | 162/162 assigned |
| Phase 11 real PDF corpus/pipeline | 7 PDFs, 3,326 pages, 0 file errors; 7/7 pipeline books and 5,096 chunks |
| Phase 13 provider evidence | Official pages verified 2026-09-04; legal/network still open |
| Phase 17 lease/runtime and stage workers | 16 focused tests passed |
| Phase 17 restart/recovery load | 8 focused tests passed; 64-job drain |
| Phase 27 local privacy journey | 13 focused tests passed |
| Phase 30 feedback/quality/privacy | 14 focused tests passed |
| Phase 31 bridge/3D/fallback | 24 focused tests passed; JavaScript syntax passed |
| Phase 32 Shelf3D provenance/publisher | 28 focused tests passed; TypeScript/build/bundle syntax passed; tamper rejection passed |
| Phase 34 LAN/security filter | 61 tests passed |
| Phase 35 classroom client | 107 tests passed |
| Phase 36 school admin | 46 tests passed |
| Phase 37 security filter | 34 tests passed |
| Phase 38 migration/update trust | 12 tests passed |
| Combined Phase 16/34/36/37 regression slice | 147 tests passed |
| Whole-repository format | Known baseline failures unrelated to these increments; do not claim clean |
| Whole Release build/test | Can be affected by active app/worker output locks; isolated slices are the reliable evidence |
| Full Debug discovery suite | Historically aborts at the 50k discovery test; not claimed green |

## Integration findings

1. The application composition resolves external metadata providers only when
   explicitly enabled, and AI remains fail-closed/offline by default.
2. The durable job runtime now has a single resource-capacity model for OCR,
   PDF, search, metadata, and embeddings. Search/embedding triggers are
   queue-backed with a compatibility stage poll for legacy rows.
3. Search, FTS, OCR, embeddings, and advisor paths preserve local fallback or
   explicit unavailable states rather than inventing successful results.
4. Classroom host projections enforce published scope before details/files,
   and private fields are redacted at the read boundary.
5. Release acceptance is intentionally separate from candidate packaging; an
   unsigned or uninstalled candidate cannot satisfy Phase 39.

## Verification limitations

- Headless tests cannot establish GPU frame budgets, native WebView attachment,
  real credential stores, physical accessibility, network firewall/mDNS
  behavior, or two-machine isolation.
- Synthetic 500-book PDF/OCR baselines demonstrate bounded local behavior but
  are not acceptance of the real mixed-quality corpus.
- Provider policy pages were verified live, but no archive snapshot or legal
  owner approval is present.
