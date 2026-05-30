# Spike S02 — PDFium Wrapper Benchmark Result

**Date:** 2026-05-30
**Platform:** Windows 11 Pro (10.0.26200), x64, .NET 10.0.1
**Status:** COMPLETE — Candidate A (PDFtoImage) is the recommended winner

---

## 1. Methodology

### Fixture generation

Three synthetic PDFs were generated programmatically using **QuestPDF 2025.1.0**
(Community licence) to avoid any third-party copyright dependency:

| Fixture file | Pages | Size | Description |
|---|---|---|---|
| `gc-simple-text.pdf` | 5 | 74 KB | Single-column text, five pages |
| `gc-large.pdf` | 285 | 2.3 MB | 200+ pages of dense text (spike stand-in for the 1000-page fixture; actual page count 285 due to QuestPDF multi-page layout expansion) |
| `gc-two-column.pdf` | 13 | 188 KB | Two-column body text, 10 source pages (expanded to 13 by layout) |

These files are stored under `spikes/s02-pdfium/fixtures/` and are not subject
to any copyright restrictions — they are 100% synthetic.

### Benchmark harness

- **Warmup:** 2 full passes over pages 0–4 of each fixture (results discarded).
- **Timed iterations:** 10 passes × pages 0–4 = 50 samples per fixture per wrapper.
- **Scale:** 1.0 × (72 dpi effective), consistent with a "default render" call.
- **Latency measurement:** `System.Diagnostics.Stopwatch` around each
  `RenderPageAsync` call.
- **Memory measurement:** `GC.GetTotalMemory(forceFullCollection: false)` before
  and after the timed loop; peak delta recorded.
- **Percentiles:** P50 = 50th-percentile sample; P95 = 95th-percentile sample
  (index = `min(floor(n * 0.95), n-1)`).

> **Caveat:** All timings are from a development machine (Windows 11 x64, dev-box
> trend), **not gated to W-REF-01** reference hardware. These numbers establish
> relative ordering between the two candidates; absolute targets are assessed
> against NFR-OGMA-005 (≤ 100 ms P95 cached) on reference hardware in Phase 02.

### IPdfRenderer adapter interface

Both adapters implement the interface consistent with HLD §F:

```csharp
interface IPdfRenderer : IDisposable
{
    string Name { get; }
    int GetPageCount(string path);
    Task<byte[]> RenderPageAsync(string path, int pageIndex, float scale, CancellationToken ct);
}
```

G2 gate criterion "adapter interface is feasible" is **confirmed**.

---

## 2. Wrapper versions

| Candidate | NuGet package | Version | Wrapper licence | Native runtime |
|---|---|---|---|---|
| A (winner) | `PDFtoImage` (sungaila) | 4.1.0 | MIT | bblanchon.PDFium 128.0.6569 (BSD 3-Clause) via `bblanchon.PDFium.Win32` / `bblanchon.PDFium.macOS` |
| B | `Docnet.Core` (GowenGit) | 2.6.0 | MIT | Bundled PDFium (BSD 3-Clause) in `runtimes/` folder |

---

## 3. Measured results

All measurements taken on Windows 11 Pro 10.0.26200, x64, .NET 10.0.1,
Release build. Times in **milliseconds (ms)**, memory in **MB**.

### Candidate A — PDFtoImage 4.1.0

| Fixture | Pages | P50 (ms) | P95 (ms) | Peak heap delta (MB) | Native load |
|---|---|---|---|---|---|
| gc-simple-text | 5 | **77.7** | **124.1** | 4.1 | YES |
| gc-large | 285 | **97.0** | **156.9** | 7.1 | YES |
| gc-two-column | 13 | **94.2** | **139.1** | 4.2 | YES |

### Candidate B — Docnet.Core 2.6.0

| Fixture | Pages | P50 (ms) | P95 (ms) | Peak heap delta (MB) | Native load |
|---|---|---|---|---|---|
| gc-simple-text | 5 | 111.9 | 215.4 | 4.2 | YES |
| gc-large | 285 | 79.2 | 174.8 | 6.3 | YES |
| gc-two-column | 13 | 164.0 | 257.2 | 6.4 | YES |

### Head-to-head P95 comparison

| Fixture | PDFtoImage P95 | Docnet P95 | Winner |
|---|---|---|---|
| gc-simple-text | **124.1 ms** | 215.4 ms | PDFtoImage |
| gc-large | **156.9 ms** | 174.8 ms | PDFtoImage |
| gc-two-column | **139.1 ms** | 257.2 ms | PDFtoImage |

PDFtoImage wins on P95 latency across **all three fixtures**.

---

## 4. Windows x64 native load

Both wrappers loaded their native PDFium binary without error on Windows x64.
`GetPageCount` and `RenderPageAsync` executed successfully for all fixtures
under both adapters.

