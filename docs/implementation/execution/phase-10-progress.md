# Phase 10 Progress - PDF Validation and Containment

Date: 2026-09-04

## Delivered in this increment

- Added `IPdfInputBroker` and typed validation outcomes.
- Added a root-bounded validator that canonicalizes paths through the shared
  pathing authority, rejects escapes and non-PDF extensions, enforces a 512 MiB
  ceiling, and reads only the five-byte PDF magic header before parser entry.
- Registered the broker in the ingestion composition module.
- Added tests for valid input, missing/extension failures, traversal, magic and
  size violations.
- Added a brokered sandbox-local input copy for every worker operation; worker
  arguments no longer carry the original source path.
- Replaced the password environment variable with a one-shot stdin handshake;
  decoded worker buffers are cleared and passwords are absent from process
  environment and command-line arguments.
- Added a persistent-worker regression proving rendering continues after the
  original source path is removed once the sandbox copy is opened.
- Added sandbox-root-checked output reads with a positive-size ceiling and
  SHA-256 output manifest calculation for page artifacts; asset outputs are
  checked by the same sandbox/size policy before copy-out.
- Added Windows Job Object process ceilings for active process count, CPU time,
  and process memory, with configurable timeout, memory, CPU, and output limits.
- Added an oversized-output acceptance test.

## Remaining phase gate

The brokered copy and one-shot password transport gates are now implemented and
tested. True Windows/macOS network and filesystem sandbox adapters, physical
escape evidence, and independent security approval remain open. Current
platform-contract research is recorded in
`evidence/phase-10-platform-sandbox-currentness-2026-09-06.md`; it confirms
that the Windows candidate API is experimental and that macOS enforcement is
entitlement/signing based, so neither is treated as implemented from a
wrapper or environment flag alone.

The Aug-39 Definition of Done now records the locally executable resource,
password-handling, and failure-isolation gates as closed. OS-level escape
proof and independent two-platform security approval remain unchecked and
`NOT ASSESSED`.

Focused worker-isolation and catalogue-secret regression on 2026-09-06:
**12 passed, 0 failed, 0 skipped**.
