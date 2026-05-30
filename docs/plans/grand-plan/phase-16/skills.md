# Phase 16 — Skills & Slash Commands

Phase-scoped guidance for skills and slash commands. Every entry states *which
task* it informs and *what artifact* it must produce. Listing skills decoratively
is prohibited (CONVENTIONS.md).

---

## Always-on (inherited from SKILLS-INDEX.md)

| Skill / command | Used in | Produces |
| --- | --- | --- |
| `superpowers:brainstorming` | Before WP1 (ADR-0010 design) and WP8 (Host UI design) | Structured options for transport choice, UI flow options |
| `superpowers:writing-plans` | Phase start | Executable plan from this `tasks.md` |
| `superpowers:executing-plans` / `superpowers:subagent-driven-development` | All WPs | Driven task execution with review checkpoints |
| `superpowers:test-driven-development` | Every WP | Tests written before implementation; interface stubs before implementations |
| `superpowers:verification-before-completion` | Phase DoD | Verification commands run on Windows and macOS before marking phase done |
| `superpowers:requesting-code-review` + `/code-review` | P16-WP11-T8 | Code review findings resolved |
| `superpowers:systematic-debugging` | Any failing test | Diagnosis before fix |
| `superpowers:using-git-worktrees` | Phase 16 branch | `feature/P16-lan-host` worktree |
| `documentation-generation:docs-architect` | P16-WP1-T7 | Updated `SOURCE-SUMMARY.md`, ADR cross-references |

---

## Phase-16-specific skills

### `architecture:system-architecture-design`

- **When:** WP1 (ADR-0010 authoring), WP2 (LanHost bounded-context scaffold).
- **Produces:** ADR-0010 document at `docs/architecture/adr-0010-lan-host-mode.md`;
  interface contract files under `Application/LanHost/`.
- **Guidance:** Use to reason through the isolation mandate: `LanHost` must have
  zero compile-time dependency on `CredentialStore`, `UntrustedPdfWorker`, and
  `IAiProvider`. Draw the dependency graph explicitly; generate architecture tests
  from it (P16-WP2-T3).

### `architecture:realtime-systems`

- **When:** WP5 (page-render mode streaming, concurrency limiter).
- **Produces:** Design for `PageRenderConcurrencyLimiter`; decision on whether
  to use Server-Sent Events or polling for queued render notifications.
- **Guidance:** Apply backpressure patterns. The Host is a desktop, not a server;
  10 simultaneous renders is the budget. The limiter must degrade gracefully
  (queue then 429, not crash).

### `security:network-security`

- **When:** WP7 (certificate TOFU, session tokens, subnet validation).
- **Produces:** Reviewed authentication flow; subnet-validation implementation;
  QR-code fingerprint delivery design.
- **Guidance:** Focus on: (1) TOFU MITM risk on first connection — mitigate with
  QR-code physical delivery; (2) session token scope — must include `profileId`
  and `role` for Phase 17; (3) RFC-1918 subnet allowlist at listener level.

### `documentation-generation:architecture-decision-records`

- **When:** P16-WP1-T2 (ADR-0010).
- **Produces:** `docs/architecture/adr-0010-lan-host-mode.md` in the standard
  ADR format (title, status, context, decision, consequences).
- **Guidance:** The ADR must explicitly state the amended text of CI-2, reference
  every CTRL-OGMA control it activates, and list the open questions it defers
  (full 40-client threat model → Phase 19).

### `frontend-design:frontend-design`

- **When:** WP8 (Sharing settings view in Avalonia).
- **Produces:** `SharingSettingsView.axaml` and `SharingSettingsViewModel.cs`;
  QR-code panel control.
- **Guidance:** Apply the calm-control design language (Phase 03 tokens). The
  Host mode toggle is a high-stakes action — the confirmation dialog must be
  clear and not dismissible by accident. Use `ic_host_sharing` as the section
  icon.

### `devops-cloud:reliability-engineering`

- **When:** WP5 (concurrency limiter, graceful render queue), WP9 (audit
  middleware resilience), WP11 (CI pipeline).
- **Produces:** Concurrency limiter implementation; graceful Host shutdown
  sequence (drain in-flight requests before closing listener); CI pipeline
  additions for load smoke tests.
- **Guidance:** Graceful shutdown must: (1) stop accepting new connections;
  (2) drain in-flight requests up to a configurable timeout (default 10 s);
  (3) revoke all sessions; (4) deregister mDNS. Verify this sequence in
  P16-WP7-T4 and P16-WP11-T4.

### `/security-review`

- **When:** P16-WP11-T7, after WP7 and WP5/WP6 are complete.
- **Produces:** Security review findings; resolved issues before merge.
- **Guidance:** Focus the review on: authentication endpoints (WP7), raw-file
  endpoint guard (WP6-T1), path-traversal validation in asset serving (WP4-T1),
  subnet validation bypass vectors (WP7-T3), and audit log completeness (WP9).

### `comprehensive-review:full-review`

- **When:** Phase DoD gate, before closing Phase 16.
- **Produces:** Full review artifact covering correctness, architecture
  conformance, and security posture.

---

## Cross-platform skill notes

The Phase 01 LAN spike will have retired the mDNS library choice and Kestrel
vs. `HttpListener` into ADR-0010. When executing WP1-T1, confirm which libraries
were proven on **both Windows and macOS** before wiring them in WP1-T4 and
WP1-T5. Do not introduce a Windows-only or macOS-only dependency without a
recorded ADR amendment.

The certificate provisioner (WP1-T4) has platform-specific paths:
- **Windows:** DPAPI-backed file (matching CTRL-OGMA-001 pattern from Phase 12).
- **macOS:** Keychain item using the `Security` framework via P/Invoke or a
  managed wrapper. Reference the credential-store abstraction from Phase 12
  (`ICredentialStore`) — but note that `LanHost` must consume this through the
  `Application`-layer interface only, not by direct dependency on the concrete
  `Infrastructure.Credentials` implementation.
