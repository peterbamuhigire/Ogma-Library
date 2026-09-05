# Phase 18 Directory Fallback Copy Evidence

Date: 2026-09-05

The directory catalogue no longer uses hard-coded XAML fallback literals for
missing paths or titles. `BookSummaryProjection` exposes explicit availability
predicates, and the directory view binds the fallback labels through the
localized `CatalogueViewModel` presentation surface. The catalogue view model
also refreshes its displayed labels when the application culture changes.

Verification:

- `CatalogueDirectoryViewRenderTests`: 2 passed, 0 failed, 0 skipped.
- `dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration Release --no-restore`:
  0 warnings, 0 errors.

This closes the directory fallback-copy and catalogue-label culture-refresh
slice only. Classroom Host-sharing copy, contrast snapshots, complete
application-wide copy coverage, and physical Narrator/VoiceOver journeys remain
open for Phase 18.
