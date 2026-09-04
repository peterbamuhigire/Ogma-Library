# Phase 16 asset-variant evidence — 2026-09-04

## Scope

This record closes the deterministic variant implementation gate for Phase 16. It
does not represent physical UI, network-provider, or large-library performance
acceptance.

## Delivered

- `VisualAssetVariants` defines bounded generated families: cover `default`
  (200×300), cover `detail` (400×600), spine `default` (7×100), and spine
  `retina` (14×200).
- `IVisualAssetService.GetVariantAsync` returns one exact ready variant and does
  not silently substitute a different size.
- `ThumbnailService` and `SpineService` expose on-demand variant generation and
  preserve the existing default paths for compatibility.
- The isolated PDF worker accepts the requested dimensions, preserves cover
  aspect ratio with letterboxing, and validates the final image dimensions and
  bounded output before copying it from the worker sandbox.
- `ProviderCoverImageClient` accepts only HTTPS requests to the approved Open
  Library/Google Books image hosts, restricts response formats to JPEG/PNG/WebP,
  rejects redirect responses whose effective URI changes, enforces a 4 MiB
  encoded-byte ceiling and 4096-pixel decoded dimensions, and records the
  downloaded SHA-256.
- `ProviderCoverAssetService` atomically persists validated provider art as a
  deterministic JPEG and registers `provider` provenance through the manifest;
  failed registration cleans up only a newly-created file.
- The image client and persistence service are composed only by
  `AddMetadataEnrichment(..., enableExternalProviders: true)`; the default
  composition registers neither and retains no-egress behavior.
- Sidecar variants use safe deterministic suffixes and retain manifest source
  hash, dimensions, format, version, and lifecycle status.
- The HTTPS classroom Host endpoint resolves the bounded `_provider` cover
  variant when the legacy default cover URL has no generated/default file, so
  a published provider-only cover does not become a 404 for clients.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore --filter "FullyQualifiedName~Phase16VisualAssetTests" --logger "console;verbosity=minimal"
```

Result: **PASS — 6 passed, 0 failed, 0 skipped**.

The tests cover custom-cover protection, exact variant lookup, unsupported
family rejection, catalogue projection, safe paths, and stale-file collection.
The Debug build completed without compiler errors.

## Remaining gates

- Resolver precedence integration, cache policy, source/license metadata, and
  physical network evidence remain open.
- Embedded-art extraction and deterministic source fallback remain unverified.
- Catalogue/3D UI lazy requests and accessibility states require physical host
  evidence; no snapshot or two-display result is inferred from these unit tests.
- Large-library disk/GPU/memory budget remains `NOT ASSESSED` until a controlled
  benchmark is run and retained.
