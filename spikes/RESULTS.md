# Phase 01 — Spike Results (consolidated)

> Throwaway technical proofs. All code lives under `spikes/`, never `src/`.
> Measurements labelled **dev-box trend** are NOT gated to the reference
> hardware (W-REF-01 / M-REF-01); formal gates run in Phase 20.
> Date: 2026-05-30 · Runtime: .NET 10.0.101 · Dev box: Windows 11 x64.

## Scoreboard

| Spike | Topic | Result | Key number | ADR |
| --- | --- | --- | --- | --- |
| **S1** | .NET 10 dependency matrix | ✅ PASS | 10/10 libs build on net10.0 | ADR-0001 confirmed |
| **S2** | PDFium wrapper benchmark | ✅ PASS | **PDFtoImage** wins, P95 124–157 ms | ADR-0004 amended |
| **S3** | WebView↔C# bridge | ✅ PASS | 7/7 contract checks | ADR-0003 (bridge) confirmed |
| **S4** | 3D macOS WebGL2 FPS | ⏳ deferred | scene ready; FPS needs macOS | ADR-0003 (FPS open) |
| **S5** | SQLite FTS5 indexing | ✅ PASS | **P95 1.97 ms** (budget 500 ms) | ADR-0006 confirmed |
| **S6** | AI gateway (`IAiProvider`) | ✅ PASS (structural) | 3 adapters, 1 chokepoint, no key leak | ADR-0007 confirmed |
| **S7** | LAN transport | ✅ PASS (mDNS ⏳) | **196.75 MB/s**; mDNS firewall-blocked | ADR-0010 amended |

**Go decision:** every MVP-critical architecture choice is backed by evidence.
No spike produced counter-evidence that forces an architecture change. Three
items are environment-deferred (not failures), each tracked below.

---

## S1 — .NET 10 dependency matrix ✅

All 10 planned libraries restore and build on `net10.0` with zero NU1202.

| Package | Version | Notes |
| --- | --- | --- |
| Avalonia | 11.3.17 | shell |
| Microsoft.EntityFrameworkCore.Sqlite | 9.0.16 | ORM |
| Microsoft.Data.Sqlite | 9.0.16 | FTS5 access (native) |
| UglyToad.PdfPig | **1.7.0-custom-5** | **prerelease pin required** (`*-*`) |
| SkiaSharp | 3.119.4 | thumbnails/spines (native) |
| PDFtoImage | 5.2.1 | PDF render (native; see S2) |
| Docnet.Core | 2.6.0 | PDF render candidate B (native) |
| Velopack | 1.0.1 | auto-update |
| Makaretu.Dns.Multicast | 0.27.0 | mDNS (see S7) |
| System.Net.Http | 10.0.0 | built-in |

- **macOS arm64:** `dotnet restore -r osx-arm64` succeeded for all native-asset
  packages; `osx-arm64` is in the .NET 10 RID graph. Runtime load deferred to
  macOS CI (`TRACK-P01-MACOS-NATIVE`).
- **CI note:** the dev box had a broken Telerik local NuGet source; a spike-local
  `NuGet.Config` with `<clear/>` is the fix — **replicate in CI and in Phase 02
  `Directory.Build.props`/`nuget.config`.**
- **Phase 02 input:** pin these versions; PdfPig needs the prerelease-inclusive
  wildcard or an explicit pin.

## S2 — PDFium wrapper benchmark ✅ → **PDFtoImage**

Detail in the ADR-0004 amendment and `spikes/s02-pdfium/RESULT.md`. PDFtoImage
wins P95 on all three synthetic fixtures (124.1 / 156.9 / 139.1 ms); both
wrappers load natively on Windows x64 and expose osx-arm64 RIDs; licences (MIT +
PDFium BSD) permit Windows Store + Mac App Store redistribution. Page **render**
cost is the cold cost; the NFR-OGMA-005 ≤ 100 ms budget is for **cached** page
turns, which the Phase 08 page-render cache serves.

