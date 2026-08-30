# Release-candidate pipeline record

The release workflow is `.github/workflows/release-candidate.yml`.

## Inputs and trust boundaries

- Manual runs require an explicit SemVer candidate version.
- Tag runs derive the version from the `v*` tag.
- Windows `win-x64` and Apple Silicon `osx-arm64` are built as separate
  platform artifacts because their native runtime dependencies differ.
- `OGMA_RELEASE_SIGNING_KEY` is a protected secret containing the detached
  descriptor signing key. It is materialised only under the ephemeral runner
  temp directory and removed after the command. No private key is committed.
- `require_signature` defaults to true. A run that cannot access its protected
  signing secret fails before it can be treated as a release candidate.

## Gate order

1. Checkout with read-only repository permission.
2. Restore with the repository lock file and publish the app in Release mode.
3. Create the portable candidate, exact descriptor, artifact digest, and
   optional detached signature.
4. Verify descriptor shape, filename binding, and artifact SHA-256.
5. Upload the candidate under a name containing the source commit SHA.

The workflow is a packaging/integrity gate. Authenticode/MSIX signing and
Developer ID/notarization remain protected platform acceptance gates in Phase 39;
the workflow must not be called a final release until their evidence is attached.
The uploaded artifact is immutable for promotion: downstream jobs must consume
the uploaded artifact and descriptor, never rebuild from source.

## Rollback

Promotion is reversible by selecting the previous release ID and digest from the
beta channel. A failed descriptor, digest, install, migration, or observation
gate stops promotion. Application rollback must not imply destructive database
rollback; use the compatibility and backup procedure in
`docs/releases/phase-38-release-pipeline.md`.
