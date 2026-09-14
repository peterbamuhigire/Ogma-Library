# Phase 13: reachable AI settings, privacy and spending control

Status: planned. Owner: AI/security engineer; reviewers: privacy lead and operational-cost reviewer.
Dependencies:03/04 and phase01 privacy contract. Estimate:7-12 person-days.
Findings:F07/F08/F17/F18/F30. Requirements:AI001/002/004/005/009/010/011; old phase27.
Skills:E3/E5/F1/R1/R2/D8; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

Users can keep AI off, configure a supported provider deliberately, inspect exactly what leaves the device, understand cost and remove retained data. Unknown pricing cannot be displayed or enforced as zero.

## Work slices

1. Mount PrivacyCenter and provider settings from the real shell; trace profile selection into the actual IAiProvider/IAiPrivacyService gateway instance. Preserve default AiDisabledProvider and offline tier.
2. Show capabilities, selected model/provider, local/cloud status, connection validation, key presence/rotation/deletion and a clear disabled state. Credentials remain in OS-backed stores; no config/DB/plaintext export.
3. Test preview against captured transport, including adapter-added fields. Bind remembered preview to explicit provider/model/tier/scope/content policy; invalidate when it changes. Consent remains independent and revocable.
4. Fix unknown-price semantics in AiCostCalculator: return unavailable estimate, not zero. Record source/effective date/currency and cached-token treatment. Local execution may have no provider charge but must not imply no resource cost.
5. Reserve prospective monetary cost as well as tokens; reconcile actual usage after completion, retries and cancellation. Test concurrent calls and missing usage; never label a soft after-the-fact threshold a hard cap.
6. Repair durable budget lifecycle: first use versus corrupt/unreadable store, failed save, restart, outstanding reservation and day rollover. Fail safely for paid dispatch when trusted remaining budget is unknown; show recovery rather than silently reset.
7. Validate history/retention/export/erasure through UI and persistence, including embeddings, feedback and audit minimization. Explain immutable security audit versus erasable content without retaining exact payloads by default contrary to privacy intent.

## Acceptance

- Default/no-key/offline critical reading/search journeys pass with zero cloud requests.
- Provider switching and key rotation work on Windows Credential Manager/macOS Keychain; no secret appears in logs, DB, config, screenshots or diagnostic exports.
- Captured outbound requests equal the disclosed permitted fields; declined/revoked consent emits no request.
- Unknown/stale price, corrupted budget and failed persistence cannot report free/available paid use; concurrent reservations respect the approved limit or explicit documented provider uncertainty policy.
- Usage and cost reconcile to fixture provider responses; missing/cached usage and retries have independent oracles.
- Erasure removes the requested content scope across restart and reports residual security records accurately.

## Recovery

Keep provider dispatch disabled until all paid/privacy gates pass. Back up nonsecret configuration; never recover by exporting keys. Restore trusted budget state or require admin reconciliation after corruption. Re-measure cost per successful task and consent comprehension; no current rate is hardcoded from this audit's model-price examples.
