# Phase 27 Evidence — AI Provider Resilience

Date: 2026-09-04
Scope: active provider-call timeout, retry, and circuit controls

`ResilientAiProvider` now wraps every non-disabled provider created by
`AiProviderFactory`. Each attempt has a bounded timeout, transient transport
failures receive a bounded retry, and repeated failures open a per-provider
circuit. Caller cancellation is preserved and is never retried. A health
registry exposes consecutive failures, total failures, total retries, and the
circuit expiry for diagnostics.

Verification is covered by `Phase27ProviderResilienceTests`:

- transient failure retries once and records telemetry;
- repeated failures open the circuit;
- caller cancellation does not retry.

The durable settings, secret lifecycle, budget persistence, UI, allowlist, and
cloud conformance gates remain open.
