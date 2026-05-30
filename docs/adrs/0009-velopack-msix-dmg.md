# ADR-0009: Distribute with Velopack for Direct Channels and MSIX for Store and Enterprise

## Status

Accepted

> Ratified in Phase 00 by the project owner, 2026-05-30.

## Date

2026-05-30

## Context

Ogma Library ships to Windows and macOS as a signed desktop application with an auto-update path. The security baseline requires that all public builds be code-signed, that signatures be verified before an update installs, that updates be fetched over an authenticated integrity-checked channel, and that the update descriptor be signature-verified independently of transport (CTRL-OGMA-012 and CTRL-OGMA-013). Two distribution contexts coexist: a direct download channel where the team controls signing, hosting, and the update feed for both platforms, and managed channels — the Microsoft Store and enterprise deployment — that expect a packaged, policy-deployable format on Windows. macOS direct distribution additionally requires Developer ID signing and Apple notarization. The packaging choice must serve both the direct channel and the managed channels without two unrelated release pipelines.

## Decision Drivers

- **Code-signed builds with verified, signature-checked, integrity-checked updates** (CTRL-OGMA-012, CTRL-OGMA-013).
- **A direct download channel the team controls** for both Windows and macOS.
- **A Store- and enterprise-deployable package format** on Windows.
- **macOS Developer ID signing and notarization** for direct distribution.
- **One coherent release pipeline** rather than disconnected per-channel toolchains.

## Considered Options

### Option A — Velopack for direct distribution plus MSIX for Store and enterprise

- **Pros:** Velopack provides cross-platform Windows and macOS direct installers and delta auto-updates with signature verification, matching the team-controlled channel; MSIX provides the Store- and enterprise-policy-deployable Windows package for managed contexts; macOS builds are Developer ID signed and notarized; the two formats cover both channels from one build.
- **Cons:** two packaging targets must be produced and tested; the build pipeline maintains both a Velopack feed and an MSIX package.

### Option B — Velopack only

- **Pros:** one packaging tool; full control of the update feed.
- **Cons:** no Microsoft Store presence and weaker enterprise policy-deployment story on Windows.

### Option C — MSIX only on Windows, native pkg on macOS, no unified updater

- **Pros:** most native to each platform's managed channel.
- **Cons:** no controlled cross-platform direct-download auto-update feed; the team loses delta updates and a single update path; more bespoke pipeline glue.

## Decision Outcome

Adopt Velopack for direct Windows and macOS distribution — the team-controlled download channel with signed, signature-verified, delta auto-updates satisfying CTRL-OGMA-012 and CTRL-OGMA-013 — and MSIX for the Microsoft Store and enterprise policy deployment on Windows. macOS builds are Developer ID signed and notarized for direct distribution. Both packaging targets are produced from the same build pipeline so a release yields a Velopack feed update and, where applicable, an MSIX package together. Update descriptors are signature-verified independently of transport regardless of channel.

## Consequences

### Positive

- The direct channel offers controlled, signed, delta auto-updates on both platforms while the Store and enterprise channels get a policy-deployable Windows package.
- macOS distribution is notarized, meeting platform Gatekeeper expectations.

### Negative

- Two packaging targets must be built, signed, and tested each release, increasing pipeline surface.
- Signing material for Windows Authenticode and Apple Developer ID must be provisioned, secured, and rotated.

### Affects

- CTRL-OGMA-012 and CTRL-OGMA-013 (signing and secure update verification); ADR-0001 (runtime packaged); ADR-0004 (native PDF interop signed per platform).
