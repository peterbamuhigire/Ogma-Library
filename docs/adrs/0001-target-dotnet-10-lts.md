# ADR-0001: Target .NET 10 LTS as the Application Runtime

## Status

Accepted

> Ratified in Phase 00 by the project owner, 2026-05-30.

## Date

2026-05-30

## Context

Ogma Library is a new local-first desktop application whose first commit lands in May 2026 and whose maintenance window must extend well past initial launch. The original specification names .NET 8 and C# 12. .NET 8 reaches end of support on 2026-11-10, roughly six months after this engagement begins, which would force a runtime migration during the build phases rather than after a stable release. .NET 10 is the November 2025 Long-Term Support (LTS) release, supported through 2028-11-14. Both releases run the same desktop UI stacks the project depends on (Avalonia per ADR-0002, WebView hosting per ADR-0003) and offer equivalent access to the PDF, SQLite, and AI-gateway libraries the architecture requires.

## Decision Drivers

- **Support runway:** a runtime whose support ends before the product stabilises is a liability, not a foundation.
- **Migration cost:** choosing the shorter-lived runtime guarantees a forced in-flight migration during Phase 0 through Phase 7.
- **Library and tooling parity:** the desktop, PDF, and storage dependencies must be available and supported on the chosen runtime.
- **Reversibility:** the project must retain a clean, documented path to fall back if a hard blocker emerges.

## Considered Options

### Option A — .NET 10 LTS

- **Pros:** supported through 2028-11-14, giving a 2.5-year-plus runway past launch; latest LTS C# language and runtime performance; no forced mid-build runtime migration; matches the vision document's runtime-baseline correction.
- **Cons:** a small number of third-party libraries may lag an LTS release by weeks at launch; the team adopts a runtime newer than the one named in the original specification.

### Option B — .NET 8 (as named in the original specification)

- **Pros:** maximum third-party library maturity on day one; matches the literal text of the source specification.
- **Cons:** end of support on 2026-11-10 forces a migration within months of project start; shipping a product on an out-of-support runtime is a security and supportability defect.

### Option C — .NET 8 now, planned migration to .NET 10 later

- **Pros:** starts on the most mature library set; defers the newer runtime.
- **Cons:** schedules certain rework; carries the .NET 8 end-of-support risk through the highest-churn build phases; doubles runtime validation effort.

## Decision Outcome

Adopt .NET 10 LTS as the application runtime and target language version. Fall back to .NET 8 only behind a documented, specific blocker recorded as an amendment to this ADR, and only with a dated migration plan back to .NET 10 LTS attached at the time the blocker is recorded. The blocker must name the library or platform capability that fails on .NET 10, the evidence, and the target date for re-evaluation. This decision is confirmed or revisited at the close of Phase 0, the runtime-decision deadline carried from design-report Section 17.

## Consequences

### Positive

- The product ships on a runtime supported through 2028-11-14, avoiding a forced mid-build runtime migration and an out-of-support launch.
- A single runtime decision governs all build phases, simplifying continuous-integration matrix and dependency validation.

### Negative

- Any dependency that is not yet validated on .NET 10 at adoption time must be spike-tested in Phase 0; an unresolved case triggers the documented fallback rather than a silent downgrade.

### Affects

- Every build artifact and the packaging pipeline (ADR-0009); the Phase 0 risk-spike backlog must include a .NET 10 dependency-validation spike.
