# Phase 14 Remote CI Evidence

Date: 2026-06-01

Pushed commit range:

- `25ab01b..5677bb5 main -> main`

Local gates before push:

- `npm run typecheck` in `src/shelf3d`: passed.
- `npm run build` in `src/shelf3d`: passed.
- `npm run perf:budget` in `src/shelf3d`: passed.
- `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore`: passed.
- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore`: passed, 388 tests.
- `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore`: passed, 23 tests.
- `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore`: passed, 109 tests on the third full run. The first two full UI attempts failed in unrelated timing-sensitive tests (`SearchViewModel_StaleResults_DoNotOverwriteLatestQuery` and `ReaderViewModel_PageTurnP95_With100AnnotationsPerPage_Under100ms`), then the unchanged full UI suite passed.

Remote CI lookup:

- `gh run list --limit 5`: `gh unavailable`.
- GitHub Actions REST lookup:
  `https://api.github.com/repos/peterbamuhigire/Ogma-Library/actions/runs?branch=main&per_page=5`
  returned `404 Not Found`.

Conclusion: Phase 14 was pushed successfully, but remote GitHub Actions status
could not be observed from this workstation/API context. Local gates are recorded
above as the release evidence for this phase.