## S3 — WebView↔C# bridge ✅

7/7 headless checks (`dotnet run` exit 0): valid events dispatch; unknown types
and malformed JSON rejected (SI-3); outbound `setScene` uses `ogma://` textures
only. Live WebView round-trip deferred (`TRACK-P01-S3-WEBVIEW-RUNTIME`). Detail:
`spikes/s03-webview-bridge/RESULT.md`.

## S4 — 3D macOS WebGL2 FPS ⏳

500-spine Three.js scene + WebGL2 check + 10 s FPS sampler built. The ≥ 60 FPS
gate (NFR-OGMA-006) needs M-REF-01 (M1, real GPU). Tracked:
`TRACK-P01-S4-MACOS-FPS`. Grid/list remain first-class regardless. Detail:
`spikes/s04-3d-macos/RESULT.md`.

## S5 — SQLite FTS5 indexing ✅ (252× headroom)

External-content FTS5 over a synthetic ~6,000-row corpus; worst-case **P95
1.97 ms** (prefix query) vs the 500 ms NFR-OGMA-004 budget. Integrity-check
passed; the ADR-0006 trigger pattern (insert/update/delete) confirmed. Detail:
`spikes/s05-fts5/RESULT.md`. (dev-box trend; formal gate on W-REF-01 in Phase 20.)

## S6 — AI gateway ✅ (structural)

`IAiProvider` + three adapters (OpenAI / Anthropic / Ollama) behind a single
`RunThroughGateway` call site — no provider-specific code leaks past the
interface (the single-egress-chokepoint pattern for Phase 12, CTRL-OGMA-016).
**No key in any source/output file** (grep-confirmed). No live call: no OpenAI
key; Anthropic auth accepted but out of credits (proves the auth path); Ollama
not running. Detail: `spikes/s06-ai-gateway/RESULT.md`.

## S7 — LAN transport ✅ (mDNS ⏳)

Kestrel HTTPS streamed 10 MB at **196.75 MB/s** loopback (39× the 5 MB/s bar).
mDNS (`Makaretu.Dns.Multicast`, `_ogma._tcp`) compiles/loads but discovery
latency was unmeasured — Windows Firewall blocked UDP 5353. **Phase 16 Host
installer must add the UDP 5353 rule**; manual host entry is the fallback.
Tracked: `TRACK-P01-S7-MDNS`. Detail: `spikes/s07-lan-transport/RESULT.md`.

---

## Tracked items carried out of Phase 01

| ID | What | Owner/phase |
| --- | --- | --- |
| `TRACK-P01-MACOS-NATIVE` | Confirm SkiaSharp/PDFtoImage/Docnet/SQLite native load on osx-arm64 | macOS CI / Phase 02 |
| `TRACK-P01-S3-WEBVIEW-RUNTIME` | Live bridge round-trip on WebView2 + WKWebView | Phase 03/14 |
| `TRACK-P01-S4-MACOS-FPS` | ≥ 60 FPS measurement on M-REF-01; amend ADR-0003 | Owner HW / Phase 14 |
| `TRACK-P01-S7-MDNS` | mDNS discovery latency on a real LAN + firewall rule | Phase 16 |
| `TRACK-P00-GATE` | Hybrid validation gate engine availability in CI | governance |

## Phase 02 hand-off

- Pin the S1 versions in `Directory.Build.props`; add the `<clear/>` nuget.config.
- Adopt PDFtoImage behind `IPdfRenderer`.
- Carry the bridge contract (`BridgeCommand`/`BridgeEvent`, closed event set,
  `ogma://`) into the Phase 14 design.
- FTS5 external-content + triggers pattern is validated for Phase 10.
- `IAiProvider` shape is validated for Phase 12.
- Kestrel HTTPS + Makaretu.Dns for Phase 16.
