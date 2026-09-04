# Phase 27 Usage-Budget Evidence

Date: 2026-09-04

## Verification

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase27UsageBudgetTests|FullyQualifiedName~Phase27ProviderResilienceTests|FullyQualifiedName~AiGatewayTests|FullyQualifiedName~AiGatewayIntegrationTests" --verbosity minimal -m:1
```

Result: 16 passed, 0 failed.

The implementation provides:

- token reservation before cloud provider execution, after payload preview and
  consent;
- provider-completion token reconciliation and calculated cost recording;
- fail-closed rejection when the daily token or recorded cost allowance is
  exhausted;
- release of reservations on cancellation or provider failure; and
- versioned atomic JSON persistence with an in-memory enforcement fallback.

The ledger is redacted and keyed only by the UTC day. It does not persist
prompts, completions, provider secrets, or user identifiers.

## Scope boundary

This closes the runtime enforcement sub-gate, not the complete Phase 27 gate.
User-editable provider profiles and budget policy, retention/erasure workflow,
and live cloud-provider conformance remain open. Token reservations use a
conservative character-based estimate because providers do not expose a
provider-neutral tokenizer at this boundary; final usage is reconciled from
provider-reported counts when available.
