# Phase 19 — Path and Update Trust Boundaries

## Implemented controls

| Control | Implementation | Evidence |
| --- | --- | --- |
| CTRL-OGMA-008 | `PathGuard.EnsureWithinRoot` canonicalizes and bounds paths | `PathGuardTests.EnsureWithinRoot_RejectsTraversal` |
| CTRL-OGMA-009 | `PathGuard.CanonicalizeRoot` resolves existing link segments | `PathGuardTests.EnsureWithinRoot_RejectsSymlinkEscapeWhenSupported` |
| CTRL-OGMA-010 | LAN file resolution uses the shared guard | Existing `LanBookFileResolverTests` plus path-guard unit tests |
| CTRL-OGMA-011 | Sidecar resolution uses the shared guard | Existing `SidecarServiceTests` |
| CTRL-OGMA-012 | RSA-4096/PSS/SHA-256 descriptor verifier | `RsaUpdateVerifierTests.VerifyDescriptor_RejectsAlteredDescriptor` |
| CTRL-OGMA-013 | SHA-256 package digest verifier | `RsaUpdateVerifierTests.VerifyPackage_RejectsAlteredPackage` |

## Boundary notes

- URL-escaped path components are decoded before validation so encoded traversal is
  not treated as a safe literal filename.
- Existing symbolic-link segments are resolved before the root comparison. Missing
  final files remain valid candidates only when their existing parent segments stay
  within the root.
- The verifier is deliberately detached from update transport and installation. The
  Phase 22 packaging adapter must supply the descriptor's exact UTF-8 bytes and the
  protected public signing key; an invalid signature or digest must stop the update.
- No signing private key is stored in this repository.

## Verification command

```powershell
dotnet build OgmaLibrary.sln -c Release --no-restore
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~PathGuardTests|FullyQualifiedName~RsaUpdateVerifierTests"
```
