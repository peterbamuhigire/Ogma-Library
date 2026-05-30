# ADR-0007: Route AI Through a Provider-Neutral Gateway with Four Privacy Tiers

## Status

Accepted

> Ratified in Phase 00 by the project owner, 2026-05-30.

## Date

2026-05-30

## Context

The AI reading advisor is a core promise, but the product is local-first and privacy is an acceptance criterion, not decorative language. AI capability spans local on-device inference and several optional cloud providers, and the user must retain explicit, legible control over what leaves the device. The security baseline mandates a single egress chokepoint: all off-device calls route through one gateway and no UI or feature module opens its own provider connection (CTRL-OGMA-016). The compliance surface appears only at off-device transmission (CTRL-OGMA-017 through CTRL-OGMA-022), so the AI layer must enforce consent, payload minimisation, payload preview, and audit at exactly one place. The design must also let providers be added or swapped without rewriting feature code, and must default to the least data exposure.

## Decision Drivers

- **One enforceable egress chokepoint** for every off-device call (CTRL-OGMA-016).
- **Provider neutrality** so cloud and local providers are interchangeable behind one interface.
- **A privacy-tier model** that makes data exposure explicit and selectable by the user.
- **Default to the lowest exposure**, with no transmission unless the active tier permits and the user confirms a previewed payload (CTRL-OGMA-017).
- **Per-tier, per-provider consent, payload minimisation, and audit** enforced in one place.

## Considered Options

### Option A — Provider-neutral IAiProvider gateway with four privacy tiers

- **Pros:** one interface abstracts local and cloud providers; one gateway enforces consent, minimisation, preview, and audit; the four-tier model (offline, metadata-only cloud, content-aware cloud, local model) makes exposure explicit and user-selectable; metadata-only is the safe default for cloud; no feature module can bypass the chokepoint.
- **Cons:** every provider must be adapted to the common interface; the tier model and payload-preview contract must be designed carefully and kept honest as providers evolve.

### Option B — Direct per-feature provider calls

- **Pros:** less abstraction; fastest to wire one provider.
- **Cons:** multiple egress points defeat CTRL-OGMA-016; consent, minimisation, and audit scatter across features and drift; adding a provider touches every caller; privacy control becomes unverifiable.

### Option C — Single hard-coded cloud provider, no local tier

- **Pros:** simplest integration.
- **Cons:** violates local-first and offline-useful principles; no provider neutrality; forces off-device processing for AI at all times.

## Decision Outcome

Adopt a provider-neutral `IAiProvider` gateway as the sole AI egress path, with four privacy tiers:

- **Tier 0 — Offline:** no AI transmission; the application default.
- **Tier 1 — Metadata-only cloud:** sends only title, author, tags, categories, descriptions, and notes; the default for any cloud use.
- **Tier 2 — Content-aware cloud:** sends retrieved document chunks; explicit per-library/per-query opt-in only.
- **Tier 3 — Local on-device model:** inference runs on the device via Ollama or equivalent; no data leaves the machine.

No UI or feature module may call a provider directly (CTRL-OGMA-016); all calls pass through the gateway, which enforces per-tier and per-provider consent (CTRL-OGMA-019), payload minimisation (CTRL-OGMA-020), the no-training default opt-out (CTRL-OGMA-022), and the local audit entry per transmission (CTRL-OGMA-018). Before any off-device send, the gateway presents a payload preview showing the exact content, field set, and destination provider, and transmits only after the user confirms (CTRL-OGMA-017). Adding a provider means implementing the interface and declaring its tier and processing region; it never means a new egress path.

## Consequences

### Positive

- A single chokepoint makes consent, minimisation, preview, and audit verifiable and centrally enforced.
- Providers are interchangeable, and the default posture exposes the least data.

### Negative

- Each provider needs an adapter to the common interface, and the payload-preview contract must stay faithful to what is actually sent.
- Content-aware Tier 2 depends on retrieval (ADR-0006) and must minimise the context it forwards.

### Affects

- CTRL-OGMA-016 through CTRL-OGMA-024 (the entire off-device control set); ADR-0006 (content-aware tier consumes retrieved context); the DPIA screening, which assesses each off-device tier.
