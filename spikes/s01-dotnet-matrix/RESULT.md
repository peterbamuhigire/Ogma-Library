# Spike 1 — .NET 10 Dependency Matrix: RESULT

**Date:** 2026-05-30  
**Executor:** Peter Bamuhigire / Chwezi Core Systems  
**SDK:** .NET 10.0.101 (Windows 11 Pro 10.0.26200)  
**Branch:** feature/phase-01-spikes  
**Status:** PASS — all packages resolve and build on `net10.0`

---

## 1. Methodology

One minimal `net10.0` console `.csproj` per planned library was created under
`spikes/s01-dotnet-matrix/<probe>/`. Each project:

1. Declares exactly the target package(s) (no shared project).
2. Uses a spike-local `NuGet.Config` that clears all machine-level sources and
   adds only `https://api.nuget.org/v3/index.json` (required because the dev
   machine has a broken Telerik local source at a non-existent path).
3. Is restored with `dotnet restore`, built with `dotnet build --no-restore`,
   and run with `dotnet run` to confirm the assembly loads at runtime.
4. Is also restored with `dotnet restore -r osx-arm64` for native-asset packages
   to confirm macOS arm64 RID presence in the package graph.

All `TreatWarningsAsErrors` properties are `false` (spike policy).

---

## 2. Commands run

```
# Per-project (representative; same sequence for all 10 projects):
cd spikes/s01-dotnet-matrix/S01.<ProjectName>
dotnet restore
dotnet build --no-restore
dotnet run --no-build

# macOS arm64 RID check (native-asset packages only):
dotnet restore -r osx-arm64
```

---

## 3. Results table

| # | Package ID | Pinned version | NuGet resolved | Restore | Build | Run | macOS RID | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | `Avalonia` | `11.*` | **11.3.17** | PASS | PASS | `11.3.17.0` | osx-arm64: ✓ (via SkiaSharp.NativeAssets.macOS transitive) | No NU1202. Net10 supported. |
| 2 | `Microsoft.EntityFrameworkCore.Sqlite` | `9.*` | **9.0.16** | PASS | PASS | `9.0.16.0` | N/A (managed) | EF Core 9 → net10 compat confirmed. |
| 3 | `Microsoft.Data.Sqlite` | `9.*` | **9.0.16** | PASS | PASS | `9.0.16.0` | osx-arm64: ✓ (SQLitePCLRaw bundles arm64 native e_sqlite3) | Bundled native: SQLitePCLRaw.bundle_e_sqlite3 2.1.10. |
| 4 | `UglyToad.PdfPig` | `*-*` ¹ | **1.7.0-custom-5** | PASS | PASS | `0.1.8.0` ² | N/A (managed) | Pre-release only on NuGet.org; wildcard must include pre-release (`*-*`). |
| 5 | `SkiaSharp` + `SkiaSharp.NativeAssets.Win32` | `3.*` | **3.119.4** | PASS | PASS | `3.119.0.0` ³ | osx-arm64: ✓ (`SkiaSharp.NativeAssets.macOS/3.119.4` in graph) | Pull native Win32 DLL at build time. macOS RID present. Runtime validation deferred to macOS CI. |
| 6 | `PDFtoImage` | `*` | **5.2.1** | PASS | PASS | `5.2.1.0` | osx-arm64: ✓ (`bblanchon.PDFium.macOS/147.0.7690` + `SkiaSharp.NativeAssets.macOS/3.119.2` in graph) | PDFium 147.0.7690 (bblanchon). Pulls native PDFium + SkiaSharp Win32. macOS RID present. Runtime validation deferred to macOS CI. |
| 7 | `Docnet.Core` | `*` | **2.6.0** | PASS | PASS | `2.6.0.0` | osx-arm64: ✓ (restore succeeded with `-r osx-arm64`) | No macOS-specific native asset package in graph; relies on bundled PDFium. Runtime validation deferred to macOS CI. |
| 8 | `Velopack` | `*` | **1.0.1** | PASS | PASS | `1.0.0.0` ³ | N/A (managed CLI wrapper) | Managed-only on Windows; macOS notarisation validation deferred to Spike 2 + macOS CI. |
| 9 | `Makaretu.Dns.Multicast` | `*` | **0.27.0** | PASS | PASS | `0.27.0.0` | N/A (managed; uses .NET sockets) | Transitive: Makaretu.Dns 2.0.1, IPNetwork2 2.1.2, Common.Logging 3.4.1. Targets netstandard2.0 (compat shim, no NU1202). |
| 10 | `System.Net.Http` | built-in | **10.0.0** (inbox) | PASS | PASS | `10.0.0.0` | N/A (BCL) | No NuGet reference needed; shipped with the runtime. |

