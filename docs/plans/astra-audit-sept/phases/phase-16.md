# Phase 16: classroom Host, publication and trust

Status: planned. Owner: networking/security engineer; reviewer: school administrator and security QA.
Dependencies:05/10/13. Estimate:7-12 person-days. Findings:F06/F20/F21.
Requirements:LAN001-010; old phase34. Skills:E5/E4/E1/R1/R2; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

A school administrator intentionally publishes a permitted subset of books to authenticated local clients, understands host exposure and can stop/revoke access. Personal books and reading notes never become shared merely because a folder was added.

## Work slices

1. Inventory Kestrel host routes, session/publication services, asset/PDF resolvers, TLS provisioning, mDNS and audit. Trace every endpoint from authentication to published-scope authorization.
2. Create a usable host setup flow in the real settings navigation: select network/library, explain exposure, choose published folders/shelves, review permitted use and confirm. Default install has no inbound listener.
3. Show host state, connection information, fingerprint, enrollment, sessions and stop/revoke controls. Separate administrative configuration from student joining.
4. Verify per-resource publication rules for catalogue, thumbnails, spines, PDFs, search snippets and AI. Unpublish/revoke invalidates subsequent requests without relying on UI hiding alone.
5. Test TLS trust/pinning, one-time/expiring enrollment, limits, brute-force/rate constraints, network bind scope and redacted audit. Treat NAT/firewall/discovery failures as diagnosable states.
6. Run actual two-machine Windows/Mac pairing plus sustained20-40-client capacity scenarios per canonical LAN thresholds; separate virtual clients from physical-network proof.

## Acceptance

- No listener by default; explicit enable/disable binds/releases intended LAN interface and records a minimal audit event.
- Client cannot enumerate/read unpublished records or assets through alternate endpoints or guessed IDs.
- Invalid/expired/reused enrollment and changed certificate are rejected with actionable messages; no silent trust replacement.
- Stop/revoke/unpublish behavior is defined and verified against active streams, cached projections and subsequent AI calls.
- Canonical LAN latency/session/load budgets pass on defined host hardware; discovery/firewall and network loss are exercised physically.
- Administrator completes publish/enroll/revoke without CLI/environment-variable knowledge.

## Recovery

Keep host disabled on configuration/trust uncertainty. Preserve local standalone reading when networking fails. Back up host configuration and keys through approved OS custody, not plaintext exports. Roll back endpoint changes if authorization or publication tests fail; review all affected client cache policies with phase17.
