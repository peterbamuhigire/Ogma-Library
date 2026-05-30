# Security Policy

Ogma Library is a local-first desktop application. It opens no inbound network
listener by default and stores the user's catalogue, reading state, annotations,
and indexes on the user's own device. The exception is the **opt-in Library Host
mode** (the LAN / classroom feature), which introduces a deliberately designed,
authenticated, LAN-bounded server surface — see ADR-0010.

We take the security of Ogma Library and its users' data seriously, especially
because the classroom track handles data belonging to students, who may be
minors.

## Supported versions

Until the first stable release, security fixes target the current `develop`
branch and the latest pre-release. After 1.0, the latest Stable release and the
immediately preceding Stable release receive security updates.

## Reporting a vulnerability

**Please do not open a public issue for security vulnerabilities.**

Report privately to **peter@techguypeter.com** with:

- A description of the vulnerability and its impact.
- Steps to reproduce (a proof of concept if possible).
- The affected version, platform (Windows / macOS), and mode (standalone /
  Library Host / Client).

You will receive an acknowledgement within 72 hours and a remediation timeline
after triage. We follow a coordinated-disclosure approach and will credit
reporters who wish to be credited once a fix ships.

## Scope and hardening baseline

Ogma Library's security controls are specified as `CTRL-OGMA-001` through
`CTRL-OGMA-024` in the SRS. Reports touching the following areas are highest
priority:

- **Secrets / credentials** — provider API keys are stored only in the OS
  credential store (Windows Credential Manager / DPAPI, macOS Keychain) and must
  never appear in settings files, the catalogue database, logs, or crash reports.
- **Untrusted document handling** — all imported PDFs are untrusted input;
  rendering and OCR run in an isolated worker boundary with embedded scripts and
  auto-actions disabled.
- **Path / library-root validation** — file operations are confined to the
  validated library root; directory traversal is rejected.
- **Off-device transmission** — every off-device AI or metadata call routes
  through the single AI gateway, shows a payload preview, and is audited; the
  default posture transmits nothing.
- **Library Host mode (LAN)** — the inbound surface, transport, authentication,
  and client isolation (ADR-0010 + Phase 19 threat model).
- **Update integrity** — builds are code-signed and signature-verified
  independently of transport.

## No secrets in the repository

Contributors must never commit API keys, tokens, passwords, or credentialed
connection strings. The pre-commit/CI checks reject changes that introduce
secrets. See `CONTRIBUTING.md`.
