# The 12-phase PDF Kaizen roadmap

## Sequence

```text
1 Scope/profile
   ↓
2 Boundary/containment → 3 Engine/telemetry → 4 Document identity/model
                                      ↓
5 Geometry/render contract → 6 Responsive reader
                                      ↓
7 Text semantics → 8 Navigation/interchange → 9 Annotations/forms/actions
                                      ↓
10 Assets/OCR/search/AI provenance → 11 Platform/performance/a11y
                                      ↓
12 Release profile, CI and re-audit
```

Phases 2–4 are trust and data-model prerequisites. Phases 5–9 make the reader
technically faithful. Phase 10 protects every downstream consumer. Phase 11
proves the experience in the real supported environments. Phase 12 turns the
result into a maintained standard rather than a one-off clean-up.

## Phase summary

| # | Outcome | Existing phase anchors | Exit signal |
|---:|---|---|---|
| 1 | Versioned supported/degraded/refused capability profile | 1, 10, 11, 37–39 | Profile and corpus register approved |
| 2 | One safe input/parser/render boundary | 5, 10, 17, 37 | No direct production PDF opens; OS proof begins |
| 3 | Pinned engine/errata compatibility evidence | 1, 10, 11, 38 | Version matrix and regression results published |
| 4 | Stable document/page/object identity model | 3–8, 10–11 | Snapshot/xref/page-tree diagnostics are durable |
| 5 | Effective page geometry and rendering contract | 11, 16, 21 | Geometry/rotation/overlay corpus passes |
| 6 | Responsive preview, cache, scroll and zoom pipeline | 17–21, 38 | Measured user budgets pass on reference hardware |
| 7 | Faithful text/Unicode/reading-order pipeline | 11, 23–24 | Corpus quality and search/copy gates pass |
| 8 | Complete safe navigation/interchange surface | 11, 20–23 | Destinations/labels/links degrade predictably |
| 9 | Explicit annotations/forms/signatures/active-content policy | 15, 21, 37 | Safe policy and fixtures pass; no silent execution |
| 10 | Versioned derived assets and evidence chain | 12–16, 23–30 | OCR/FTS/AI citations retain page/provenance |
| 11 | Physical, accessibility and cross-platform proof | 18–21, 37–39 | Windows/macOS acceptance records complete |
| 12 | Release profile, CI gates and re-audit | 1, 17, 37–39 | Conformance statement, rollback and owner sign-off |

## Delivery rule

Do not run all twelve as parallel feature work. The owner may parallelise
independent corpus preparation, documentation and test-fixture work, but the
contracts and gates must land in order. A later phase cannot close a failure
caused by an earlier boundary violation through UI polish.