**¹ PdfPig version pin note:** `UglyToad.PdfPig` has no stable release on NuGet.org
(only pre-release `1.7.0-custom-*`). Wildcard must use `*-*` (prerelease-inclusive)
or pin explicitly to `1.7.0-custom-5`. Recommend pinning `1.7.0-custom-5` in
`Directory.Build.props` until a stable release is published. Restore fails with
NU1103 when using plain `*`.

**² PdfPig assembly version:** The NuGet package `1.7.0-custom-5` reports
assembly version `0.1.8.0` — expected, this is a custom/pre-release fork and the
assembly version string is not in sync with the NuGet package version.

**³ Assembly version vs. NuGet package version:** Some packages (SkiaSharp, Velopack)
use a lower assembly version number than the NuGet package version. This is normal
and does not indicate a compatibility problem.

---

## 4. NU1202 / NU1605 errors

None observed. All packages resolved cleanly on `net10.0` or via the
`AssetTargetFallback` (netstandard2.0 → net10.0), which .NET 10 supports.

---

## 5. Native-asset packages and macOS arm64 RID summary

Packages that pull native binary assets at build/publish time:

| Package | Native asset package(s) | macOS arm64 RID present | Runtime validation |
|---|---|---|---|
| SkiaSharp | `SkiaSharp.NativeAssets.Win32/3.119.4`, `SkiaSharp.NativeAssets.macOS/3.119.4` | Yes | Deferred to macOS CI runner |
| PDFtoImage | `bblanchon.PDFium.Win32/147.0.7690`, `bblanchon.PDFium.macOS/147.0.7690`, `SkiaSharp.NativeAssets.macOS/3.119.2` | Yes | Deferred to macOS CI runner |
| Microsoft.Data.Sqlite | `SQLitePCLRaw.lib.e_sqlite3/2.1.10` (arm64 inside nupkg) | Yes (`dotnet restore -r osx-arm64` passes) | Deferred to macOS CI runner |
| Docnet.Core | None (bundled PDFium; osx-arm64 restore passes) | Yes | Deferred to macOS CI runner |

`dotnet restore -r osx-arm64` succeeded for all four native-asset packages, and
`osx-arm64` is present in the .NET 10 portable RID graph
(`PortableRuntimeIdentifierGraph.json`). Full runtime validation (shared library
load, P/Invoke calls) must be done on macOS hardware or a macOS CI runner.

---

## 6. Known issues / risks

| Risk | Detail | Mitigation |
|---|---|---|
| PdfPig has no stable NuGet release | Only `1.7.0-custom-*` pre-release exists on nuget.org | Pin to `1.7.0-custom-5` in `Directory.Build.props`; file a Phase 02 issue to monitor for stable release |
| Docnet.Core last release 2.6.0 (2022) | Old release; macOS arm64 native load not confirmed on-device | Spike 2 will do a full runtime benchmark on both platforms; disqualify if arm64 fails to load |
| Makaretu.Dns targets netstandard2.0 | Uses AssetTargetFallback shim for net10.0 | Functional but no net10 TFM; watch for runtime issues in Spike 7 |
| Machine-level NuGet.Config has broken sources | `Telerik Collection` path missing; `Syncfusion` path may not exist on CI | Spike-local `NuGet.Config` with `<clear />` pattern resolves this; replicate in CI pipeline |

---

## 7. Pass/fail verdict

**PASS.** All 10 libraries (11 NuGet package references) resolved and built on
`net10.0` with no `NU1202` (framework incompatibility) errors.

Criterion from Phase 01 README §6: *"`dotnet restore` and `dotnet build` succeed
with no `NU1202` errors on both platforms."*  
Windows confirmed. macOS runtime validation deferred to CI (see §5 above).

---

## 8. Recommended `Directory.Build.props` pins (Phase 02)

```xml
<PackageReference Include="Avalonia"                                  Version="11.3.17" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite"      Version="9.0.16" />
<PackageReference Include="Microsoft.Data.Sqlite"                     Version="9.0.16" />
<PackageReference Include="UglyToad.PdfPig"                           Version="1.7.0-custom-5" />
<PackageReference Include="SkiaSharp"                                 Version="3.119.4" />
<PackageReference Include="PDFtoImage"                                Version="5.2.1" />
<PackageReference Include="Docnet.Core"                               Version="2.6.0" />
<PackageReference Include="Velopack"                                  Version="1.0.1" />
<PackageReference Include="Makaretu.Dns.Multicast"                    Version="0.27.0" />
<!-- System.Net.Http: built-in, no pin needed -->
```
