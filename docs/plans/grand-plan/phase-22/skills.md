# Phase 22 — Skills & Slash Commands

> Phase-scoped detail. The bird's-eye map is `SKILLS-INDEX.md`.

---

## Always-on (every phase)

| Skill / command | Task | Artifact |
| --- | --- | --- |
| `superpowers:writing-plans` → `superpowers:executing-plans` | Before WP1 | Execution plan for the 3-week phase |
| `superpowers:test-driven-development` | WP4, WP5, WP6 | MAS sandbox tests, trust-chain tests, migration rollback tests before implementation |
| `superpowers:verification-before-completion` | End of each WP | Checklist: artifact produced, signed, verifiable, tested |
| `superpowers:systematic-debugging` | Any signing or sandbox failure | Root-cause analysis before re-submission |
| `superpowers:requesting-code-review` + `/code-review` | End of WP4, WP5 | Review MAS implementation and trust-chain implementation |
| `superpowers:using-git-worktrees` | WP1–WP10 | `feature/P22-release-pipeline`, `feature/P22-mas-sandbox`, `feature/P22-trust-chain` |

---

## Phase-specific skills

### WP1–WP3 — CI pipeline and cross-platform signing

**`devops-cloud:deployment-release-engineering`**
- Tasks: P22-WP1-T1 through P22-WP3-T5
- Produce: `release.yml`, `promote.yml`, signed Windows and macOS artifacts,
  notarized DMG, `docs/distribution/` records.
- Invocation: Use this skill to design the two-tier pipeline (signed binary
  job → packaging + feed job → release job); the channel-aware artifact tagging
  convention; and the promote-not-rebuild policy (when promoting alpha → beta,
  only the feed descriptor is re-signed, not the binary — the binary signature
  timestamps are preserved).

**`cicd-devsecops`**
- Tasks: P22-WP1-T1, P22-WP2-T1, P22-WP3-T1
- Produce: Secrets management for certificate private keys in GitHub Actions;
  supply-chain attestation (SLSA level 2 for the release artifact if feasible).
- Invocation: Use this skill to review the pipeline for credential leakage
  (certificates and private keys must never appear in build logs), to confirm
  that artifact provenance is recorded (commit SHA, workflow run ID, timestamp
  embedded in the artifact metadata), and to assess the supply-chain risk of
  each third-party action used in the pipeline.

---

### WP4 — MAS sandbox

**`mobile-cross:app-store-review`**
- Tasks: P22-WP4-T1 through P22-WP4-T8, P22-WP9-T1..T2
- Produce: `entitlements-mas.plist`; `SandboxedFileSystemService`; MAS build
  validation; submission record.
- Invocation: Use this skill to:
  (1) Review the App Store Review Guidelines relevant to desktop apps that
      access user-selected files (section 2.5 Hardware Compatibility, 5.1
      Privacy, and the specific macOS Sandbox guidance).
  (2) Verify that the Privacy Nutrition Label in App Store Connect accurately
      describes all data collected: local file access (book metadata, reading
      progress, annotations — user-controlled), optional AI network calls
      (disclosed as "optional, user-consented"), and opt-in telemetry
      (device-local, no identifiers).
  (3) Generate a review preparation checklist that the team works through
      before submitting to avoid first-rejection on procedural grounds.

**`documentation-generation:architecture-decision-records`**
- Tasks: P22-WP7-T1
- Produce: `docs/adrs/ADR-0021.md`
- Invocation: Use the ADR template; fill Context (MAS sandbox requirement,
  CI-2 amendment, LAN Host exclusion), Decision (two macOS targets, security-
  scoped bookmarks, `#if APPSTORE` build conditional), and Consequences (users
  on MAS build cannot use LAN Host; bookmark must be persisted reliably; MAS
  build must be tested independently of the direct build).

---

### WP5 — Velopack trust chain

**`devops-cloud:deployment-release-engineering`** (continued)
- Tasks: P22-WP5-T1 through P22-WP5-T5
- Produce: `VelopackUpdateService` with Ed25519 verification; trust-chain tests;
  delta update test.
- Invocation: Specifically use this skill to design the Ed25519 key embed
  strategy (the public key is compiled into the binary via a `const string`
  resource; it is part of the signed artifact, so tampering with the public
  key breaks the Authenticode / Developer-ID signature on the binary itself,
  providing a self-bootstrapping trust anchor).

---

### WP6 — Migration rollback

**`backend-databases:database-reliability`**
- Tasks: P22-WP6-T1 through P22-WP6-T3
- Produce: `Migrate_RollbackTest_<versionPair>` suite; rollback procedure doc.
- Invocation: Use this skill to design the rollback test framework: how to
  seed a specific schema-version database, how to exercise the
  backup-before-apply path without the full EF Core migration runner, and how
  to verify that all user-created data survives a rollback (books, annotations,
  bookmarks, reading progress, shelf memberships).

---

### WP8–WP9 — Store submissions

**`mobile-cross:mobile-custom-icons`**
- Tasks: P22-WP8-T1, P22-WP9-T1 (icon asset prep)
- Produce: Store listing icon asset checklist; `docs/distribution/store-listing/`
  asset specification.
- Invocation: Use this skill to generate the icon size matrix for both stores:
  Windows Store requires specific PNG sizes (`StoreLogo.png`, `Square150x150Logo.png`,
  `Square44x44Logo.png`, etc.); Mac App Store requires `icon.icns` (with all
  sizes from 16x16 to 1024x1024). Produce an asset specification document so
  the owner can supply or procure them.

**`documentation-generation:changelog-automation`**
- Tasks: P22-WP10-T2
- Produce: GitHub Release notes generated from `CHANGELOG.md`.
- Invocation: Use this skill to configure the changelog extraction script
  that reads the latest version block from `CHANGELOG.md` and formats it as
  a GitHub Release body with: summary, new features, bug fixes, and
  breaking-change notices.

---

## Slash commands

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review` (high effort) | End of WP4, WP5 | Review MAS sandbox implementation and trust-chain verification code |
| `/security-review` | WP5-T3, WP7-T2 | Confirm CTRL-OGMA-012/013 implementation; review key custody |
| `/verify` | WP3-T5, WP4-T6, WP5-T4 | Run Gatekeeper verification; MAS sandbox test; trust-chain tests |
| `/run` | WP4-T5 | Launch MAS build; confirm library root picker works; confirm LAN Host notice appears |
| `superpowers:finishing-a-development-branch` | Phase gate | Merge/PR strategy for the release pipeline and MAS implementation |
