# Conventional Commits

Ogma Library uses [Conventional Commits](https://www.conventionalcommits.org).
The `commit-msg` hook (`.github/hooks/commit-msg`) enforces the format locally;
CI re-checks it on every PR.

## Format

```
type(scope)!: subject

body (optional, wrapped ~72 cols, imperative mood)

footer (optional)
```

- **type** — one of: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`,
  `perf`, `ci`, `build`, `style`, `revert`.
- **scope** — the bounded context or project: `domain`, `application`,
  `infrastructure`, `reader`, `workers`, `bookshelf3d`, `search`, `ai`, `app`,
  `host` (LAN), `build`, `ci`.
- **`!`** — marks a breaking change (also note `BREAKING CHANGE:` in the footer).
- **subject** — imperative, ≤ 72 characters, no trailing period.

## Footers

- Reference the requirement or story: `Implements LIB-012`, `Closes READ-004`,
  or `Closes #123`.
- DCO sign-off is required: `Signed-off-by: Name <email>` (use `git commit -s`).

## Examples

```
feat(workers): isolate per-file scan failures

Each file is processed in its own try/catch; a parse failure is recorded
against the BookFile and the batch continues.

Implements FR-LIB-005
Signed-off-by: Jane Developer <jane@example.com>
```

```
fix(reader): restore exact scroll offset on reopen

Closes FR-READ-001
```

```
feat(ai)!: change IAiProvider.CompleteAsync signature

BREAKING CHANGE: providers must now return token usage for cost estimates.
Implements FR-AI-010
```

## Why

Conventional Commits drive the changelog (Keep a Changelog format), make history
auditable against the requirement baseline, and keep the Hybrid traceability
chain intact.
