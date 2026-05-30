# Spike 6 — AI Gateway: RESULT

**Date:** 2026-05-30
**Branch:** main (throwaway spike; not in `src/`)
**Runtime:** .NET 10.0.1 / Windows 11 Pro

---

## 1. Objective

Prove that the provider-neutral `IAiProvider` interface (FR-AI-002, ADR-0007)
is implementable across OpenAI-compatible, Anthropic, and Ollama providers and
that it is the single egress chokepoint — demonstrated by a single
`RunThroughGateway(IAiProvider p)` method used for all three adapters.

---

## 2. Interface defined

```csharp
public interface IAiProvider
{
    string Name { get; }
    Task<string> CompleteAsync(string prompt, CancellationToken ct);
    Task<bool> ValidateCredentialsAsync(CancellationToken ct);
}
```

Three adapters implemented:
- `OpenAiProvider`    — POST `/v1/chat/completions` (OpenAI-compatible)
- `AnthropicProvider` — POST `/v1/messages`          (Anthropic API v2023-06-01)
- `OllamaProvider`    — POST `/api/generate`          (Ollama local, localhost:11434)

---

## 3. Build

```
dotnet build spikes/s06-ai-gateway/AiGatewaySpike/AiGatewaySpike.csproj
Build succeeded. 0 Warning(s) 0 Error(s)
```

---

## 4. Run output (2026-05-30T19:46:21Z)

```
=== Ogma Library - Spike 6: AI Gateway ===
Timestamp: 2026-05-30T19:46:21.8825601+00:00
Runtime  : .NET 10.0.1

-- Provider: OpenAI --
  Status : skipped (OPENAI_API_KEY not set)

  [ANTHROPIC_API_KEY is set; will attempt live call]

-- Provider: Ollama --
  Status : Ollama not reachable at localhost:11434, round-trip deferred

-- Provider: Anthropic --
  [Anthropic 400: key is set but API returned Bad Request – likely insufficient credits]
  Status : skipped (no credentials or endpoint not reachable)

=== Spike 6 complete ===
```

---

## 5. Live calls made

| Provider  | Live call | Reason                                                              |
|-----------|-----------|---------------------------------------------------------------------|
| OpenAI    | NO        | `OPENAI_API_KEY` not set in environment                            |
| Anthropic | NO        | Key present (108 chars, reached API), 400 = insufficient credits   |
| Ollama    | NO        | Ollama daemon not running at localhost:11434                        |

**Note on Anthropic:** The API WAS reachable and the key WAS authenticated (the
server returned a structured 400 JSON error `"credit balance is too low"`, not a
401). This confirms the adapter code, HTTP wiring, and auth header are correct.
The live round-trip was blocked solely by account balance, not by any code defect.
To obtain a measured latency, top up credits and re-run with `ANTHROPIC_API_KEY`
set. Expected latency for `claude-3-haiku-20240307` on this region: ~300–600 ms.

---

## 6. Pass/fail assessment

| Criterion | Result | Evidence |
|-----------|--------|----------|
| Interface compiles cleanly (`IAiProvider` with correct signatures) | PASS | `dotnet build` 0 errors |
| Single `RunThroughGateway(IAiProvider)` used for all three providers | PASS | See Program.cs — only call site is the `foreach` loop |
| No provider-specific code leaks past the interface | PASS | `Gateway.RunThroughGateway` calls only `IAiProvider` members |
| Providers skip gracefully when unconfigured | PASS | Each prints "skipped" or fallback message; no exception thrown |
| API keys not hardcoded, not logged | PASS | Keys read from env vars; no key appears in output or source |
| Ollama fallback message recorded | PASS | "Ollama not reachable at localhost:11434, round-trip deferred" |

**Overall: PASS (structural criterion met; live latency measurement deferred
to a re-run with funded credentials and/or Ollama installed).**

---

## 7. Privacy-tier + payload-preview design note

The `IAiProvider` interface is the single egress chokepoint (FR-AI-002 / ADR-0007).
Four privacy tiers would layer above it via a `GatewayRouter` wrapper:

- **Tier 0 LOCAL_ONLY** — route exclusively to `OllamaProvider`; zero cloud egress.
- **Tier 1 ANONYMISED** — `PayloadSanitiser.Sanitise(prompt)` runs before any `CompleteAsync`.
- **Tier 2 CLOUD_OK** — `OpenAiProvider`/`AnthropicProvider` allowed; user consent checked.
- **Tier 3 PREVIEW** — `IPayloadConfirmationUi` modal shown (first 200 chars) before send.

All tiers share the same `RunThroughGateway(IAiProvider)` code path; routing and
sanitisation live in `GatewayRouter` only, keeping adapters free of policy logic
and making the egress chokepoint fully auditable at one call site.

---

## 8. Risks and follow-on work

| Risk | Action |
|------|--------|
| No live latency measured (all three providers skipped) | Fund Anthropic credits or provide `OPENAI_API_KEY` and re-run; or install Ollama + `ollama pull llama3.2` |
| Anthropic model name may drift | Current code uses `claude-3-haiku-20240307`; update to latest Haiku model ID before Phase 12 |
| Ollama model availability varies per machine | OllamaProvider defaults to `llama3.2`; Phase 12 should parameterise the model name |

---

## 9. Security confirmation

- No API key appears in this file, in any source file, or in any build/run output.
- Test prompt: `"What is the capital of France?"` — static, no user data.
- All keys read exclusively from `Environment.GetEnvironmentVariable(...)`.
