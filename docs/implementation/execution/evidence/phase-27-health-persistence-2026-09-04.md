# Phase 27 Provider Health Persistence Evidence

Date: 2026-09-04

## Implementation

`AiProviderHealthRegistry` accepts an optional `IAiProviderHealthStore` and
restores bounded failure, retry, and circuit-expiry state at construction.
`JsonAiProviderHealthStore` writes a versioned document through a temporary
file and atomic replacement. The document contains only provider key and
operational counters/expiry; it contains no API keys, endpoints, prompts, or
responses.

The registry treats I/O and authorization failures as non-fatal. A lost health
file may lose telemetry continuity, but cannot turn a provider call into an
allowed call or interrupt the core catalogue.

## Verification

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase27ProviderResilienceTests" --verbosity minimal -m:1
```

Result: 4 passed, 0 failed.

The persistence test restores failure/retry/circuit values into a new registry
and asserts sensitive payload categories are absent from the serialized file.

## Scope boundary

Provider profile editing, durable token/cost budgets, retention/erasure
journeys, cloud-provider conformance, and physical secret-store evidence
remain open.
