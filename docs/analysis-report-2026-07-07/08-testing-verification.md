# Testing and Verification

Score: **58 / 100**. Weight: 12%.

Coverage reviewed: unit, integration, architecture, UI render, load/performance tests, Test Strategy, Test Completion Report, QA evidence, and benchmarks.

| ID | Location | Rule violated | Severity | Evidence | Consequence |
| --- | --- | --- | --- | --- | --- |
| F-TEST-001 | `dotnet restore OgmaLibrary.sln`, `Directory.Build.props:14` | Canonical test execution must be green without disabling audits. | Critical | Restore fails before tests because NU1903 is warnings-as-errors. | No valid regression baseline exists today. |
| F-TEST-002 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_TestCompletionReport.txt:1` | Full suite must be green for release evidence. | High | Diagnostic run reports 789 cases, 788 passing, 1 failing. | Quality gate is not met even with audit disabled. |
| F-TEST-003 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_TestStrategy.txt:1` | Public-beta gates require platform and reference-hardware evidence. | High | Test Strategy marks G1-G4 lacking platform/reference evidence and G5-G8 needing formal RC gate run. | Beta release cannot be accepted from local tests alone. |
| F-TEST-004 | `tests/OgmaLibrary.Tests.Ui/ReaderViewRenderTests.cs:41`, `docs/qa/PHASE-09-A11Y-SIGNOFF.md:75` | Automated UI render tests do not replace manual accessibility signoff. | Medium | Many headless Avalonia tests exist, but manual accessibility release evidence is incomplete. | False confidence for assistive technology users. |

Strengths: test corpus is broad and includes architecture, UI, migration, AI, LAN host, OCR, and performance tests.

90%+ means restore, build, unit, integration, UI, architecture, security, and performance tests pass in a single canonical run; release-candidate gate records exact commands, platform matrix, hardware, and artifacts.
