# Ogma Kaizen runner (Jidoka, JAD)

This is Ogma's **Jidoka** installation: JAD, the *Jidoka Automated Development* tool. It works
unattended, stops the line on a defect (failed gate, `REWORK`, `BLOCKED`) and never publishes
defective work. The engine's canonical Jidoka lives in `chwezi-development-engine/auto-agents/`.

An unattended, metered driver that executes the remaining phases of
`docs/plans/sept-23-kaizen`. It is specific to this repository and separate from
the generic `scripts/autorun` driver, which it does not use or change.

Authorised by the owner on 2026-11-19: an orchestrator model plans and reviews,
an executor model implements, and every model call is measured for the
token-economy paper. The models per role are set only in `config.json`
(`roles`); this is an owner-directed exception to the "Class S case: none" line
in `CLAUDE.md` and lasts for this run.

## Control arm (owner, 2026-11-19)

Six of the sixteen phases not yet started when the decision was made (40%, rounded
down) run with the standard class planning and reviewing and the normal class
executing, under the same driver, gates and acceptance rule, so the paper can
compare model combinations on one repository. They were fixed before any of them
ran, as the second member of six like-for-like pairs: 11/**12**, 14/**15**,
19/**20**, 21/**22**, 23/**24**, 25/**26** (control in bold). Phases 16, 18, 27
and 28 stay on the default roles, unpaired. The mapping is `arm` in
`phases.json` and `arms` in `config.json`; every session and phase row records
its arm.

## What one phase does

1. **Plan** (orchestrator, read-only): verifies the phase's premises against the
   code as it is, then writes a work order of at most eight slices.
2. **Execute** (executor): one fresh session per slice, committing as it goes.
3. **Gates** (the driver itself, no model): clean tree, requirement
   accountability, locked restore, format, Release build, vulnerable-package
   scan, analyzers, the fast test suite, and the shelf3d gates when
   `src/shelf3d` changed.
4. **Review** (orchestrator, read-only, independent): `ACCEPT`, `REWORK` or
   `BLOCKED`.
5. **Rework** (executor): at most six sessions per phase, for a failed gate or
   a rejected review.
6. **Publish** (the driver): commit guard for secrets and oversized files, then
   a non-forced `git push origin HEAD:main`. Agents cannot push, merge, rebase,
   stash, hard-reset, clean or switch branches.

A blocked phase keeps its branch `kaizen/phase-NN` unmerged and its dependents
are skipped. Checks needing a Mac, a signing certificate, a live provider or
people are recorded `NOT_ASSESSED` and never block the doable work. Phases run
one at a time; subagents are disabled so each token is attributed to one role.

## Use

```powershell
./scripts/kaizen-runner/run.ps1 -Action SelfTest   # fake agent, no model calls
./scripts/kaizen-runner/run.ps1                    # start detached
./scripts/kaizen-runner/run.ps1 -Action Status
./scripts/kaizen-runner/run.ps1 -Action Watch
./scripts/kaizen-runner/run.ps1 -Action Stop       # stops after the current session
```

Starting again resumes from `state.json` (phase, stage and slice). To retry a
blocked phase, set its `status` back to `todo` in `state.json` while stopped.

**Restart after a crash or reboot.** On the build PC the scheduled task "Autorun ensure runners"
runs `_autorun/ensure-runners.sh` every 30 minutes. It restarts this runner when `runner.lock`
names a process that is no longer Python, and leaves it alone when `HOLD` or `STOP` exists in the
work root or when there is no `runner.lock` (a clean finish; a person decides).

## Where things are

Work root: `C:\wamp64\www\_autorun\Ogma-Library\kaizen\` (outside the repository).

| Path | Content |
|---|---|
| `wt/` | The git worktree the agents work in |
| `state.json` | Phase status, stage, slice, rework count |
| `driver.log` | Driver events |
| `logs/` | Raw session streams and gate output (stay local; never mirrored) |
| `work-orders/` | Work orders and review findings (stay local) |
| `metrics/` | The measurement set below |

## Metrics

Numbers and identifiers only: no prompt, code or transcript text. After every
session the folder is mirrored to the paper's
`working/data/ogma-kaizen-runner/`.

| File | One row per | Fields |
|---|---|---|
| `calls.csv` | model call (deduplicated by message ID; final usage read from the session transcript) | The paper's `calls.csv` schema (`corpus, mode, session, is_subagent, agent_file, ts_utc, model, family, input, output, cw_5m, cw_1h, cache_read, context, speed, geo`) plus `run_id, phase, role, slice, seq` |
| `sessions.jsonl` | session | role, requested model and effort, wall and API time, turns, outcome, token totals, thinking tokens, first and maximum context, CLI cost, per-model usage, commits and lines changed, permission denials |
| `gates.jsonl` | gate invocation | phase, round, commit, seconds, exit code, pass, test counts |
| `phases.jsonl` | phase outcome | verdict, sessions, rework sessions, gate rounds, slices, commits, lines, cost |
| `limits.jsonl` | usage-limit episode | first denial, wait and its basis (provider estimate or backoff), streak |
| `events.jsonl` | driver event | timeline, including the first success after a limit |
| `run-meta.jsonl` | driver start | CLI version, runner digest and revision, role configuration, instruction-file sizes |
| `summary.md`, `summary-by-phase-role.csv` | | Rebuilt totals |

`cost_usd_cli` is the CLI's list-price figure; the sessions run on the plan
login, so it is a notional cost, not a bill. The session IDs match the
transcripts under `~/.claude/projects/` (directory hint in `run-meta.jsonl`), so
the paper's `extract.py` can cross-check the same sessions.

## Limits of this tool

- Usage limits are waited out (provider reset time when given, otherwise
  backoff with jitter) and are not counted as sessions. The allowance is shared
  with any other runner on this machine.
- Real-window journeys need an unlocked desktop. On a locked desktop the agents
  must record them `NOT_ASSESSED`.
- The reviewer is a model. It is independent of the executor but is not the
  human sign-off the Product Excellence Standard requires for a release.
