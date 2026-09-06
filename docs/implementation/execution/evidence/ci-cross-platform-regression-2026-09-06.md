# Cross-platform CI regression evidence

Date: 2026-09-06

Commit: `75effc78c44350de79e107ce53f2da9955dc6fcf`

Workflow: [CI run 34020988156](https://github.com/peterbamuhigire/Ogma-Library/actions/runs/34020988156)

## Result

The Windows and macOS matrix jobs completed successfully. Both jobs passed the
requirement-accountability gate, locked restore, repository format check,
warnings-as-errors build, dependency vulnerability check, analyzer scan, 3D
source/performance budget, secret scan, and the full test matrix.

| Platform | Architecture tests | Core tests | UI tests | Result |
| --- | ---: | ---: | ---: | --- |
| Windows runner | 41 | 938 | 159 | PASS |
| macOS runner | 41 | 938 | 159 | PASS |

The run therefore provides repository-level cross-platform evidence for 1,138
tests per platform. It does not close physical reference-machine, signing,
installer, accessibility, or owner-acceptance gates.

The 938-test core run includes the concurrency-safe classroom profile-key
initialization, atomic writeback promotion, safe generic batch-pause semantics,
transactional school AI-history purge audit, explicit local embedding
token/zero-egress/zero-external-cost accounting, frozen v1 search contract,
executable beta schema-sequence freeze, and release-acceptance contract
pass/fail execution. The validator's later evidence-digest binding is not part
of this run and remains owned by its successor CI run.

The Phase 16 production-worker disk benchmark also reproduced the same encoded
maximum on both runners: 78,274 bytes per book and a 3.645-GiB worst-sample
projection at 50,000 books. The synthetic hostile-PDF boundary regression was
included in the core suite on both platforms. The earlier authoritative run for
that 930-test baseline was CI run 34012939882 at commit `831ed9b`.

## Explicit capability limits

The macOS run emitted two deliberate `NOT ASSESSED` diagnostics rather than
claiming unsupported capability evidence:

- the Tesseract 5.2.0 package does not provide a supported macOS/Linux native
  runtime for the packaged OCR acceptance fixture;
- the hosted macOS runtime did not expose the worker private-resource counters.

Those limitations remain open in the owning phase records.
