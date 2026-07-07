# Phase 24 Verification: Final Working Software Acceptance

Run these checks exactly, adding phase-specific commands where the changed module requires them.

1. `dotnet restore OgmaLibrary.sln`
2. `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
3. Targeted tests for affected modules under `entire repository; release artifacts; docs/qa; docs/analysis-report-2026-07-07`. Choose the narrowest matching project/filter first and record the exact command.
4. `dotnet test OgmaLibrary.sln --configuration Release --no-build`
5. If UI changed: run the affected `tests/OgmaLibrary.Tests.Ui` filters and capture before/after screenshots or render-test evidence.
6. If security/release changed: run dependency/SAST/secret/release commands named by the governing skill and record artifact paths.
7. If performance changed: run the documented benchmark on the required reference hardware and store results under `docs/benchmarks` or `docs/qa`.
8. Walk `acceptance-criteria.md` line by line and write pass/fail evidence into `COMPLETED.md`.

Findings that may be marked resolved after this phase: **All findings in 99-findings-register.md**.

Full verification must be rerun after any fix. A partial rerun is not sufficient for phase completion.
