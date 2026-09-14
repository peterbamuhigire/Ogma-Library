# Ogma Library: September product audit and 21-phase remediation plan

Date: 14 September 2026. Requested by Peter Bamuhigire, Lead Consultant.
Audited commit: `a93900a2bd7313a2948a29d2813ec3e14696f877`.
Scope: audit, testing and planning only. No application remediation is included.

**Verdict: not ready for a school or public desktop release. Published readiness baseline: 40/100, capped as requested; target: 95/100, conditional on evidence.**

The product has substantial engineering underneath an interface that prevents ordinary reading tasks. The command palette remains visible after dismissal attempts, the right inspector remains open without a selected book, and fixed toolbar layouts overlap. These are observed behaviors in a fresh Windows build, not merely aesthetic preferences. The 21 phases prioritize these failures while preserving the existing catalogue, worker, reader and privacy foundations.

## Read in this order

1. [Detailed product and functionality audit](01-product-audit.md): purpose, score, strengths, findings, risk and release verdict.
2. [UI and UX audit](02-ui-ux-audit.md): native-window evidence, command palette diagnosis, right-panel overhaul brief, reader and navigation assessment.
3. [Reference-document audit](03-reference-document-audit.md): all 19 supplied documents, contradictions and requirement implications.
4. [Test report](04-test-report.md): actual commands, results, failures and limitations.
5. [21-phase master plan](05-remediation-master-plan.md): sequence, dependencies, ownership, estimates, old-roadmap crosswalk and 95% gates.
6. [Skills, sources and Kaizen record](06-skills-sources-and-kaizen.md): canonical engine routes, currentness, model review and audit-quality checks.

Individual executable planning briefs are under [phases/](phases/). Evidence, original audit scripts, native screenshots, automation trees and test logs are under [evidence/](evidence/).

## Boundaries that matter

- Windows native behavior was exercised in a separate temporary application-data directory. macOS execution, Narrator/VoiceOver, signed installers, real classroom networks and reference-machine performance remain NOT ASSESSED.
- Scores assess evidenced release readiness; they are not percentages of source code completed or user satisfaction measured. Unknown platform gates do not receive inferred passes.
- The requested 40% ceiling overrides the engines' default 65% ceiling. It does not require inventing failures. The 95% target does not authorize closing safety, accessibility or release blockers through an average.
- The 21 phases reorganize remediation against the existing Aug-39 accountability model. They do not silently replace the approved 39-phase roadmap or renumber its requirements.
- Peter's explicit feedback on the distracting, uncloseable palette and the poor right panel is incorporated in findings F01/F02 and phases 02/07.

Start implementation, when separately instructed, with phases 01 and 02. Preserve this baseline and compare the same journeys after each phase.
