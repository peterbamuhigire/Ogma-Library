# Ogma Library current-source requirements and design update

Cycle date: 2026-09-10  
Scope: library, reader, PDF, identity, desktop/runtime, release, and document
refresh requirements. Visual inspection was explicitly excluded.

## Evidence-backed currentness register

| ID | Primary source | Version/date | Accessed | Freshness/review | Decision |
| --- | --- | --- | --- | --- | --- |
| DR-WCAG-22 | https://www.w3.org/TR/WCAG22/ | W3C Recommendation, 2024-12-12 | 2026-09-10 | standard; review 2026-10-10 | retain for reader, catalogue, keyboard, and assistive-technology requirements |
| DR-SSDF-11 | https://www.nist.gov/publications/secure-software-development-framework-ssdf-version-11-recommendations-mitigating-risk | v1.1, 2022-02-03; updated 2022-11-29 | 2026-09-10 | durable guidance; review 2027-09-10 | retain for build provenance, release evidence, and vulnerability response |
| DR-DOTNET-10 | https://learn.microsoft.com/en-us/dotnet/core/releases-and-support | .NET 10 LTS support page; support through 2028-11 | 2026-09-10 | time-sensitive; review 2026-10-10 | retain .NET 10 target and verify SDK/runtime in CI |
| DR-OPENAPI-311 | https://spec.openapis.org/oas/v3.1.1.html | 3.1.1, 2024-10-24; newer index entries exist | 2026-09-10 | contract standard; review 2026-10-10 | pin only where API boundary exists; do not infer newest version |
| DR-MODEL | https://openai.com/index/gpt-6-astra/ and https://openai.com/index/gpt-5-6/ | official release evidence accessed current page | 2026-09-10 | time-sensitive; review 2026-10-10 | retain pins; actual runtime/account catalogue NOT ASSESSED; local policy DRIFT remains |

## Requirements changes

1. Reader and catalogue requirements must specify PDF containment, page-count and
   navigation behaviour, interrupted-load recovery, malformed-document handling,
   search failure, library identity, and safe metadata boundaries.
2. Release requirements must name reproducible build inputs, signing/notarisation
   evidence, update/rollback behaviour, supported OS/runtime matrix, artifact
   hashes, and a release owner. Existing open criteria remain open until evidence
   is produced.
3. Accessibility requirements should use WCAG 2.2 test oracles for keyboard
   navigation, focus, names/roles/values, status, zoom/reflow, contrast, and
   error recovery. The visual/manual portion is NOT ASSESSED in this cycle.
4. If a service API is exposed, publish a pinned OpenAPI contract and validate
   generated examples against it; contract version selection remains a project
   decision because the official index lists newer revisions.

## Design and UX changes

- Preserve source document styles in regenerated DOCX files; no new typeface,
  colour system, or visual restyle is claimed without visual QA.
- Add explicit reader states for loading, empty library, unavailable document,
  malformed PDF, page overflow, search-no-results, permission denied, and
  interrupted/retry flows.
- Treat physical accessibility, 3D/shelf interactions, and platform-specific
  input as separate evidence obligations. Automated tests passing does not
  close those manual gates.
- Keep release and reader status language precise: a green automated test run
  is evidence for that command, not a production or platform-readiness claim.

## Verification after application

- Release-configuration .NET test baseline remains 42 architecture, 955 core,
  and 163 UI tests (1,160 total) from the current local evidence run.
- Shelf3D typecheck/build/performance budget were green in the baseline run.
- Open execution criteria, release/signing, PDF containment, physical/platform
  accessibility, and reviewer evidence remain open.
- Visual/browser/render checks: skipped, NOT ASSESSED; the initial test-generated
  PNGs were restored so the cycle did not retain visual-check artifacts.

