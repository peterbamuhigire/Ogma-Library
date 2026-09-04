# Phase 27 Privacy Retention and Erasure Evidence

Date: 2026-09-04
Reviewer: Peter Bamuhigire, Lead Consultant

## Decision

The local retention and erasure journey is closed. The implementation gives a
user a Privacy Center path to export erasable AI history, hard-delete that
history, and erase local semantic embeddings. Immutable AI audit records are
intentionally retained and are not deleted by the history action. Provider
profiles can also be deleted through the durable profile service; credential
material remains outside this JSON boundary in the platform credential store.

This is a local implementation gate only. It is not a provider retention
commitment, legal determination, or cloud-conformance result.

## Evidence

| Control | Evidence | Result |
| --- | --- | --- |
| History export excludes deleted rows | `AiQueryHistoryRepository.ExportToJsonAsync` and `AiPersistenceTests` | PASS |
| History deletion removes erasable rows | `AiQueryHistoryRepository.HardDeleteAllAsync` and `AiPersistenceTests` | PASS |
| Immutable audit survives history deletion | `AiPersistenceTests.QueryHistoryRepository_HardDelete_LeavesAuditIntact` and `PrivacyCenterViewModelTests.PrivacyCenter_DeleteHistory_LeavesAuditIntact` | PASS |
| Local embedding erasure resets derived state | `EmbeddingErasureTests` and `PrivacyCenterViewModel.EraseEmbeddingsAsync` | PASS |
| Desktop controls are reachable | `PrivacyCenterView.axaml` and code-behind wire delete, erase, audit export, and history export actions | PASS; physical accessibility still open elsewhere |
| Provider profile deletion is durable | `Phase27ProviderProfileTests` | PASS |

Verification command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-build --filter "FullyQualifiedName~PrivacyCenterViewModelTests|FullyQualifiedName~AiPersistenceTests|FullyQualifiedName~EmbeddingErasureTests" --verbosity minimal -m:1
```

Result: 13 passed, 0 failed, 0 skipped.

## Remaining Phase 27 gates

Policy-editing UX, provider-specific retention/terms acceptance, live
cloud-provider conformance, and physical accessibility evidence remain open.
