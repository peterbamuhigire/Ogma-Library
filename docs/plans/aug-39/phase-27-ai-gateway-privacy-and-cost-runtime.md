# Phase 27 — AI Gateway, Privacy and Cost Runtime

> [Roadmap index](./README.md) · [Previous](./phase-26-semantic-and-hybrid-retrieval.md) · [Next](./phase-28-advisor-intent-candidates-and-reranking.md)

## Objective
Make the provider-neutral AI gateway fully composed, disabled by default and reachable through transparent settings.

## Business/Product Rationale
AI must enhance Ogma without holding the library hostage or leaking private interests.

## SDLC Requirements
FR-AI-001/002/005/006/009..011, CTRL-016..023.

## Current Repository State
`src/OgmaLibrary.Infrastructure/AI/AiServiceExtensions.cs` has adapter/tier/audit concepts but does not fully register the gateway/provider/preview gate; `src/OgmaLibrary.App/Views/Ai/PrivacyCenterView.axaml` is unlinked.

## Gap Analysis
No complete secret/configuration/health/consent/retention user journey or enforced single egress.

## Architectural Impact
One gateway for completions/embeddings with provider capabilities; architecture tests ban direct provider use.

## Database Work
Provider profiles, consent versions, retention/deletion state, prompt/model usage and budgets; secrets remain OS-store references.

## Backend Work
Concrete gateway, provider factory, timeouts/retries, tier enforcement, payload preview, token/cost accounting and circuit health.

## Frontend Work
AI settings/privacy centre, exact payload preview, local/cloud choice, limits, history/export/delete and degraded states.

## PDF Processing Impact
None.

## Metadata Impact
Personal notes excluded from outbound payloads by default.

## Search Impact
Embedding calls conform to gateway/privacy policy.

## AI/RAG Impact
Primary runtime foundation; no recommendation logic yet.

## 3D Bookshelf Impact
None.

## External Integrations
Ollama and approved cloud providers through conformance tests; provider-specific terms evidence required.

## Privacy Requirements
Purpose/tier/provider consent, minimisation, region/retention/no-training evidence, erasure and audit.

## Security Requirements
DPAPI/Keychain, secret deletion/rotation, endpoint allow/validation, redacted errors/logs.

## Performance Requirements
Connection health cached; strict timeouts; budgets and rate limits.

## Error & Recovery Behaviour
Provider failure leaves core/search usable; deterministic fallback or honest unavailability.

## Logging/Observability
Provider/model/tier/tokens/cost/latency/error only; prompts/content off by default.

## Testing
Unit tier/budget/redaction; DB consent/history deletion; recorded/live-quarantine provider API; OS secret-store physical tests; UI privacy E2E; AI failure/cost; security egress architecture tests; latency.

## Skills Engines Applied
`skills-web-dev` AI gateway/security; `srs-skills` controls; digital research provider evidence; design-system privacy UX.

## Dependencies
Phases 17–18 and 26.

## Parallelisation
Provider adapters, settings UI and audit/retention can proceed against gateway contracts.

## Migration Considerations
Import existing config/history only after consent/retention mapping; never migrate plaintext secrets.

## Definition of Done
- [x] All AI egress passes one enforced gateway.
- [x] AI is disabled by default and core remains complete without it.
- [x] Exact payload preview/consent works.
- [ ] OS secrets, cost limits and deletion pass.
- [x] Provider failure is isolated.

## Kaizen Review
1. Complexity: provider/privacy matrix. 2. Remove scattered adapters. 3. Simplify AI callers. 4. Delete bypass paths. 5. Document payloads/providers. 6. Pattern: policy-enforcing gateway. 7. Debt decreases.
