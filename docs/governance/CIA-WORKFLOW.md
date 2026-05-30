# Change Impact Analysis (CIA) Workflow

Ogma Library is delivered under a Hybrid (Water-Scrum-Fall) methodology: the SRS
is a signed Waterfall baseline, and Agile build phases deliver against it. Any
change that touches a **baselined** requirement (FR, NFR, or CTRL control) must
pass a Change Impact Analysis before it is accepted.

## When a CIA entry is required

- Altering the behavior, acceptance criterion, or scope of a baselined FR/NFR/
  CTRL requirement.
- Changing a ratified ADR (requires a superseding ADR or an amendment).
- Adding or removing a bounded-context dependency edge.
- Changing a public contract consumed across contexts (e.g. `IAiProvider`,
  `ICatalogueReadModel`).

Routine work that implements an already-baselined requirement without changing
it needs only the standard PR checklist, not a full CIA entry.

## The CIA checklist (in every PR)

1. **Bounded contexts affected** — which of the nine contexts change.
2. **Requirement IDs touched** — FR/NFR/CTRL/ADR identifiers.
3. **Baseline alteration?** — does this change a baselined requirement? If yes,
   link the owner sign-off and the ADR amendment.
4. **i18n** — new user strings externalized in en + fr; pseudolocale passes.
5. **Icons + accessibility** — new controls iconified and keyboard/screen-reader
   operable.
6. **Reversibility** — destructive operations have backup/restore.
7. **Privacy/egress** — off-device calls route through the AI gateway with
   payload preview and audit.
8. **Cross-platform** — green on Windows and macOS.

## Rollback plan

A CIA entry that alters a baselined requirement must include a rollback plan:
how to revert the change (code + migration) and restore the prior state. For
schema migrations, this is the backup-before-apply snapshot (NFR-PROD-012).

## Sign-off

The product owner (Peter Bamuhigire) ratifies any baseline-altering change.
The decision and its date are recorded in the relevant ADR or in
`docs/plans/grand-plan/phase-00/decisions.md` for inception-era decisions.
