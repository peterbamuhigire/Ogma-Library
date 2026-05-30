# Reference Hardware Specification

**Version:** 1.0  
**Date:** 2026-05-30  
**Owner:** Peter Bamuhigire / Chwezi Core Systems  
**Source decision:** CON-1 in `docs/plans/grand-plan/phase-00/decisions.md`  

This document specifies the two reference machines used to anchor all
NFR-OGMA performance budgets. Every budget number in the SRS and the grand plan
is interpreted relative to these machines. All performance results collected on
developer hardware before Phase 20 are **trend-only** and do not constitute
final acceptance evidence.

---

## 1. Machine Profiles

### W-REF-01 — Windows Reference Machine

| Attribute | Value |
|---|---|
| **Machine class** | Mid-range business laptop, circa 2020 |
| **CPU** | Intel Core i5-10210U (Comet Lake-U) |
| **CPU details** | 4 cores / 8 threads; 1.6 GHz base / 4.2 GHz boost; 6 MB L3 cache; 15 W TDP |
| **RAM** | 8 GB DDR4-2666 dual-channel |
| **Storage class** | SATA SSD (2.5" or M.2 SATA) |
| **Storage performance** | Sequential read ≈ 550 MB/s; sequential write ≈ 500 MB/s; random 4 K read ≈ 40 MB/s IOPS |
| **GPU** | Intel UHD Graphics 620 (integrated) |
| **GPU/WebGL2** | WebGL2 supported via Windows WebView2 (Chromium-backed); no discrete GPU |
| **Display** | 1920 × 1080 (Full HD), 96 DPI (non-HiDPI) |
| **Operating system** | Windows 10 22H2 (build 19045) with latest cumulative updates |
| **WebView2 runtime** | Fixed-version WebView2 runtime bundled by installer |
| **Network** | Gigabit Ethernet / 802.11ac Wi-Fi (not load-bearing for local-first NFRs) |

**Rationale for this machine:** W-REF-01 represents the lower bound of the
expected Windows install base at launch: a 2020-era mid-range consumer/business
laptop with an integrated GPU and a SATA SSD (not NVMe). Choosing this as the
Windows reference ensures that NFR-OGMA budgets are achievable for the broad
majority of real users, not just users on premium hardware.

---

### M-REF-01 — macOS Reference Machine

| Attribute | Value |
|---|---|
| **Machine class** | Entry-level MacBook Air, 2022 (first M-series entry model) |
| **CPU** | Apple M1 |
| **CPU details** | 8-core (4 performance + 4 efficiency); 3.2 GHz performance cluster; 3.2 GHz burst on all-core; 16-core Neural Engine |
| **RAM** | 8 GB unified LPDDR4X (shared with GPU) |
| **Storage class** | Apple proprietary NVMe SSD |
| **Storage performance** | Sequential read ≈ 3.4 GB/s; sequential write ≈ 2.9 GB/s; random 4 K read >> W-REF-01 SATA |
| **GPU** | Apple M1 7-core GPU (integrated, unified memory architecture) |
| **GPU/WebGL2** | WebGL2 supported via WKWebView on macOS 13+; Metal-backed |
| **Display** | 2560 × 1664 Retina display (@2x native, 224 DPI) |
| **Operating system** | macOS 13.6 Ventura (latest patch on the 13.x train) |
| **Network** | Gigabit Ethernet (via adapter) / 802.11ax Wi-Fi 6 |

**Rationale for this machine:** M-REF-01 is the entry-level Apple Silicon
Mac — the first M1 MacBook Air — with the minimum 8 GB unified memory. While
M1 storage and memory bandwidth are dramatically faster than W-REF-01, the 8 GB
RAM constraint is the meaningful stress point. macOS 13.6 is chosen because
macOS 13 is the minimum supported version (WKWebView with WebGL2; ADR-0003) and
the `.6` patch represents a stable, widely-deployed state within that train.

---

## 2. NFR-OGMA Budget Anchoring

Each NFR-OGMA budget is interpreted as a pass/fail criterion measured on both
reference machines. Where the budget differs by machine, the **Windows (W-REF-01)
value is the binding gate** because it is the weaker machine. macOS measurements
are expected to be equal or better due to M1 performance characteristics.

| NFR ID | Budget | Measure on W-REF-01 | Measure on M-REF-01 | Notes |
|---|---|---|---|---|
| **NFR-OGMA-001** | Cold start ≤ 3 s P95 | ≤ 3.0 s from process launch to first interactive frame | ≤ 3.0 s | Measured: `Process.Start` → first `Loaded` event on shell window |
| **NFR-OGMA-002** | Catalogue load ≤ 2 s P95 (2,000 books) | ≤ 2.0 s from app-idle to all 2,000 books visible in grid | ≤ 2.0 s | Measured with `gc-perf-2000` corpus; thumbnail prefetch not required |
| **NFR-OGMA-003** | Metadata search ≤ 150 ms P95 | ≤ 150 ms from keypress to result list rendered | ≤ 150 ms | Measured on 2,000-book catalogue with structured SQLite query |
| **NFR-OGMA-004** | Full-text search ≤ 500 ms P95 (warm) | ≤ 500 ms from submit to results rendered | ≤ 500 ms | V1 budget; FTS5 index warm (resident in SQLite page cache) |
| **NFR-OGMA-005** | Page turn ≤ 100 ms P95 (cached) | ≤ 100 ms from navigation action to page rendered | ≤ 100 ms | Cached = page bitmap already in PDFium render cache |
| **NFR-OGMA-006** | 3D shelf ≥ 60 FPS (500 books) | ≥ 60 FPS sustained in Three.js shelf with 500 book spines | ≥ 60 FPS | Measured via WebView frame timing; W-REF-01 Intel UHD 620 is the binding constraint |
| **NFR-OGMA-007** | AI metadata-only ≤ 10 s P95 | ≤ 10 s from AI request to response rendered | ≤ 10 s | Excludes provider network latency (provider-side is not under our control); gateway overhead only |

**NFR-OGMA-008** (annotation durability across abnormal termination) and
**NFR-OGMA-009** (background job recovery without duplicate work) are
correctness NFRs, not timing budgets. They are verified by fault-injection tests
in the test suite, not by timing measurements on the reference machines.

---

## 3. Trend-Only Caveat (Phases 01–19)

All performance measurements taken **before Phase 20** (Performance &
Benchmarks) are classified as **trend-only**:

- Measurements are taken on developer hardware and CI runners, not on W-REF-01
  or M-REF-01.
- A failing trend measurement is a warning, not a release blocker, before
  Phase 20.
- A passing trend measurement does not constitute acceptance evidence.

**Phase 20** is the first phase that requires physical access to W-REF-01 and
M-REF-01 (or machines matching their specifications exactly). Phase 20 collects
the official P95 evidence for all NFR-OGMA-001..007 budgets and records the
results in a Phase 20 benchmark report. Only Phase 20 measurements on the
specified reference machines constitute formal NFR acceptance.

---

## 4. Reference Machine Availability

Physical availability of W-REF-01 and M-REF-01 is an **Owner ask** tracked
in the Phase 00 decision log (CON-1). Until the owner confirms or substitutes
specific physical machines:

- The specifications above define the **target market class** used to interpret
  all performance budgets.
- Phase 20 planning must include procurement or borrowing of machines matching
  these specifications (or owner-approved equivalents).
- An equivalent machine must match: CPU generation and class, RAM amount, storage
  class (SATA vs NVMe distinction is significant; W-REF-01 is intentionally SATA),
  and GPU capability (integrated vs discrete is significant for NFR-OGMA-006).

Substitutions must be recorded as an amendment to this file with the owner's
sign-off and the date.

---

## 5. Change Log

| Date | Author | Change |
|---|---|---|
| 2026-05-30 | Phase 00 execution | v1.0 baseline — CON-1 answer recorded |
