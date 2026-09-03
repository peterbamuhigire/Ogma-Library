# Phase 9 grouping evidence

`IdentityGroupingServiceTests` passed in Release mode. The tests demonstrate:

- reviewed edition/work groups persist active occurrence membership;
- merge and split write before/after JSON change records;
- undo restores the exact prior active membership and increments group version;
- an occurrence cannot be assigned to a second active group.

The schema migration is `20260904100000_Phase09IdentityGrouping`.

Phase 9 remains open for provider-conflict review presentation and direct
search/advisor group-collapse consumers. No phase-completion status is claimed.
