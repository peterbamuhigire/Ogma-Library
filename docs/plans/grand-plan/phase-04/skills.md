# Phase 04 — Skills & Slash Commands

This file narrows the global `SKILLS-INDEX.md` to the concrete tasks in Phase 04.
Every skill listed below has a specific artifact it must produce; skills are not
listed decoratively.

---

## Always-on (inherited from every phase)

| Skill / command | When to invoke | Artifact produced |
| --- | --- | --- |
| `superpowers:writing-plans` | Before starting WP1, convert `tasks.md` into a sequenced execution plan | Ordered task checklist with explicit acceptance criteria per task |
| `superpowers:executing-plans` | Drive each WP using the plan from above | Completed, tested WP with all tasks green |
| `superpowers:test-driven-development` | Before implementing every service/entity in WP2–WP8 | Test file exists and fails before implementation |
| `superpowers:verification-before-completion` | Before marking any WP done | All tests green, arch tests green, `dotnet build` zero warnings |
| `superpowers:requesting-code-review` + `/code-review` | End of each WP (WP3, WP4, WP7, WP8 especially); before phase close | Review findings resolved; no open R1/R2 items |
| `superpowers:systematic-debugging` | Any failing test before proposing a fix | Root-cause documented before fix written |
| `superpowers:using-git-worktrees` | Branch `feature/P04-catalogue-data-layer` from `main` | Isolated branch; PR merges only after DoD |

---

## Phase-04-specific skills

### `backend-databases:database-design-engineering`
**When:** WP2 (entity model), WP3 (migrations), WP9 (Work/Edition schema).
**Task linkage:**
- P04-WP2-T1..T18: inform the index strategy (composite vs. covering) for each
  table; verify cascade-delete rules are correct per the domain logic.
- P04-WP2-T19: provide query-plan analysis inputs so the `EXPLAIN QUERY PLAN`
  assertion in WP10 is meaningful.
- P04-WP3-T1: confirm the down-migration strategy (does dropping all tables
  lose data that cannot be reconstructed? Yes — that is the spec).
**Artifact:** `IEntityTypeConfiguration<T>` classes with documented index rationale
in XML comments; `docs/architecture/catalogue-data-model.md` ERD section.

### `backend-databases:database-reliability`
**When:** WP3 (backup-before-apply), WP7 (encryption toggle), WP8 (export/import).
**Task linkage:**
- P04-WP3-T2, P04-WP3-T4: the `MigrationService` design and its fault-injection
  test; the restore logic must be atomic.
- P04-WP7-T3: the `EncryptionService.Enable/DisableAsync()` swap must be atomic;
  this skill informs the file-swap strategy (write-tmp → rename → delete-old).
- P04-WP8-T1..T4: export/import integrity; the SHA-256 manifest approach.
**Artifact:** `MigrationService`, `EncryptionService`, `ExportBundleService` with
documented rollback paths; fault-injection tests passing.

### `backend-databases:database-internals`
**When:** WP2 (SQLite WAL/FK pragmas), WP6 (query plan), WP10 (EXPLAIN output).
**Task linkage:**
- P04-WP3-T3: verify `PRAGMA journal_mode=WAL` and `PRAGMA foreign_keys=ON` are
  the correct choices for a single-process desktop app.
- P04-WP6-T5, P04-WP10-T3: identify whether the 2,000-book query uses a covering
  index; if not, adjust the index in WP2.
**Artifact:** `EXPLAIN QUERY PLAN` analysis documented in `catalogue-data-model.md`;
confirmed WAL/FK settings in `OnConfiguring`.

### `architecture:validation-contract`
**When:** WP6 (read-model projection interfaces).
**Task linkage:**
- P04-WP6-T1..T4: ensure `ICatalogueReadModel` is a true contract — no
  implementation details leak through it; projection records are immutable;
  no `IQueryable` leaks beyond the `Infrastructure` boundary.
**Artifact:** `ICatalogueReadModel` with sealed projection records; architecture
test `CatalogueReadModel_DoesNotExposeIQueryable`.

### `documentation-generation:architecture-decision-records`
**When:** End of WP7 (encryption approach); end of phase (ADR-0005 ratification).
**Task linkage:**
- P04-WP7-T1..T2: the encryption spike (Phase 01) must produce an ADR amendment;
  this phase ratifies it.
- P04-WP10-T6: record ADR-0005 status as Accepted; file the encryption-approach
  amendment under `docs/adr/ADR-0005a-encryption-approach.md`.
**Artifact:** `docs/adr/ADR-0005.md` (Accepted) + `ADR-0005a-*.md` (encryption
amendment, Accepted).

### `security-scanning:security-hardening` (limited scope)
**When:** WP7 (at-rest encryption).
**Task linkage:**
- P04-WP7-T1: verify the key-derivation approach (OS credential store) is
  correct; confirm no key material is written to disk in plaintext.
**Artifact:** Key-derivation code reviewed; no findings on credential-store usage.

---

## Slash commands

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review` | After WP4 (identity service) and WP7 (encryption) | Correctness + security review; escalate to `--effort high` for WP7 |
| `/simplify` | After WP4 and WP8 are complete | Reduce any over-engineered identity cascade or export logic |
| `/verify` | Before marking phase done | Confirm `dotnet build`, `dotnet test`, arch tests all pass on both platform CI runners |
| `/init` | End of phase | Keep `CLAUDE.md` current with new project structure and data-layer contracts |
