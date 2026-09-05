# Phase 13 Evidence - Live Open Library Probe

Date: 2026-09-05

## Read-only probe

The approved Open Library metadata endpoint was queried with an application
identifying user agent. The approved cover URL used by the existing provider
boundary was also requested. Response bodies were not retained.

```text
metadata endpoint: HTTP 200, application/json, 5,707 bytes, no redirect
cover endpoint:    HTTP 200, image/jpeg, 70,104 bytes,
                  effective host ia902809.us.archive.org (redirected)
```

## Security interpretation

The metadata endpoint is reachable. The cover response leaves
`covers.openlibrary.org` and lands on an Archive.org host. The current
`ProviderCoverImageClient` rejects that effective URI because it is outside its
exact approved-host policy. This is a successful fail-closed security result,
not successful cover acquisition.

## Gate disposition

Live metadata endpoint reachability is evidenced. Live provider cover
acquisition remains open pending an explicit legal/security decision on the
redirect target, its host policy, and attribution/licensing terms. No
allowlist relaxation is inferred from this probe.
