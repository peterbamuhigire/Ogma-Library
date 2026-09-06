# Ogma bundled fonts

Ogma uses a deliberate cross-platform pair:

- **Spectral 2.001** for literary display text;
- **Public Sans 2.001** for interface and body text; and
- **JetBrains Mono** for identifiers and technical data only.

Each family is licensed under the SIL Open Font License 1.1. Its licence is
stored beside the redistributed font files. The application embeds these files
as Avalonia resources; platform fonts appear only after the chosen family as a
fallback.

Upstream sources verified on 2026-09-06:

- Spectral: <https://github.com/google/fonts/tree/main/ofl/spectral>
- Public Sans: <https://github.com/uswds/public-sans>
- JetBrains Mono: <https://github.com/google/fonts/tree/main/ofl/jetbrainsmono>

Public Sans upstream is no longer actively developed. Ogma therefore pins the
verified 2.001 asset and does not imply an active support lifecycle.
