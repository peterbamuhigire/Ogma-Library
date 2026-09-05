# Phase 38 cross-target packaging evidence — 2026-09-06

## Scope

An unsigned `osx-arm64` release candidate was generated from the locked
current tree on Windows to validate the cross-target publish and artifact
integrity path. The candidate was written to a temporary directory outside
the repository and removed after verification.

## Result

- Locked restore completed successfully for all solution projects.
- `dotnet publish --configuration Release --runtime osx-arm64
  --self-contained true` completed successfully.
- `Test-ReleaseCandidate.ps1` passed descriptor and artifact digest checks.
- Candidate SHA-256:
  `6f7576cf6232207bcf7b7cc104d8e8404844d95e9a9fb42637e5dce92bc143c9`.

This is cross-target packaging evidence only. It does not establish macOS
execution, WKWebView behavior, signing, notarization, clean installation,
performance, accessibility, rollback, or owner acceptance.
