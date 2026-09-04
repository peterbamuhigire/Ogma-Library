# Phase 25 — Side-by-Side Vector Lifecycle Evidence

Date: 2026-09-04
Commit: the Phase 25 implementation commit containing this evidence

## Scope

This evidence closes the repository-level side-by-side vector generation and
promotion gate. It does not claim ANN quality, provider cost, reference-corpus,
reference-machine, or physical-platform evidence.

## Implemented controls

| Control | Evidence |
| --- | --- |
| Separate generations can coexist for the same chunk/model/version | `EmbeddingVectors` unique key now includes `IndexVersion`; `EmbeddingVectorRepository` reads and upserts by that generation. |
| Active generation is durable | `EmbeddingIndexState` stores the active and resumable staging generation. |
| Rebuild is resumable | `EmbeddingIndexRebuildService` reuses the persisted staging token after provider unavailability or process interruption. Generation is idempotent by chunk and generation. |
| Promotion is fail-closed | `PromoteAsync` accepts only the recorded staging generation and clears the staging pointer in the same state transition. |
| Failed staging does not replace active search | Unavailable-provider verification leaves the active pointer unchanged; semantic retrieval filters by the durable active generation. |
| Memory remains bounded | Existing streamed top-K semantic scan remains in place; staged generation processes bounded batches. |

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~Phase25EmbeddingIndexLifecycleTests --logger "console;verbosity=minimal"
```

Result: **4 passed, 0 failed, 0 skipped**.

The combined schema/lifecycle regression slice also passed:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase11EmbeddingSchemaTests|FullyQualifiedName~Phase25EmbeddingIndexLifecycleTests" --logger "console;verbosity=minimal"
```

Result: **10 passed, 0 failed, 0 skipped**.

The solution debug build passed with **0 warnings and 0 errors** before the
focused test run.

## Remaining gates

- ANN or equivalent target-scale relevance-quality evidence: **NOT ASSESSED**.
- Provider cost accounting and live provider terms/conformance: **NOT ASSESSED**.
- Approved reference corpus and reference-machine performance/memory evidence:
  **NOT ASSESSED**.
- Physical accessibility and cross-platform release evidence: **NOT ASSESSED**.
