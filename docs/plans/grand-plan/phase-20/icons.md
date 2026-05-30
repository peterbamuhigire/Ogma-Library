# Phase 20 — Icon Manifest

> Phase 20 is primarily a backend / infrastructure phase (benchmarks, fault
> injection, observability). It introduces two small UI surfaces:
> 1. The **telemetry consent toggle** in Settings.
> 2. The **developer diagnostics panel** (debug builds only).
>
> Both surfaces require colorful icons per the owner's mandate. All other
> Phase 20 work (benchmark harness, fault-injection tests, structured logging)
> is non-UI and produces no icon surfaces.

---

## Icon manifest

| Icon key | Used on | Meaning | Style / color note | Sizes | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_telemetry_opt_in` | Settings > Telemetry section header + toggle | Represents anonymous usage data sharing (opt-in) | Outlined signal-wave or data-pulse motif; `accent/sage` (positive/informational) | 16/24/32/48 @1x-3x | ⬜ to procure |
| `ic_telemetry_off` | Telemetry toggle in Off state | Telemetry disabled / data not shared | Same motif as above but with a muted strike or closed form; `accent/slate` | 16/24/32/48 @1x-3x | ⬜ to procure |
| `ic_diagnostics_panel` | Developer diagnostics panel title bar | Performance diagnostics / developer tools | Gauge or speedometer motif; `accent/plum` (AI / intelligence surfaces) | 24/32/48 @1x-3x | ⬜ to procure |
| `ic_perf_meter_ok` | IPerformanceMeter reading chip (within budget) | Performance reading is within NFR budget | Filled circle / checkmark; `accent/sage` | 16/24 @1x-3x | ⬜ to procure |
| `ic_perf_meter_warn` | IPerformanceMeter reading chip (near budget) | Performance reading is within 20% of NFR budget | Filled circle / warning triangle; `accent/clay` | 16/24 @1x-3x | ⬜ to procure |
| `ic_perf_meter_fail` | IPerformanceMeter reading chip (over budget) | Performance reading has breached NFR budget | Filled circle / X; deep red (outside standard palette — confirm with owner) | 16/24 @1x-3x | ⬜ to procure |

---

## Accessible label keys

Every icon above is paired with a localized accessible label. Keys must exist
in `en`, `fr`, and (empty/stub) `es`, `it`, `de` by Phase 21:

| Icon key | Label resource key | en text |
| --- | --- | --- |
| `ic_telemetry_opt_in` | `Settings.Telemetry.Icon.OptIn.Label` | "Usage telemetry enabled" |
| `ic_telemetry_off` | `Settings.Telemetry.Icon.Off.Label` | "Usage telemetry disabled" |
| `ic_diagnostics_panel` | `Diagnostics.Panel.Icon.Label` | "Performance diagnostics" |
| `ic_perf_meter_ok` | `Diagnostics.Meter.Ok.Label` | "Within budget" |
| `ic_perf_meter_warn` | `Diagnostics.Meter.Warn.Label` | "Approaching budget limit" |
| `ic_perf_meter_fail` | `Diagnostics.Meter.Fail.Label` | "Budget exceeded" |

---

## Owner procurement request

**To: Peter Bamuhigire**
**For: Phase 20 — Performance Engineering & Reliability**

Please procure the following premium PNG icon set for the Phase 20 UI surfaces
(telemetry consent toggle in Settings and the developer diagnostics panel).

**Icons needed (6 icons):**

1. `ic_telemetry_opt_in` — signal-wave / data-pulse motif; sage/green accent;
   meaning: "sharing usage data voluntarily."
2. `ic_telemetry_off` — same motif muted/closed; slate accent;
   meaning: "telemetry off."
3. `ic_diagnostics_panel` — gauge or speedometer; plum accent;
   meaning: "developer performance diagnostics."
4. `ic_perf_meter_ok` — green/sage filled indicator; meaning: "within budget."
5. `ic_perf_meter_warn` — clay/amber filled indicator; meaning: "near limit."
6. `ic_perf_meter_fail` — red filled indicator; meaning: "budget exceeded."

**Style requirements (from `ICON-SYSTEM.md`):**
- Colorful, duotone or flat-color style; warm library aesthetic.
- Grid-consistent with the Phase 03 icon family already chosen.
- PNG at **@1x, @2x, @3x** in base sizes **16, 24, 32, 48 px**.
- Light and dark variants (if vendor provides them).
- License permitting redistribution inside a signed desktop app and Store
  distribution (Mac App Store + Windows Store).

**Storage path:** `OgmaLibrary.App/Assets/icons/settings/` (telemetry icons)
and `OgmaLibrary.App/Assets/icons/diagnostics/` (meter and panel icons).

> Note: the developer diagnostics panel is a debug-build-only surface and
> its icons (`ic_diagnostics_panel`, `ic_perf_meter_*`) are not shipping
> in the release build. If procurement is time-constrained, prioritize
> `ic_telemetry_opt_in` and `ic_telemetry_off` for the release build first.
