# Phase 9 candidate-blocking evidence

The deterministic `IdentityCandidateBlocking` implementation was tested in
Release mode with the existing identity decision acceptance tests:

- normalized title/author/year records produce candidate pairs while unrelated
  records do not;
- 10,000 identical broad-key profiles with a bucket limit of 64 produce exactly
  `64 × 63 / 2 = 2,016` pairs rather than an all-pairs explosion;
- pair output is deduplicated and ordered by occurrence identity;
- existing exact-copy idempotency and same-edition review tests continue to pass.

This closes only the candidate-blocking sub-gate. Phase 9 remains in progress
until reversible merge/split, provider-conflict review, user consequences and
search/advisor grouping are implemented and evidenced.
