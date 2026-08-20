# Security and Privacy Roadmap

> Part of the canonical [August 39-phase desktop roadmap](../README.md).

## Control map

| Area | Current risk | Remediation phases | Release evidence |
| --- | --- | --- | --- |
| Filesystem roots | prefix/symlink/root-outage ambiguity | 5, 8, 15, 37 | physical hostile path and disconnect suite |
| PDF processing | subprocess is not a sandbox; password environment | 10–11, 37 | network/filesystem/child-process denial and resource ceilings both OSes |
| Database/backups | no complete at-rest/restore lifecycle | 4, 37–39 | migration, backup/restore, optional encryption ADR and erasure |
| Secrets | adapters exist; full lifecycle incomplete | 27, 35–37 | DPAPI/Keychain physical store/rotate/delete tests |
| AI privacy | gateway/views not runtime-complete; notes risk | 27–30, 37 | no-bypass architecture, exact payload capture, consent/retention/delete |
| Metadata providers | automatic queries/cache policy weak | 13, 37 | disclosure, minimised recorded requests, terms/retention evidence |
| WebView/3D | facades; local file/navigation boundary unproven | 31–33, 37 | CSP, opaque assets, hostile message/navigation tests |
| Classroom | live hostile/multi-machine evidence absent | 34–37 | TLS/TOFU/RBAC/range/isolation/quotas/minors tests |
| Logs/diagnostics | fragmented; no global redaction | 17, 27, 37–38 | event schema, redaction tests, retention and support bundle |
| Supply chain/release | unsigned/unnotarized/no update trust | 38–39 | SBOM, signatures, notarization, tamper rejection and rollback |

## Filesystem trust boundary

All file operations use root ID + validated relative path + platform adapter. Canonical resolution verifies a directory boundary, platform case behavior, symlink/reparse policy and current root health. APIs and WebView messages use opaque IDs. Writeback is a separate confirm-backup-verify transaction; failure never destroys the original.

## PDF isolation

The worker receives brokered access to one file and bounded outputs. Windows and macOS adapters deny network, child processes and unrelated filesystem access, enforce CPU/memory/time/output limits and communicate passwords through one-shot IPC. Self-reported environment flags do not count as proof.

## AI and provider privacy

- All completion/embedding egress passes one architecture-enforced gateway.
- Default is AI off; local deterministic library functions remain complete.
- Exact payload preview states provider, tier, fields/passages, purpose and retention evidence.
- Personal notes are excluded by default and separately consented if ever offered.
- Audit records hashes/versions/tokens/cost, not prompts/text unless the user retains history.
- Provider secrets live in DPAPI/Keychain; deletion and rotation are verified.
- Metadata provider queries are disclosed and cached to minimize repeated disclosure.

## Classroom and minors

Standalone mode opens no listener. Host mode exposes a published projection only, with TLS/TOFU, authentication, roles, sessions, range/path validation, rate/quotas and private user state. School AI keys remain on the host. The DPIA, purpose/legal basis, consent where applicable, retention, erasure and administrator audit must be approved before minors use.

## Release trust

Windows builds are Authenticode-signed and packaged for the approved direct/MSIX channels. macOS builds use Developer ID, hardened runtime/entitlements, notarization and stapling for the approved direct channel. Update descriptors are signed independently of HTTPS and verified by the client. Immutable artifacts are promoted between channels; malicious/tampered descriptor and rollback drills are release gates. Signing material uses protected CI/HSM-grade custody with rotation/revocation runbooks.


