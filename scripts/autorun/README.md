# Unattended plan driver (scripts/autorun)

Runs Claude Code or Codex sessions one after another with nobody present, for up to 48 hours. Each session does ONE phase of the nearest unfinished plan under `docs/plans`, commits it, and the driver pushes validated commits to `main` (fast-forward only). Plans the team writes later are found automatically. Design, reasons and platform set-up: `Unattended-Agent-Driver-Pattern.pdf`.

## Run it

```bash
scripts/autorun/run_plan.sh claude                 # up to 48 h, all plans
scripts/autorun/run_plan.sh claude 1000 accounts   # only plans whose folder path contains "accounts"
scripts/autorun/status.sh claude [watch]           # progress
touch ../_autorun/<repo>/logs/STOP                 # stop cleanly after the current session
bash scripts/autorun/selftest.sh                   # prove the driver itself works (fake agent, throwaway repos)
```

Git Bash (Windows 11), Terminal (macOS), any Bash (Ubuntu, Debian, Fedora). After a reboot run the same command: state is in git, the ledger and `../_autorun/<repo>/logs`.

## Needs

`claude` (or `codex`) signed in with your plan login (API keys in the shell are ignored on purpose); git with push access to `origin main`; bash 4+; GNU `timeout` and `date` (macOS: `brew install bash coreutils`); the repository's local test database or fixtures. Never point the driver at production.

## What happens each cycle

1. **Pull first.** Fetch `origin/main`. Fast-forward when nothing is local; otherwise take a safety branch `backup/autorun-<runner>-<time>`, rebase, and if that conflicts try a merge; if that conflicts too, a dedicated resolve session merges keeping both sides. Unresolvable: the merge is aborted, BLOCKED, nothing is lost.
2. **Session.** Fresh agent session, Sonnet class at medium effort (Opus class at high effort for one session after a failure). One phase, tests first, commits as it goes.
3. **Independent gates (driver, not model).** Commit-message rules; a commit guard (no keys, secrets, `.env`, keystores, files over 5 MB: hard stop); parse checks on changed PHP, JS, shell, Python and JSON files; optional `VERIFY_CMD` (the repository's own tests). A failed gate holds the push and feeds the failure into the next session; three failures in a row end the run.
4. **Publish.** Rebase onto `origin/main` and push, never forced.
5. **Measure.** One JSON line per session in `../_autorun/<repo>/logs/metrics-<runner>.jsonl` (duration, commits, files, exit, gate) and an exponentially weighted forecast of how many more sessions fit in the budget.

Usage limits are waited out (reset time from the log, else exponential back-off with jitter up to 6 h) and do not count as sessions. Ends on: budget spent, `STATUS: DONE` (no unlisted plan folder), `STATUS: BLOCKED`, STOP file, two empty or failed sessions, low disk.

## Tuning guide (how to improve it later)

| Lever | Where | Use it when |
|---|---|---|
| `EXTRA_PROMPT` | `autorun.conf` | The agent keeps missing a repo rule: add the rule once, in one sentence. |
| `VERIFY_CMD` | `autorun.conf` | After one supervised run proves the command passes on a clean checkout. Makes the driver, not the model, the judge of "tests pass". |
| `SESSION_TIMEOUT`, `CLAUDE_MODEL`, `ESCALATE_MODEL`, `CLAUDE_EFFORT` | env or conf | `metrics-*.jsonl` shows sessions timing out (raise) or finishing in minutes (lower effort, save tokens). |
| `PLAN_SKIP`, `COPY_PATHS`, `CLEANUP_CMD` | `autorun.conf` | A folder under `docs/plans` is not a plan; a git-ignored file is needed; a server must be stopped. |
| Plan quality | `PLAN-TEMPLATE.md` | The biggest lever: small phases, testable acceptance lines, explicit dependencies, a default for every open decision. |

Read the metrics each week: sessions per commit, minutes per session, gate failures. Change one lever, re-measure, keep it only if the numbers improve. Add a rule to the driver only after a failure has shown the need, and add a scenario to `selftest.sh` for it.

## Safety

The model may not push, merge, rebase, stash, reset --hard, clean or switch branches; the driver does all publishing. The model must not touch production, deploy, spend, publish or contact people. Work happens in `../_autorun/<repo>/wt-<runner>` built from `origin/main`; your own checkout and uncommitted work are never touched.

Several repositories running together share one plan allowance: run the most valuable first.
