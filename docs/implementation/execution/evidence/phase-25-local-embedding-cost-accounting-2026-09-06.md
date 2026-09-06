# Phase 25 Local Embedding Cost Accounting

Date: 2026-09-06

## Contract

Embedding generation is currently restricted to an `IOllamaEmbeddingProvider`
that must report `IsLocalOnly`. Non-local providers are rejected before chunk
selection or provider invocation.

Each batch result now reports:

- input token count processed by the local model;
- off-device provider egress bytes; and
- estimated external provider cost in USD.

For the current local-only implementation, off-device bytes and external API
cost are explicitly zero. They are fields in the result rather than an
unstated assumption, so a future remote provider cannot silently reuse the
local accounting semantics.

The input token count is accumulated with checked arithmetic from the same
chunk tokenizer used by indexing. Provider-unavailable/rejected batches report
zero tokens, egress, and cost because no chunk reaches a provider.

## Executable proof

The successful two-chunk test verifies a positive local token count and exact
zero external egress/cost. The non-local-provider test verifies rejection before
provider invocation and zero accounting values. Worker compatibility tests
also cover the expanded result contract.

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~EmbeddingGenerationServiceTests|FullyQualifiedName~Phase17StageWorkerTests|FullyQualifiedName~EmbeddingGenerationWorkerTests" --logger "console;verbosity=minimal" -m:1
Passed: 8, Failed: 0, Skipped: 0
```

## Interpretation

This closes provider-cost accounting for the approved local baseline. It does
not estimate electricity, hardware amortization, or operator cost. Any future
off-device embedding capability requires privacy-gateway approval, payload
preview/consent, live provider pricing evidence, and non-zero usage accounting
before release.
