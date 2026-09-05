# ADR-0010: Opt-In Library Host Mode Amends CI-2 for the Classroom Track

## Status

Accepted

> Owner-ratified on 2026-09-05 after implementation of the classroom track.
> The transport decision is evidenced below; physical firewall/mDNS and
> reference-network acceptance remain release gates.

> Drafted in Phase 00 from LAN-CLASSROOM-ARCHITECTURE.md. Ratified when the
> classroom track (Phases 16–18) is scheduled. Transport and mutual-auth details
> are pending the Phase 01 LAN spike.

## Date

2026-05-30

## Context

The signed SRS baseline contains constraint CI-2 (HLD §1.3): "The application opens no inbound network listener and exposes no server endpoint." The owner's expanded vision introduces a classroom / school use-case in which a central computer (the Library Host) holds the PDF folder and catalogue of record, and student computers on the local area network browse, read, and search against it. This use-case requires exactly the inbound listener that CI-2 forbids.

The resolution is not to delete CI-2 but to scope it: CI-2 remains the mandatory default for the single-user standalone product. A new, deliberately-designed, opt-in **Library Host mode** introduces a LAN server surface with its own threat model, transport security, isolation controls, and activation gate. This ADR records that scoping decision, establishes the security constraints that govern the Host surface, and defers the precise transport mechanism to a Phase 01 LAN spike.

The same Avalonia / .NET 10 binary runs in one of three runtime modes: Standalone (default, no listener), Library Host (opt-in, LAN server), and Client/Classroom (connects to a Host). The mode is a runtime configuration, not a separate product or separate build.

## Decision Drivers

- **Preserve CI-2 for the single-user product** — no listener is ever opened unless the user explicitly activates Host mode.
- **Introduce a bounded, authenticated LAN server surface** so classrooms can share a curated PDF collection without exporting PDFs to student machines.
- **Isolate the inbound listener** from the credential store and from the untrusted-PDF worker boundary (CTRL-OGMA-005), reusing the same isolation discipline already established for the standalone product.
- **Keep PDFs on the Host** — serve page renders or file streams to clients rather than transferring raw PDF bytes by default, preserving the school's control over its corpus.
- **Preserve student privacy** — per-student reading state, annotations, bookmarks, and AI history remain private to the student and are never readable by other students or passively visible to the Host.
- **Prove the transport before committing** — the Phase 01 LAN spike must validate the chosen transport (trust-pinned self-signed root or mutual-auth scheme) before Phase 16 implementation begins.

## Considered Options

### Option A — Opt-in Host mode scoping CI-2, with isolation and trust-pinned transport

- **Pros:** CI-2 is preserved as the default; the listener is bounded to a dedicated bounded context isolated from credentials and workers; the transport is authenticated and encrypted (no plaintext on the wire); discovery is zero-config via mDNS so students do not need to type IP addresses; the design is extensible to the classroom identity and school-admin / managed-AI tracks (ADR-0012 and successor ADRs) without structural revision.
- **Cons:** the Host's inbound surface becomes the highest-risk new asset in the application and requires a full STRIDE threat model and attack-tree analysis in Phase 19; managing a self-signed root or mutual-auth scheme adds an admin step at setup; two packaging and signing targets are unaffected but the Host process must be explicitly tested for LAN exposure.

### Option B — A separate Host binary / separate product

- **Pros:** the standalone product's threat surface is untouched by the LAN feature.
- **Cons:** two codebases diverge immediately; shared catalogue, reader, PDF, and search logic must be maintained twice; Phases 16–18 value appears only after a second binary is built and deployed; contradicts the "one codebase, three modes" architecture.

### Option C — Peer-to-peer file sharing without a Host role

- **Pros:** no single point with an inbound listener; simpler for small groups.
- **Cons:** no central catalogue of record; PDF corpus is replicated to every student machine, defeating the school's control over its collection; no single AI-key egress point for school-managed AI (Phase 18); audit and curation are unworkable at classroom scale.

### Option D — Cloud relay (traffic routed through an Ogma-operated cloud service)

- **Pros:** avoids LAN networking complexity; no self-signed certificates.
- **Cons:** requires internet connectivity; data leaves the school's premises, raising GDPR/DPPA compliance obligations for minors' data before any classroom deployment; contradicts the local-first principle and the owner's stated LAN use-case; depends on an Ogma-operated backend that does not exist.

## Decision Outcome

Introduce opt-in Library Host mode as a scoped amendment to CI-2. The Host mode is:

