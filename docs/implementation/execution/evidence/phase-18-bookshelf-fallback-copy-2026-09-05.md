# Phase 18 3D Bookshelf Fallback Copy Evidence

Date: 2026-09-05

The 3D bookshelf scene projection no longer embeds English fallback labels for
missing title or author metadata. It now consumes the shared localized
`Search.Result.Untitled` and `Citation.UnknownAuthor` resources while retaining
the bounded scene-label policy.

Verification:

- `Bookshelf3DViewModelTests`: 11 passed, 0 failed, 0 skipped.
- The new missing-metadata test verifies English and French fallback labels.
- `dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration Release --no-restore`:
  0 warnings, 0 errors.
- Full Release solution regression after the copy correction: 1,099 passed
  (903 core, 41 architecture, 155 UI), 0 failed, 0 skipped.

This closes the 3D fallback-copy slice only. Directory-view fallback bindings,
classroom Host-sharing copy, contrast snapshots, and physical
Narrator/VoiceOver journeys remain open for Phase 18.