---

## 5. osx-arm64 RID availability

Inspected NuGet package `runtimes/` folders from the local package cache:

| Candidate | Package providing the macOS native binary | osx-arm64 RID present |
|---|---|---|
| PDFtoImage | `bblanchon.PDFium.macOS` 128.0.6569 | **YES** (`runtimes/osx-arm64/native/libpdfium.dylib`) |
| Docnet.Core | Built-in (`runtimes/osx-arm64/native/pdfium.dylib`) | **YES** |

> macOS *runtime* validation (actually loading the library on Apple Silicon
> hardware) is deferred to macOS reference hardware. The RID presence confirms
> both packages ship the binary; load testing on M1/M2 is the next validation
> step.

---

## 6. Recommended winner

**Candidate A — PDFtoImage 4.1.0** is the recommended wrapper.

**Rationale:**

1. **Lower P95 latency across all fixtures** — PDFtoImage beats Docnet.Core on
   P95 in every fixture (124 ms vs 215 ms on simple text; 157 ms vs 175 ms on
   large doc; 139 ms vs 257 ms on two-column). The two-column gap (139 ms vs
   257 ms) is especially significant: multi-column layouts are common in academic
   and textbook PDFs, which are the primary Ogma use case.

2. **More ergonomic API** — `PDFtoImage.Conversion.ToImage(stream, pageIndex,
   options)` returns an `SKBitmap` directly. The `IPdfRenderer` adapter wraps it
   in two lines. Docnet.Core requires manual raw-BGRA-to-SKBitmap encoding,
   adding complexity and a potential unsafe-pointer path.

3. **Active, higher-level maintenance** — PDFtoImage ships named `RenderOptions`
   (DPI, rotation, background colour, anti-aliasing flags), making future quality
   tuning clean. Docnet.Core exposes raw PDFium flags that require more
   low-level knowledge.

4. **SkiaSharp already in the dependency tree** — Ogma already plans SkiaSharp
   for thumbnail/spine rendering (ADR-0004 baseline). PDFtoImage brings no new
   native dependency; both tools share SkiaSharp 2.88.8 and bblanchon PDFium.

5. **Native load confirmed** — Both loaded; no differentiation here.

**Trade-off noted:** Docnet.Core's P50 on gc-large (79 ms) is slightly lower
than PDFtoImage (97 ms). However, P95 — the NFR gate metric — favours
PDFtoImage, and P95 is the right metric for user-perceived worst-case page turns.

---

## 7. Licence confirmation for app-store distribution

| Asset | Licence | MSIX (Windows Store) | Notarized DMG / Mac App Store |
|---|---|---|---|
| PDFtoImage wrapper code | MIT | Permitted (binary distribution allowed) | Permitted |
| SkiaSharp 2.88.8 | MIT | Permitted | Permitted |
| bblanchon.PDFium binaries | **BSD 3-Clause** | Permitted — binary redistribution allowed with copyright notice preserved in THIRD-PARTY-NOTICES | Permitted — BSD 3-Clause is Mac App Store compatible; no copyleft, no attribution-in-UI requirement |
| Docnet.Core wrapper code | MIT | Permitted | Permitted |

**Conclusion:** All licences (MIT + BSD 3-Clause) are compatible with Mac App
Store and Windows Store redistribution. No LGPL or GPL dependency is introduced
by either candidate. The required action for Phase 02 is to add PDFium (BSD),
SkiaSharp (MIT), and PDFtoImage (MIT) to `THIRD-PARTY-NOTICES.md`.

---

## 8. ADR-0004 amendment evidence

This RESULT.md is the evidence base for the ADR-0004 amendment. Key facts for
the ADR amendment author:

- **Winning wrapper:** `PDFtoImage` 4.1.0 by sungaila (NuGet: `PDFtoImage`)
- **Underlying PDFium:** bblanchon pre-built binaries, version 128.0.6569
- **P50 render latency (warm, dev-box):** 77–97 ms across fixtures
- **P95 render latency (warm, dev-box):** 124–157 ms across fixtures
- **Peak heap delta:** 4–7 MB per render batch (5 pages × 10 iterations)
- **NFR-OGMA-005 status:** Dev-box P95 is 124–157 ms. Target is ≤ 100 ms P95
  *cached* on reference hardware. These figures are on a dev box without page
  caching (each render re-opens the file). With a page-bitmap cache layer
  (planned in Phase 08), warm cache hits will be sub-millisecond. The Phase 02
  benchmark on reference hardware with a warm cache is required to formally gate
  NFR-OGMA-005; spike evidence is directionally positive.
- **osx-arm64 RID:** Present in NuGet package; runtime load deferred to macOS
  hardware.
- **Licence:** MIT + BSD 3-Clause — app-store safe.
