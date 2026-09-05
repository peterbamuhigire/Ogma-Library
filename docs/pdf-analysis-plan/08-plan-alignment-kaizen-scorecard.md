# Plan alignment and Kaizen scorecard

## Alignment with the authoritative 39 phases

| PDF programme need | Existing 39-phase coverage | Alignment judgement |
|---|---|---|
| Source identity and root safety | 3–8 | Strong; add content snapshot semantics |
| PDF validation/containment | 10, 17, 37 | Strong intent; real OS proof and direct-bypass gate missing |
| Extraction/text/TOC | 11, 23, 24 | Good lifecycle; weak PDF feature capability and semantic quality matrix |
| Metadata/provenance/write-back | 12–15 | Good safety intent; writer conformance/signature handling needs definition |
| Cover/thumb/spine | 16 | Good manifest direction; embedded thumb and full fallback still open |
| Reader | 18–21 | Good UX foundation; actual geometry/continuous flow/destination support open |
| Search/AI grounding | 22–30 | Strong derived-data governance; PDF uncertainty must propagate end-to-end |
| 3D/classroom | 31–36 | Adjacent consumers; not PDF compliance, but must share safe read model |
| Security/performance/release | 37–39 | Correct final gates; need PDF-specific corpus and conformance report |

## Current evidence versus plan language

The execution ledger records 1,071 passing tests (885 core, 41 architecture,
145 UI) and multiple PDF/reader evidence slices. That establishes build and
automated behavior for the tested paths. It does not close the documented open
gates: real Windows/macOS sandbox evidence, reference hardware, real mixed-PDF
accuracy/resource behavior, physical accessibility, signing/notarization,
backup/restore and independent security approval.

The plan also says all parse/render/OCR operations use the Phase 10 broker and
platform sandbox. The direct PdfPig/PDFsharp callers identified in the code scan
are therefore a plan-to-code mismatch that should become an architecture-test
blocker.

## Kaizen operating model

Each phase runs the same loop:

1. **Observe:** capture a real failure, latency, mismatch or missing capability.
2. **Baseline:** record fixture hash, build, OS/hardware, dependency versions,
   duration, memory/CPU and current user impact.
3. **Select:** choose one bounded change and one measurable hypothesis.
4. **Experiment:** implement behind a contract/feature flag where risk is high.
5. **Check:** run unit, corpus, security, UX and physical tests appropriate to
   the change.
6. **Standardise:** update contracts, architecture tests, docs, profile and CI.
7. **Teach:** add a runbook and failure-recovery guidance.
8. **Re-measure:** compare to baseline and schedule the next review.

## Gate policy

- A phase is not complete because code exists; it is complete when its evidence
  and owner acceptance are recorded.
- `NOT_ASSESSED` is never silently promoted to pass.
- A feature that cannot be rendered safely is either degraded visibly or
  refused; blank output is a failure.
- Performance budgets include open, first visual, cached page turn, scroll,
  zoom, memory and failure recovery—not just a synthetic render call.
- Release claims name the supported profile and engine versions.

## One immediate experiment

**Hypothesis:** a single worker-owned document context with a content snapshot,
warm session and page geometry cache will reduce first-to-next-page latency and
eliminate geometry drift compared with repeated direct PdfPig/PDFium opens.

**Measure:** p50/p95 open-to-first-preview, cached next-page, uncached next-page,
middle-page wheel frame continuity, allocations, peak RSS, and geometry/overlay
alignment across 20 real PDFs. Compare current and context-backed paths on
reference Windows hardware first, then macOS.

**Stop/rollback:** retain the current adapter behind the interface if fidelity
or memory regresses; do not ship a mixed path. Standardise only after corpus,
security and user-journey checks pass.
