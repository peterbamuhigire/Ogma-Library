# 09. Ollama readiness: plug-and-play on-device and school-Host AI

Owner instruction, 20 November 2026: Ogma is to be given free to schools. On-device Ollama is the
default AI (D-06; Phase 15 §3b). Ogma does **not** implement, bundle, install or manage Ollama. It
must work with no configuration when Ollama is already installed, on one computer or on a school
library Host serving pupil PCs.

This annex is binding for Phases 15, 19 and 20. Each requirement below names the phase that
delivers it. Where it is stricter than a phase file, this annex wins.

## 1. Source and what was verified

The source is the field guide *Offline AI for schools: Ogma Library and Ollama on one library
computer, serving pupil PCs over the school network* (Chwezi Core Systems, revised November 2026,
12 pages). The owner holds a copy outside the repository. It was studied twice for this annex.

The guide's statements about Ogma's code were checked against `main` at `6bd7e213` on 2026-11-20:

| Guide statement | Code | Status |
|---|---|---|
| A local Ollama chat adapter exists and calls `http://localhost:11434` | `Infrastructure/AI/Providers/OllamaChatProvider.cs:9-11` | Verified |
| The adapter requests one complete response, not a stream | `OllamaChatProvider.cs:37-44` (`Stream = false`) | Verified |
| The adapter refuses non-loopback addresses | `OllamaChatProvider.cs:58-64` | Verified |
| The school handler asks for a fixed model named `school-metadata-search` | `Infrastructure/SchoolAdmin/AiProxyEndpointHandler.cs:20` | Verified |
| The handler enforces profile, payload confirmation, policy and rate limit, and grounds citations | `AiProxyEndpointHandler.cs` | Verified (profile, preview, rate limit read; grounding per guide) |
| Standard registration binds the disabled provider | `AiServiceExtensions.cs:63`, called from `App/Composition/CatalogueProcessingModule.cs:44` | Verified |

Further defects found while verifying, not named in the guide. Each one has a fix below.

| # | Defect | Where | Fixed by |
|---|---|---|---|
| OD-1 | Chat adapter has no timeout of its own | `OllamaChatProvider.cs` | OR-05 |
| OD-2 | A missing model or stopped Ollama becomes a raw HTTP exception (`EnsureSuccessStatusCode`) in **both** adapters | `OllamaChatProvider.cs:48`, `OllamaEmbeddingAdapter.cs:54` | OR-05 |
| OD-3 | No `keep_alive`; Ollama's timing fields are discarded | `OllamaChatProvider.cs` | OR-05 |
| OD-4 | The Ollama address is hard-coded three times and `OLLAMA_HOST` is never read | `OllamaChatProvider.cs:11`, `OllamaEmbeddingAdapter.cs:15`, `AiServiceExtensions.cs:54` | OR-17 |
| OD-5 | `localhost` may resolve to IPv6 `::1` first while Ollama listens on IPv4 `127.0.0.1` by default, adding delay or failure on some Windows machines | same three places | OR-17 |
| OD-6 | The system prompt is an English literal inside the adapter, outside the payload builder and its injection guard | `OllamaChatProvider.cs:41` | OR-18 |
| OD-7 | Classroom search asks for a model name no installation has | `AiProxyEndpointHandler.cs:20` | OR-03 |
| OD-8 | Only the disabled provider is registered | `AiServiceExtensions.cs:63` | Phase 15 task 15.4, OR-09 |

The guide's hardware suggestions (Intel N100 / 8 GB; Core i5-8500 / 16 GB; a Z440 with an RTX 3050
8 GB as the shared Host), model tags (`llama3.2:1b`, about 1.3 GB; `llama3.2:3b`) and the 20-second
target are **trial configurations and proposed targets, not measured capacity**. The guide says so
itself. Nothing in Ogma may present them as guarantees.

## 2. Two deployment shapes

**A. Standalone.** One computer runs Ogma and Ollama. Ogma calls Ollama on loopback. This is the
plug-and-play default of Phase 15 §3b.

**B. School Host.** The library computer runs Ogma in Host mode and Ollama. Pupil PCs run Ogma in
Client/Classroom mode and talk **only to the Ogma Host** at its join address. The Host's AI gateway
calls Ollama on its own loopback. **Pupils never reach port 11434, and Ollama is never exposed to
the LAN.** Model storage, compute and simultaneous requests all count against the Host. Installing
Ollama on a pupil PC must not be needed for classroom AI and must not change classroom behaviour.

