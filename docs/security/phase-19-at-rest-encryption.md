# Phase 19 — At-Rest Protection for Classroom Private Data

## Decision

SQLCipher is not part of the pinned cross-platform dependency set. Ogma therefore
uses application-level authenticated encryption for sensitive values in the
per-profile classroom database. This is an explicit bounded decision: indexes and
ownership fields remain queryable, while annotation bodies and AI history content
are encrypted before EF Core writes them to SQLite.

## Cryptographic contract

- AES-256-GCM with a 96-bit random nonce and a 128-bit authentication tag.
- A 32-byte profile secret is stored through `IClassroomCredentialStore`.
- The row-encryption key is derived with HKDF-SHA256, scoped by profile ID.
- Associated data includes the field purpose, preventing ciphertext reuse across
  annotation, conflict, query, and response fields.
- The `ogma1:` envelope is versioned for future migration.
- Existing pre-encryption plaintext remains readable once and is rewritten in the
  encrypted format on the next save; no plaintext is emitted for new writes.

## Protected fields

| Data | Fields |
| --- | --- |
| Student annotations | `Body` |
| Annotation conflicts | `LocalBody`, `RemoteBody` |
| Student AI history | `Query`, `ResponseSummary` |

## Evidence

| Requirement | Evidence |
| --- | --- |
| CTRL-OGMA-014 | `AesGcmAtRestEncryptionServiceTests` and `StudentPrivateRepository_EncryptsSensitiveFieldsInRawDatabase` |
| Profile isolation | Existing `StudentPrivateRepositoryTests` plus profile-scoped HKDF key derivation |
| Tamper detection | `Unprotect_RejectsTamperingAndWrongPurpose` |
| Legacy compatibility | `Unprotect_LegacyPlaintext_RemainsReadableForMigration` |

Main-catalogue whole-file encryption remains an explicit follow-up for the SQLCipher
or encrypted-container decision; this phase does not claim that optional control
CTRL-OGMA-015 is complete.