- **Off by default.** The application starts in Standalone mode with no listener. An administrator must explicitly activate Host mode through a deliberate settings action.
- **Bounded to a dedicated bounded context.** The `Library Sharing / Host` bounded context owns the entire LAN server surface. It is the only place in the application with an inbound listener. It is isolated from the OS credential store and from the untrusted-PDF worker pool (CTRL-OGMA-005) using the same process/boundary discipline already in place.
- **Encrypted transport, no plaintext on the wire.** The transport is HTTPS over the LAN. The specific scheme — Host-generated certificate with admin trust-pin to clients (self-signed root provisioned at setup), or a mutually-authenticated scheme — is determined by the **Phase 01 LAN spike** and recorded as an amendment to this ADR.
- **Zero-config discovery via mDNS/DNS-SD.** Clients discover the Host (e.g., "Room 12 Library") without typing an IP address. Manual host-address entry is the fallback.
- **PDF content stays on the Host by default.** The Host serves either page renders (Host renders, streams images — raw PDFs never leave) or file streams (client renders locally). The mode is an admin setting per deployment, balancing privacy against performance.
- **No inbound write to the shared catalogue from clients** except through explicitly authorised, audited use-cases (e.g., a teacher curating shelves). Student annotations and reading progress are client-private by default and optionally synced to a per-student store the Host holds.
- **Worker and credential isolation.** The Host process does not share the credential store used by standalone AI keys; school-managed AI keys (Phase 18) are held in a separate OS credential-store entry accessible only to the Host context.

The Phase 01 LAN spike is the gate: no Phase 16 Host implementation begins until the spike has validated the transport, demonstrated mDNS discovery on the target LAN configurations, and confirmed the isolation boundary. The spike outcome is recorded as an amendment to this ADR.

## Consequences

### Positive

- CI-2 is preserved for the standalone product; the inbound listener is introduced only with deliberate admin opt-in, reducing the default attack surface to zero.
- The classroom use-case is realised in a single codebase without a separate product, keeping all catalogue, reader, PDF, and AI logic shared.
- PDF corpus remains on the Host machine; students read without raw PDF files being distributed across the network.
- Student privacy is structurally enforced: per-student state is client-private and never passively visible to the Host or to other students.
- The architecture is extensible to classroom identity (ADR-0012) and school-managed AI (future ADR) without structural revision.

### Negative

- The Host's inbound surface is the new highest-risk asset in the application and requires a full STRIDE threat model (Phase 19) and penetration-testing spike.
- Self-signed root or mutual-auth provisioning adds an admin setup step; the UX for certificate trust-pinning must be clear and error-resistant.
- Hosting on a desktop machine (~20–40 concurrent clients) imposes capacity constraints; render throughput and concurrency budgets must be benchmarked in Phase 20.
- Students are often minors; the school becomes a data controller, raising the compliance bar (GDPR-style DPIA, Uganda DPPA, or applicable jurisdiction) for every off-device feature in the classroom track.

### Affects

- SRS CI-2 (scoped, not deleted; the standalone constraint remains intact); CTRL-OGMA-005 (worker isolation pattern reused for the Host boundary); CTRL-OGMA-016 (AI egress chokepoint moves to the Host in classroom mode); ADR-0007 (the IAiProvider gateway operates on the Host on behalf of the class); ADR-0012 and future classroom-track ADRs (depend on this Host-mode foundation); the Phase 01 LAN spike backlog; the Phase 19 threat model; the DPIA for minors' data.

---

## Amendment Log

_This section is completed when the Phase 01 LAN spike concludes. Record: spike date, transport scheme validated, mDNS discovery confirmed platforms, acceptance thresholds, and any constraints imposed on the Host surface as a result._

| Date | Transport scheme chosen | mDNS validated | Notes |
|------|------------------------|----------------|-------|
| 2026-05-30 | **Kestrel HTTPS** + **Makaretu.Dns.Multicast** (`_ogma._tcp`) | ⏳ deferred (firewall) | Phase 01 Spike 7 — `spikes/s07-lan-transport/RESULT.md` |

### Phase 01 Spike 7 result (2026-05-30)

A two-process host/client spike validated the LAN transport on the dev box.

- **Transport:** a minimal .NET 10 **Kestrel HTTPS** server (dev self-signed
  cert) served a 10 MB payload; the client streamed it over HTTPS.
- **Throughput:** **196.75 MB/s** over loopback — **39× the ≥ 5 MB/s** acceptance
  bar; real Wi-Fi (40–100 MB/s) clears it comfortably. ✅
- **Discovery:** `Makaretu.Dns.Multicast` advertising `_ogma._tcp` compiles and
  loads cleanly. **mDNS latency was not measured** because the dev-box **Windows
  Firewall blocked UDP 5353 multicast**. ⏳
  - **Constraint on the Host (Phase 16):** the Host installer must add a firewall
    rule for UDP 5353; manual host-address entry remains the documented fallback
    (LAN-CLASSROOM §3). Tracked: `TRACK-P01-S7-MDNS`.
- **Security:** dev-trust only in the spike; production trust-pinning of the
  Host's self-signed root is scoped to Phase 16.

**Decision recorded:** the ADR-0010 transport stack is **Kestrel HTTPS +
Makaretu.Dns.Multicast (`_ogma._tcp`)**. ADR-0010 remains **Proposed** (Host-mode
is ratified when Phases 16–18 are scheduled); the transport sub-decision is now
evidence-backed.
