# Phase 27 Provider Profile Evidence

Date: 2026-09-04

## Scope

The new `IAiProviderProfileService` stores user-managed provider configuration
without API-key material. Only a `credential:` platform reference is accepted;
provider endpoints are validated against the existing allowlist (loopback-only
for Ollama), and writes use a temporary file followed by atomic replacement.
Default selection is deterministic and older defaults are cleared on save.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase27ProviderProfileTests|FullyQualifiedName~Phase27UsageBudgetTests|FullyQualifiedName~Phase27ProviderResilienceTests" --verbosity minimal -m:1
```

Result: 9 passed, 0 failed, 0 skipped.

The tests cover durable round-trip and deletion, raw-secret rejection,
untrusted-endpoint rejection, and regression coverage for the existing budget
and resilience services.

## Still open

Policy-editing UI, retention/erasure journey, live cloud-provider conformance,
and physical accessibility evidence remain open.
