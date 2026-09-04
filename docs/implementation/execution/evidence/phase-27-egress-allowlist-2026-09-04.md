# Phase 27 AI provider egress allowlist evidence

Date: 2026-09-04

`AiProviderFactory` now validates configured custom endpoints against stable
provider allowlists: OpenAI, DeepSeek, and Anthropic use their canonical API
hosts, while Ollama must remain loopback. Endpoint URLs must be absolute and
cannot contain embedded credentials. Unapproved custom hosts fail before a
provider adapter is created.

Verification: `AiProviderAdapterTests.AiProviderFactory_RejectsUnapprovedCustomEndpoint` passes.

Remaining Phase 27 gates cover configurable profiles, OS-backed secret
references and rotation/deletion, durable budgets, health persistence, full
preview UI wiring, retention/erasure journeys, and cloud conformance.
