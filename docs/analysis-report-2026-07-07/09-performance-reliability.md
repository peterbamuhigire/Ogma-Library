# Performance, Reliability, and Observability

Score: **45 / 100**. Weight: 8%.

Coverage reviewed: benchmarks, reference hardware docs, 3D shelf FPS gate, metadata health/load tests, DeploymentOps observability and SLO readiness.

| ID | Location | Rule violated | Severity | Evidence | Consequence |
| --- | --- | --- | --- | --- | --- |
| F-PERF-001 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_ADRs.txt:1`, `docs/plans/grand-plan/phase-20/README.md:424` | Signature 3D surface must pass macOS FPS gate. | High | ADR-0003 leaves macOS WKWebView frame-rate gate open; Phase 20 documents flakiness mitigation. | macOS users may get a degraded signature experience. |
| F-PERF-002 | `docs/governance/REFERENCE-HARDWARE.md:77`, `:94` | NFRs must be measured on reference hardware before release. | High | Cold start and performance gates are defined, but measurements before Phase 20 are explicitly preliminary. | Release readiness cannot be proven. |
| F-PERF-003 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | SLOs require instrumentation and operational evidence. | High | DeploymentOps says telemetry surface and SLO measurement surface are not built. | Support cannot detect or triage beta regressions. |
| F-PERF-004 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_TestCompletionReport.txt:1` | Reliability tests must pass under realistic load. | High | 2000-book health dashboard retry test fails in diagnostic run. | Large libraries may experience broken metadata health reporting. |

90%+ means Phase 20 performance gates run on W-REF-01 and M-REF-01; cold start, page turns, search, 3D FPS, OCR, and health dashboard budgets are recorded; opt-in telemetry, local diagnostics, SLO dashboards, and runbooks are exercised.
