# Unattended plan driver (scripts/autorun)

Runs Claude Code or Codex sessions one after another, with nobody present, for up to 48 hours. Each session does ONE phase of the nearest unfinished plan under `docs/plans`, commits it, and the driver pushes to `main` (fast-forward only). Plans written later by the team are found automatically. The full design, the reasons and the platform set-up are in `Unattended-Agent-Driver-Pattern.pdf`.

## Run it

```bash
scripts/autorun/run_plan.sh claude            # up to 48 h, all plans
scripts/autorun/run_plan.sh claude 1000 accounts   # only plans whose folder path contains "accounts"
scripts/autorun/status.sh claude              # progress (add: watch)
touch ../_autorun/<repo>/logs/STOP            # stop cleanly after the current session
```

Run it in Git Bash (Windows 11), Terminal (macOS), or any Bash (Ubuntu, Debian, Fedora). After a reboot, run the same command: all state is in git, the ledger and `../_autorun/<repo>/logs`.

## What you need

- `claude` (or `codex`) installed and signed in with your plan login. Any API key in the shell is ignored on purpose.
- Git with push access to `origin main`, `bash` 4+, GNU `timeout` and `date`. macOS: `brew install bash coreutils` and run with the Homebrew bash first on PATH.
- The repository's local test database or fixtures, as its own docs describe. Never point the driver at production.

## How it behaves

- Works in its own worktree `../_autorun/<repo>/wt-claude` on branch `auto/claude`, built from `origin/main`. Your own checkout and uncommitted work are never touched.
- Keeps a small ledger `docs/plans/AUTORUN.md` (the first session surveys the plans and writes it). A plan folder not listed in the ledger is registered next, even after the ledger said DONE.
- A blocked plan is marked in the ledger and skipped, so one open decision does not stop the run.
- The model is denied `git push`; the driver checks commit messages and pushes. Sonnet-class executes; one session on the expert class follows a failure.
- Usage limits are waited out (reset time read from the log, else back-off up to 6 h) without counting as a session.
- Ends when: the budget is spent, `STATUS: DONE`, `STATUS: BLOCKED`, a STOP file, two empty or failed sessions in a row, or low disk space.

## Settings

Edit `scripts/autorun/autorun.conf` (all optional). Add new non-plan folders to `PLAN_SKIP`. Several repositories running together share one plan allowance, so run the repositories you care about most first.

## Review

`git log --oneline -30 origin/main` lists what was pushed. Anything the tests could not check is recorded as NOT_ASSESSED in the commit body.