## 3. Requirements

| ID | Requirement | Phase |
|---|---|---|
| OR-01 | **Detect without setup.** A `LocalOllamaProbe` (Infrastructure) checks the local Ollama API on loopback at startup, when an AI feature or AI settings open, and on a backoff timer while it is absent (Ollama often starts after Ogma following a reboot or power cut). Honour `OLLAMA_HOST` only when it resolves to loopback. Confirm it is Ollama through its version endpoint. Short timeouts, off the UI thread; any failure means "not available", never an error dialog. | 15 |
| OR-02 | **One status model**, used everywhere: `NotInstalled` (no answer), `NotRunning` (if detectable), `NoSuitableModel`, `Ready(model)`, `Busy(queue)`, `Error(kind)`. The Privacy Center, the Advisor and the Host dashboard show the same plain-language state. | 15 |
| OR-03 | **Logical model roles, not fixed names.** Ogma asks for a role (`advisor-chat`, `school-metadata-search`, `embedding`), and a resolver maps it to an installed tag using a curated, dated preference list (data, not code) plus an administrator override. The fixed `school-metadata-search` model name at `AiProxyEndpointHandler.cs:20` becomes a role resolved this way. The list starts from the guide's tags; exact tags and sizes pass the 15.1 currentness gate. | 15 (resolver), 19 (school role) |
| OR-04 | **Missing model is a clear staff message,** never a raw error: "Ollama is running but has no suitable model. On this computer, run `ollama pull <tag>` (about N GB) while online." The optional "Download for me" uses Ollama's own pull API, with consent, size and progress. Never download automatically. | 15 |
| OR-05 | **Harden both Ollama adapters** (chat, and the embedding adapter as Phase 14 leaves it; if Phase 14 replaced it, apply this to its replacement). In `OllamaChatProvider`: a connect timeout and a separate, configurable generation timeout; cancellation honoured; HTTP failures mapped to typed results (model not found, Ollama not running, overloaded, timeout, malformed response) with no raw `ex.Message` reaching the UI; record Ollama's load and total durations for timing evidence; send a configurable `keep_alive` so the model stays loaded through a lesson. Keep the complete-response mode for the school proxy (the guide's acceptance measures time to a finished result). Streaming for the standalone Advisor is optional and must not weaken grounding. | 15 |
| OR-06 | **Warm-up.** When AI is Ready on a Host (and optionally standalone), send one tiny request in the background so the first pupil does not wait for the model to load. Show "Preparing local AI…" meanwhile. | 19 (Host), 15 (option) |
| OR-07 | **Distinct failure messages for pupils and staff** (guide, troubleshooting step 04): rate limit (with retry time), school policy refusal, preview not confirmed, AI not enabled by the teacher, Host AI not running, model missing, Host busy or timed out. A pupil sees a short, kind message; the Host dashboard shows the staff detail and the fix. | 19 (Host), 20 (client) |
| OR-08 | **Host concurrency.** The Host gateway puts classroom AI requests in a bounded queue with a configurable maximum in flight (default 1), a per-request timeout and a queue limit. Pupils see their place in the queue. The existing per-pupil rate limit stays. Ogma never changes Ollama's own configuration; the dashboard may explain Ollama's parallelism setting for administrators. | 19 |
| OR-09 | **Complete-path compatibility.** On the Host, the school proxy resolves the school-approved Ollama provider (not the disabled default) under the `LocalOllama` tier, and keeps every existing gate: enrolled profile, payload preview, policy, quota, rate limit, DPIA screening and citation grounding against catalogue candidates. Phase 19 objective 7's "no key configured" message applies only when neither a Host Ollama nor a school cloud profile exists. | 19 |
| OR-10 | **No invented books.** Answers are limited to the catalogue candidates; any title or citation not among the candidates is removed before display. A small-model hallucination fixture (a fake Ollama reply naming a book that is not in the catalogue) proves it. | 19 |
| OR-11 | **Local AI diagnostics for the librarian.** A Settings → Privacy and AI → Local AI page (and a Host dashboard card): Ollama version, installed models and sizes, the loaded model and whether it runs on CPU or GPU (Ollama's process-status endpoint), last request timings, and the troubleshooting order from the guide. A "Copy report" button produces content-free text. | 15 (page), 19 (Host card) |
| OR-12 | **Ollama cloud models are not local.** A model Ollama reports as remote or cloud-hosted is never chosen automatically and needs the full cloud consent of Phase 15 §3/§3a. | 15 |
| OR-13 | **Languages.** Nothing filters a pupil's query by language (for example Luganda). Model quality and catalogue metadata coverage per language are field evidence, not claims. | 19, 22 |
| OR-14 | **First-release scope is metadata search:** "Which books in our collection cover bean cultivation?" Questions needing book text ("Explain the argument on page 24") depend on Phase 14 retrieval and Phase 16 grounded answers and must not be promised for the school release until those pass. | 19, 28 |
| OR-15 | **Never do:** bundle or install Ollama; start, stop or reconfigure its service; expose 11434 beyond loopback; scan the LAN for Ollama; download a model without consent; send a pupil request to a cloud provider unless the school enabled one with consent. | all |
| OR-17 | **One Ollama endpoint setting.** Replace the three hard-coded addresses with a single `OllamaEndpointOptions` (Infrastructure) used by the probe, chat and embedding adapters and the named HTTP client. Default `http://127.0.0.1:11434/` (IPv4 loopback, not `localhost`); `OLLAMA_HOST` read once and accepted only when loopback; an administrator override in Settings, also loopback-only. A grep test fails on any other `11434` literal in `src/`. | 15 |
| OR-18 | **System prompt through the payload builder.** Move the adapter's literal system prompt into the payload builder used by all providers, so it is versioned, localisable where appropriate and covered by the 15.9 untrusted-data wrapping and red-team fixture. Adapters only transport. | 15 |
| OR-16 | **In-app help** adapts the guide: install Ollama on Windows, Linux and macOS (linking the official pages), the first offline test, the Host setup, and the troubleshooting order. Platform minimums (for example Windows 10 22H2, macOS 14) pass the currentness gate first. | 28 |

## 4. Acceptance in this repository

All automated checks use a **fake Ollama server on loopback** with recorded responses (version,
model list, process status, chat, model-not-found, slow reply, connection refused, malformed body,
remote-flagged model). No real Ollama or model is installed for tests.

- Probe and resolver tests: every OR-02 state; `OLLAMA_HOST` off-loopback ignored; Ollama appearing
  after Ogma starts is detected without a restart (OR-01).
- Role resolution: preference order, administrator override, no suitable model (OR-03, OR-04).
- Adapter tests: timeouts, cancellation, typed error mapping, timing capture, `keep_alive` sent (OR-05).
- Host tests: queue limit and timeout under 1, 3 and 5 simultaneous requests; the per-pupil rate
  limit; all gates still enforced; the hallucination fixture filtered (OR-08 to OR-10).
- Real-window journeys: **J-AI-3** standalone (fake Ollama present → Advisor answers with no setup →
  one-time notice); **J-SCH-AI** Host and client as two isolated instances on loopback (pupil
  confirms a metadata search → Host queue → fake Ollama → grounded result opens the correct
  catalogue book; repeat with Ollama stopped → clear message; restart the fake Ollama → recovers
  without restarting Ogma).
- A grep test: no literal model tag in view models; no `11434` literal outside `OllamaEndpointOptions`;
  no system-prompt literal in a provider adapter; the Host never binds Ollama to a non-loopback address.
- Both adapters: a stopped fake Ollama and a missing model produce typed results, never a raw
  exception (OD-2).

## 5. Field evidence the owner collects (NOT_ASSESSED until then)

From guide pages 6–10, recorded in `docs/implementation/execution/evidence/sept-23-kaizen/ollama-field/`:
a real Ollama answering offline on each target OS; a pupil PC on a real school LAN completing a
confirmed search that the Host's model handled (seen in the Host's process status), with the
internet uplink disconnected; the timing table for 1, 3 and 5 pupils against the proposed target
(nine of ten single-pupil searches usable within 20 seconds, agreed with the teacher); the reboot
check after a simulated power cut; a fluent teacher's check of Luganda searches. Linux Ogma packaging
is unverified; do not recommend a Linux Host until it is.

## 6. Currentness items for 15.1

Verify on docs.ollama.com and ollama.com/library, and record in `00-currentness-register.md` with
dates: the API endpoints used (version, model list, process status, chat, pull) and their error
responses; `keep_alive` semantics; the `OLLAMA_HOST` default; how remote or cloud models are
reported; supported OS minimums; and the tags and sizes in the preference list.
