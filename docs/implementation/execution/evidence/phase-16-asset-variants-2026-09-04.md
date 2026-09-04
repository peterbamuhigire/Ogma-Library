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
- Sidecar variants use safe deterministic suffixes and retain manifest source
  hash, dimensions, format, version, and lifecycle status.

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

- Provider image acquisition still needs a physically tested allowlisted HTTP
  client, content-type/byte limits, cache policy, and source/license metadata.
- Embedded-art extraction and deterministic source fallback remain unverified.
- Catalogue/3D UI lazy requests and accessibility states require physical host
  evidence; no snapshot or two-display result is inferred from these unit tests.
- Large-library disk/GPU/memory budget remains `NOT ASSESSED` until a controlled
  benchmark is run and retained.
