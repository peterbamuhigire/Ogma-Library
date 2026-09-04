# Phase 27 provider-secret evidence

Date: 2026-09-04

The existing `SchoolAiKeyProvider` stores provider secrets only through
`IClassroomCredentialStore`; the platform implementation selects Windows
Credential Manager on Windows, Keychain on macOS, Secret Service on supported
Linux hosts, and a restricted file fallback otherwise. The API exposes
configuration status and update time, not secret values. Saves replace the
provider entry, deletes remove it, and mutable key buffers are cleared.

Verification: `SchoolAdminScaffoldTests` and `ClassroomCredentialStoreTests`
passed, 17 tests total.

This closes the secret-reference/rotation/deletion sub-gate only. Durable
provider profiles, budgets, health persistence, UI preview wiring, retention,
and cloud conformance remain open.
