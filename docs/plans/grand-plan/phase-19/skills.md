# Phase 19 — Skills & Slash Commands

Phase-scoped guidance. Every entry states which task it informs and what
artifact it must produce.

---

## Always-on

| Skill / command | Used in | Produces |
| --- | --- | --- |
| `superpowers:brainstorming` | Before WP1 (threat model scoping), WP6 (encryption approach decision) | Structured options for threat prioritization, SQLCipher vs. app-level encryption |
| `superpowers:test-driven-development` | WP3 (worker isolation), WP4 (path validation), WP6 (encryption), WP8 (DPIA) | Tests written before hardening code |
| `superpowers:verification-before-completion` | Phase DoD | CTRL-OGMA matrix verified; SAST passes; `/security-review` signed off |
| `superpowers:requesting-code-review` + `/code-review` | After WP9 (SAST) + WP11 | Code review findings resolved |
| `superpowers:systematic-debugging` | Any failing security test | Diagnosis before fix |
| `superpowers:using-git-worktrees` | Phase 19 branch | `feature/P19-security-hardening` |
| `documentation-generation:docs-architect` | WP1, WP8 | Threat model, DPIA register, CTRL-OGMA matrix |

---

## Phase-19-specific skills

### `security-scanning:stride-analysis-patterns`

- **When:** P19-WP1-T2 (STRIDE analysis of all trust boundaries).
- **Produces:** Threat table in `docs/security/threat-model-phase-19.md`
  covering all components × STRIDE categories. The LAN Host ↔ LAN client
  boundary receives the most attention (new highest-risk surface).
- **Guidance:** Apply the DFD from P19-WP1-T1 as the input. For the LAN Host
  boundary: Spoofing (client impersonation, MITM on TOFU), Tampering (catalogue
  projection injection, page-render response substitution), Repudiation (audit
  log tampering), Information Disclosure (student private data leakage, API key
  exposure), DoS (render queue flooding), Elevation (student → admin role
  escalation).

### `security-scanning:attack-tree-construction`

- **When:** P19-WP1-T3 (attack trees for T1..T5).
- **Produces:** `docs/security/attack-trees-phase-19.md` with 5 attack trees;
  each node annotated with the countermeasure.
- **Guidance for T1 (exfiltrate school API key):**
  Root goal: obtain school API key.
  Sub-goals: (a) read from credential store — blocked by OS ACL + CTRL-OGMA-001;
  (b) observe in HTTP response — blocked by CTRL-OGMA-002 + secret scan;
  (c) prompt injection in AI query to echo key — blocked by
  `SchoolAiKeyProvider` never including key in prompt;
  (d) access log file — blocked by CTRL-OGMA-002 (key not logged).
  Leaf nodes with no countermeasure = residual risks → owner accepts or remediates.

### `security-scanning:threat-mitigation-mapping`

- **When:** P19-WP1-T4 (threat-to-control matrix).
- **Produces:** Control matrix table: Threat ID → CTRL-OGMA control(s) →
  test evidence. This is the backbone of the phase's verification record.
- **Guidance:** Every threat must either map to an existing CTRL-OGMA control
  with a passing test, or result in a new control proposal (Phase 19 can
  introduce new sub-controls; they are numbered CTRL-OGMA-025+ if needed and
  documented in the ADR).

### `security-scanning:security-hardening`

- **When:** WP3 (PDF worker isolation hardening), WP4 (PathGuard), WP6
  (at-rest encryption).
- **Produces:** Hardened worker isolation code; `PathGuard` implementation;
  `IAtRestEncryptionService` with SQLCipher or AES-GCM column encryption.
- **Guidance for PathGuard:** the implementation is deliberately simple:
  `Path.GetFullPath(path).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar)`.
  Do not add complexity; the simplicity is the safety. Add a benchmark confirming
  < 1 ms P99 per call (it is called on every file I/O).

### `security-scanning:security-sast` + `security-scanning:sast-configuration`

- **When:** WP9 (SAST configuration and scan).
- **Produces:** Configured `Directory.Build.props` with analyzer packages;
  `.editorconfig` rule severity overrides; SARIF output; `sast-report-phase-19.md`.
- **Guidance:** Start with the default rule set from `SecurityCodeScan`;
  suppress only confirmed false positives (with a comment explaining why);
  never suppress a rule globally without owner approval. The SARIF output is
  the permanent record; do not discard it.

### `security-scanning:security-requirement-extraction`

- **When:** WP1-T5 (build the CTRL-OGMA matrix from the control set).
- **Produces:** The initial CTRL-OGMA matrix populated with requirement IDs,
  source phase, and test-evidence slots (to be filled in WP2..WP8).
- **Guidance:** Use the control set from `SOURCE-SUMMARY.md §F` and the phase
  READMEs (Phases 12, 16, 17, 18) as the control inventory. Do not invent new
  control IDs without necessity.

### `security:dpia-generator` + `security:uganda-dppa-compliance`

- **When:** WP8 (DPIA register, jurisdiction matrix, `IDpiaScreeningService`
  hardening).
- **Produces:** `docs/security/dpia-register.md`; `docs/security/dpia-jurisdiction-matrix.md`;
  hardened `IDpiaScreeningService` implementation.
- **Guidance for Uganda DPPA:** key requirements relevant to Ogma Library
  classroom: data minimization (enforce metadata-only default), lawful basis
  (school as data controller must have consent or legitimate interest for
  processing student data), data subject rights (student can delete own history),
  transfer restrictions (if AI provider is outside Uganda: assess data transfer
  controls). Document each requirement in the DPIA register with the Ogma
  control that satisfies it.

### `/security-review`

- **When:** P19-WP11-T1, after all WPs complete.
- **Produces:** Security review findings document; resolved issues.
- **Guidance:** This is the most comprehensive `/security-review` in the entire
  plan. The reviewer must read the threat model (WP1), verify the CTRL-OGMA
  matrix (WP1-T5), check the SAST report (WP9), and review the at-rest
  encryption implementation (WP6) and the DPIA service (WP8). Allocate a full
  day for this review.

### `comprehensive-review:security-auditor`

- **When:** P19-WP11-T3 (independent sign-off on threat model and findings).
- **Produces:** Sign-off record in `docs/security/phase-19-review-signoff.md`.
- **Guidance:** The security auditor should be a different contributor from the
  one who authored the threat model. If no second contributor is available,
  the owner (Peter) reviews the threat model and the CTRL-OGMA matrix
  personally before sign-off.

### `frontend-design:frontend-design`

- **When:** WP10 (Privacy settings view).
- **Produces:** `PrivacySettingsView.axaml` and `PrivacySettingsViewModel.cs`.
- **Guidance:** The Privacy settings view conveys trustworthiness through calm,
  clear layout — not alarming red colors. Use `ic_privacy_settings` (slate) as
  the section header. The DPIA status column uses sage (Pass), clay (Disqualified),
  slate (Not configured) — paired with text labels, never color alone.
