# Phase 29 Evidence — Untrusted Evidence Payload Boundary

Date: 2026-09-04
Scope: provider payload construction and injection fixtures

`AiProviderPayload` now labels catalogue metadata and content passages as
untrusted library data. Ampersands, angle brackets, line controls, and other
control characters are escaped or removed before provider transport. The
task/query portion remains separate from the evidence sections, and content
cannot inject a closing evidence delimiter followed by a new task line.

Verification is covered by two fixture cases in
`AiProviderAdapterTests.ProviderPayload_ContainsInjectionFixturesAsEscapedUntrustedData`.
The fixture patterns include forged system markup and an attempted closing
delimiter followed by an exfiltration task.

Remaining Phase 29 gates include provider-generated grounded explanations,
durable trace persistence, answer citation navigation, shell consent wiring,
and representative unsupported-claim/abstention benchmarks.
