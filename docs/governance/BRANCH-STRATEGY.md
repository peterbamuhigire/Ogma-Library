# Branch Strategy

Ogma Library uses a trunk-with-integration model. `main` is always releasable
and only ever carries signed, released commits.

## Branches

| Branch | Purpose | Rules |
| --- | --- | --- |
| `main` | Signed releases only. | No direct commits. Only fast-forward-free merges from `release/*` and `hotfix/*`. Protected. Every commit on `main` corresponds to a tagged, signed build. |
| `develop` | Integration of completed work. | PRs merge here after review. Squash or merge commits; each must build and pass tests on its own. |
| `feature/<phase-ID>-<slug>` | One story or one defect. | Branch from `develop`. Example: `feature/LIB-003-content-hash-identity`. Rebase on `develop` before opening a PR so history stays linear. Deleted after merge. |
| `release/<semver>` | Stabilization for a release. | Branch from `develop` when scope is frozen. Only release-blocking fixes. Merges to `main` (tag + sign) and back to `develop`. |
| `hotfix/<semver>` | Urgent fix to a released version. | Branch from `main`. Merges to `main` (tag + sign) and `develop`. |

## Naming

- `feature/<requirement-or-phase-ID>-<short-slug>` — e.g. `feature/READ-004-page-turn-cache`, `feature/P03-design-tokens`.
- `fix/<ID>-<slug>` for defect fixes; `chore/<slug>` for tooling/build.

## Rules

- Keep a branch focused on one story or defect.
- Each commit builds and passes tests on its own; do not commit a broken
  intermediate state.
- Every PR links the requirement IDs it implements and passes the Change Impact
  Analysis checklist (`CIA-WORKFLOW.md`).
- A change that alters a baselined FR/NFR/control requires a CIA entry with a
  rollback plan and owner sign-off before merge.
- No one approves their own PR; at least one approving review is required.

## Relationship to release channels

`develop` builds feed the **Dev** channel; promotion to **Alpha → Beta →
Stable** is by re-pointing the already-signed artifact, never a rebuild (see the
Deployment & Operations guide and ADR-0009).
