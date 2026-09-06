# Cross-platform CI regression evidence

Date: 2026-09-06

Commit: `bb277254eb0b93de1247c9762fdc5efdaecd2991`

Workflow: [CI run 34001078271](https://github.com/peterbamuhigire/Ogma-Library/actions/runs/34001078271)

## Result

The Windows and macOS matrix jobs completed successfully. Both jobs passed the
requirement-accountability gate, locked restore, repository format check,
warnings-as-errors build, dependency vulnerability check, analyzer scan, 3D
source/performance budget, secret scan, and the full test matrix.

| Platform | Architecture tests | Core tests | UI tests | Result |
| --- | ---: | ---: | ---: | --- |
| Windows runner | 41 | 925 | 159 | PASS |
| macOS runner | 41 | 925 | 159 | PASS |

The run therefore provides repository-level cross-platform evidence for 1,125
tests per platform. It does not close physical reference-machine, signing,
installer, accessibility, or owner-acceptance gates.

## Explicit capability limits

The macOS run emitted two deliberate `NOT ASSESSED` diagnostics rather than
claiming unsupported capability evidence:

- the Tesseract 5.2.0 package does not provide a supported macOS/Linux native
  runtime for the packaged OCR acceptance fixture;
- the hosted macOS runtime did not expose the worker private-resource counters.

Those limitations remain open in the owning phase records.
