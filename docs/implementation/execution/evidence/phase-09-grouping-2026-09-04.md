# Phase 9 grouping evidence

`IdentityGroupingServiceTests` passed in Release mode. The tests demonstrate:

- reviewed edition/work groups persist active occurrence membership;
- merge and split write before/after JSON change records;
- undo restores the exact prior active membership and increments group version;
- an occurrence cannot be assigned to a second active group.

The schema migration is `20260904100000_Phase09IdentityGrouping`.

The Phase 9 implementation gate is closed. Provider-conflict decisions are
conservative and review-required, while the search/catalogue and advisor
consumers now apply canonical alias/group projections. Physical operator
review screens and cross-platform UI walkthroughs remain release gates owned
by later platform phases.
