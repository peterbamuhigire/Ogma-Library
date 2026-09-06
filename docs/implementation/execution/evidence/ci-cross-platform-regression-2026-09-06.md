# Cross-platform CI regression evidence

Date: 2026-09-06

Commit: `831ed9b125e7682ef12c56175416da94ae55c578`

Workflow: [CI run 34012939882](https://github.com/peterbamuhigire/Ogma-Library/actions/runs/34012939882)

## Result

The Windows and macOS matrix jobs completed successfully. Both jobs passed the
requirement-accountability gate, locked restore, repository format check,
warnings-as-errors build, dependency vulnerability check, analyzer scan, 3D
source/performance budget, secret scan, and the full test matrix.

| Platform | Architecture tests | Core tests | UI tests | Result |
| --- | ---: | ---: | ---: | --- |
| Windows runner | 41 | 930 | 159 | PASS |
| macOS runner | 41 | 930 | 159 | PASS |

The run therefore provides repository-level cross-platform evidence for 1,130
tests per platform. It does not close physical reference-machine, signing,
installer, accessibility, or owner-acceptance gates.

The Phase 16 production-worker disk benchmark also reproduced the same encoded
maximum on both runners: 78,274 bytes per book and a 3.645-GiB worst-sample
projection at 50,000 books. The synthetic hostile-PDF boundary regression was
included in the 930-test core suite on both platforms.

## Explicit capability limits

The macOS run emitted two deliberate `NOT ASSESSED` diagnostics rather than
claiming unsupported capability evidence:

- the Tesseract 5.2.0 package does not provide a supported macOS/Linux native
  runtime for the packaged OCR acceptance fixture;
- the hosted macOS runtime did not expose the worker private-resource counters.

Those limitations remain open in the owning phase records.
