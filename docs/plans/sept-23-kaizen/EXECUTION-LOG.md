# Sept-23 Kaizen execution log

Append-only. The newest entries go at the bottom. Every resumed session reads this log first, then
continues from the last entry.

## Authority

On 2026-09-25 the owner (Peter Bamuhigire) granted full authority to act and decide on their
behalf, guided by the skills engines, to complete the entire assignment. Their instructions were
to commit and push to `main`, to spawn subagents where useful, and to resume automatically after
usage limits.

A session-only hourly cron job (`:17`) re-enters the work while the session is idle.

## Owner-proxy decisions

These decisions were taken under that authority. Each is the recommended option in
[08-master-plan.md §3](08-master-plan.md#3-owner-decisions-needed), unless noted.

| ID | Decision | Basis |
|---|---|---|
| D-01 | **Restore the deleted tests** (`git restore tests/`) | The owner chose this explicitly on 2026-09-25 |
| D-02 | Sept-23 Kaizen is the **execution sequence**; the Aug-39 matrix stays the **requirement accountability** authority | Implied by the owner's "start implementation" instruction |
| D-03 | Support **multiple library roots**, with add, remove and enable in Settings | The backend `ILibraryRootService.AddAsync` exists; LIB-001 intent |
| D-04 | Derived covers, spines and caches go in the **app-data folder** by default; an opt-in portable sidecar mode is deferred | Privacy and least surprise (design and security engines); K27 |
| D-05 | Local semantic search: **keep the provider seam; offer Ollama detection and setup guidance first**; bundling an ONNX model is re-evaluated in Phase 14 after licence and size review | Avoids an unreviewed model download; the currentness gate is required |
| D-06 | AI providers: a **local-only / offline deterministic mode first**; a cloud provider profile is configurable by the user with their own key; exact model IDs come from the Phase 15 currentness review | Fail-closed privacy doctrine |
| D-07 | Icons: use an **open-licence, consistent colourful icon set with recorded licence** as the interim; the owner's premium PNG purchase is a drop-in later | Purchasing needs the owner's payment; no untraceable assets |
| D-08 | Installers: **keep ADR-0009** (Velopack plus MSIX plus notarised DMG) | Accepted ADR |
| D-09 | Classroom clients: **stream, with a bounded, encrypted, expiring cache** | Privacy of minors (DPIA) |
| D-10 | Extensibility: **defer the plugin loader and local API**; Calibre/Zotero import is optional in Phase 25 | Scope control |
| D-11 | Teacher dashboard: a **minimal** view (connected clients, published content) | CLIENT-012 minimum |
| D-12 | Beta cohort: plan and protocol prepared; **recruiting and running it is NOT ASSESSED** until the owner recruits participants | Needs real people |

## Entries

- **2026-09-25** — Audit and plan committed (`d9585a2`) and pushed. Temporary files cleaned.
- **2026-09-25** — Phase 00 started. T00.1: tests restored (D-01); the solution builds in Release with 0 errors and 0 warnings.
