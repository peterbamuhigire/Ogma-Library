#!/usr/bin/env bash
# Unattended plan driver for repositories that keep their plans under docs/plans. Built for long runs: 48 hours by default.
# Usage: scripts/autorun/run_plan.sh [claude|codex] [max_sessions=1000] [plan_filter]
#   plan_filter (optional): only plans whose folder path contains this text, for example area-03 or numbering.
#
# What it does: starts one fresh agent session per phase. Each session reads the small ledger docs/plans/AUTORUN.md,
# continues the active plan or picks the nearest unfinished plan under docs/plans (new plans written later are found
# automatically), implements ONE phase, commits, and updates the ledger. After every session the DRIVER (not the
# model) checks the new commits, rebases onto origin/main and pushes them to main (fast-forward only, never forced).
# The model is denied git push. Works in its own git worktree on branch auto/<runner>; your checkout is never touched.
# Restartable: all state is in git, the ledger and the log folder, so after a reboot run the same command again.
#
# Repository knobs live in scripts/autorun/autorun.conf (optional). Environment knobs (defaults in brackets):
#   MAX_HOURS [48]  SESSION_TIMEOUT [3h]  CLAUDE_MODEL [sonnet]  ESCALATE_MODEL [opus]  CLAUDE_EFFORT [medium]
#   ESCALATE_EFFORT [high]  DAILY_SESSION_CAP [0=off]  LIMIT_WAIT_MIN [30]  LIMIT_WAIT_MAX_MIN [360]
#   LIMIT_MARGIN_SEC [120]  NET_WAIT_MAX_MIN [120]  MIN_FREE_GB [15]  LOG_KEEP_DAYS [4]  LOG_MAX_MB [500]
#   NOTIFY_CMD []  CLEANUP_CMD []  COPY_PATHS []  PLAN_SKIP []  EXTRA_PROMPT []  CLAUDE_EXTRA_ARGS []
#   AUTORUN_HOME [<parent of repo>/_autorun]  LEDGER [docs/plans/AUTORUN.md]
# ANTHROPIC_API_KEY, ANTHROPIC_AUTH_TOKEN and OPENAI_API_KEY are always ignored: sessions use the plan login.
set -u
RUNNER="${1:-}"
MAX_SESSIONS="${2:-1000}"
PLAN_FILTER="${3:-${PLAN_FILTER:-}}"
case "$RUNNER" in claude | codex) ;; *) echo "usage: $0 <claude|codex> [max_sessions] [plan_filter]" >&2; exit 64 ;; esac
MAIN_REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
[ -f "$MAIN_REPO/scripts/autorun/autorun.conf" ] && . "$MAIN_REPO/scripts/autorun/autorun.conf"
MAX_HOURS="${MAX_HOURS:-48}"
SESSION_TIMEOUT="${SESSION_TIMEOUT:-3h}"
CLAUDE_MODEL="${CLAUDE_MODEL:-sonnet}"
ESCALATE_MODEL="${ESCALATE_MODEL:-opus}"
CLAUDE_EFFORT="${CLAUDE_EFFORT:-medium}"
ESCALATE_EFFORT="${ESCALATE_EFFORT:-high}"
CLAUDE_EXTRA_ARGS="${CLAUDE_EXTRA_ARGS:-}"
DAILY_SESSION_CAP="${DAILY_SESSION_CAP:-0}"
LIMIT_WAIT_MIN="${LIMIT_WAIT_MIN:-30}"
LIMIT_WAIT_MAX_MIN="${LIMIT_WAIT_MAX_MIN:-360}"
LIMIT_MARGIN_SEC="${LIMIT_MARGIN_SEC:-120}"
NET_WAIT_MAX_MIN="${NET_WAIT_MAX_MIN:-120}"
MIN_FREE_GB="${MIN_FREE_GB:-15}"
NOTIFY_CMD="${NOTIFY_CMD:-}"
CLEANUP_CMD="${CLEANUP_CMD:-}"
COPY_PATHS="${COPY_PATHS:-}"
PLAN_SKIP="${PLAN_SKIP:-}"
EXTRA_PROMPT="${EXTRA_PROMPT:-}"
LOG_KEEP_DAYS="${LOG_KEEP_DAYS:-4}"
LOG_MAX_MB="${LOG_MAX_MB:-500}"
LEDGER="${LEDGER:-docs/plans/AUTORUN.md}"
VERIFY_CMD="${VERIFY_CMD:-}"            # optional independent gate run by the driver in the worktree before each push
VERIFY_TIMEOUT="${VERIFY_TIMEOUT:-40m}"
GATE_MAX_FAIL="${GATE_MAX_FAIL:-3}"     # consecutive gate failures before the run is BLOCKED
MAX_FILE_MB="${MAX_FILE_MB:-5}"         # a commit adding a larger file is refused
EWMA_ALPHA="${EWMA_ALPHA:-0.3}"         # weight of the newest session in the throughput estimate
MAX_SECS=$((MAX_HOURS * 3600))
REPO_NAME="$(basename "$MAIN_REPO")"
AUTORUN_HOME="${AUTORUN_HOME:-$(dirname "$MAIN_REPO")/_autorun}"
WORKTREE="$AUTORUN_HOME/$REPO_NAME/wt-$RUNNER"
LOG_DIR="$AUTORUN_HOME/$REPO_NAME/logs"
BRANCH="auto/$RUNNER"
STOP_FILE="$LOG_DIR/STOP"
LOCK_DIR="$LOG_DIR/lock-$RUNNER"
mkdir -p "$LOG_DIR"
SUMMARY="$LOG_DIR/summary-$RUNNER.log"
STARTS_FILE="$LOG_DIR/starts-$RUNNER.txt"
STATUS_FILE="$LOG_DIR/status-$RUNNER.txt"
touch "$SUMMARY"
START_EPOCH="$(date +%s)"
STATE="starting"; SESSION_NO=0; CUR_MODEL=""; pushed_total=0

# ---- portability: GNU timeout and GNU date are required (macOS: brew install coreutils, see the README) ----
if ! command -v timeout >/dev/null 2>&1; then
  if command -v gtimeout >/dev/null 2>&1; then timeout() { gtimeout "$@"; }; fi
fi
if ! date -d '+1 second' +%s >/dev/null 2>&1; then
  if command -v gdate >/dev/null 2>&1; then date() { gdate "$@"; }; fi
fi
missing=""
for c in git awk sed grep find tail head df du; do command -v "$c" >/dev/null 2>&1 || missing="$missing $c"; done
command -v timeout >/dev/null 2>&1 || missing="$missing timeout(GNU coreutils)"
date -d '+1 second' +%s >/dev/null 2>&1 || missing="$missing date(GNU coreutils)"
command -v "$RUNNER" >/dev/null 2>&1 || missing="$missing $RUNNER(not installed or not on PATH)"
if [ -n "$missing" ]; then echo "missing required tools:$missing (see scripts/autorun/README.md)" >&2; exit 69; fi

write_status() {
  local up=$(( $(date +%s) - START_EPOCH ))
  {
    echo "repo:     $REPO_NAME   runner: $RUNNER (pid $$)${PLAN_FILTER:+   filter: $PLAN_FILTER}"
    echo "updated:  $(date '+%F %T')"
    echo "started:  $(date -d "@$START_EPOCH" '+%F %T'), $((up / 3600)) h $(((up % 3600) / 60)) min in, budget $MAX_HOURS h"
    echo "session:  $SESSION_NO of $MAX_SESSIONS${CUR_MODEL:+ (model $CUR_MODEL)}"
    echo "pushed:   $pushed_total commit(s) this run"
    echo "state:    $STATE"
  } >"$STATUS_FILE"
}
say() { printf '%s %s\n' "$(date '+%F %T')" "$*" | tee -a "$SUMMARY"; write_status; }
tidy() {
  local oldest
  find "$LOG_DIR" -maxdepth 1 -type f \( -name "$RUNNER-*.log" -o -name "preflight-$RUNNER.log" \) -mtime +"$LOG_KEEP_DAYS" -delete 2>/dev/null
  while [ "$(du -sm "$LOG_DIR" 2>/dev/null | cut -f1)" -gt "$LOG_MAX_MB" ]; do
    oldest="$(ls -1tr "$LOG_DIR/$RUNNER"-*.log 2>/dev/null | head -n 1)"
    [ -z "$oldest" ] && break
    rm -f "$oldest"
  done
  if [ "$(wc -l <"$SUMMARY" 2>/dev/null || echo 0)" -gt 5000 ]; then
    tail -n 3000 "$SUMMARY" >"$SUMMARY.tmp" && mv "$SUMMARY.tmp" "$SUMMARY"
  fi
  if [ -f "$STARTS_FILE" ]; then
    awk -v c="$(( $(date +%s) - 172800 ))" '$1 > c' "$STARTS_FILE" >"$STARTS_FILE.tmp" && mv "$STARTS_FILE.tmp" "$STARTS_FILE"
  fi
  return 0
}
notify() { [ -n "$NOTIFY_CMD" ] && NOTIFY_MSG="$*" bash -c "$NOTIFY_CMD" >/dev/null 2>&1; return 0; }

# ---- one instance per runner; a lock left by a crash or reboot is taken over when its process is gone ----
if ! mkdir "$LOCK_DIR" 2>/dev/null; then
  oldpid="$(cat "$LOCK_DIR/pid" 2>/dev/null)"
  if [ -n "$oldpid" ] && kill -0 "$oldpid" 2>/dev/null; then
    say "another $RUNNER driver (pid $oldpid) holds $LOCK_DIR"; exit 1
  fi
  say "stale lock (pid ${oldpid:-unknown} is not running), taking it over"
  rm -rf "$LOCK_DIR"; mkdir "$LOCK_DIR" || exit 1
fi
echo "$$" >"$LOCK_DIR/pid"
if [ -f "$STOP_FILE" ]; then say "removing a leftover STOP file from an earlier run"; rm -f "$STOP_FILE"; fi
tidy

# ---- keep the machine awake while the driver runs ----
KEEPAWAKE_PID=""
case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*)
    if command -v powershell.exe >/dev/null 2>&1; then
      powershell.exe -NoProfile -Command '
        Add-Type -Namespace W -Name P -MemberDefinition "[System.Runtime.InteropServices.DllImport(\"kernel32.dll\")] public static extern uint SetThreadExecutionState(uint f);"
        [W.P]::SetThreadExecutionState(0x80000001) | Out-Null
        while ($true) { Start-Sleep -Seconds 60 }' >/dev/null 2>&1 &
      KEEPAWAKE_PID=$!
    fi ;;
  Darwin) if command -v caffeinate >/dev/null 2>&1; then caffeinate -i -w $$ >/dev/null 2>&1 & KEEPAWAKE_PID=$!; fi ;;
  Linux)
    if command -v systemd-inhibit >/dev/null 2>&1; then
      systemd-inhibit --what=sleep:idle --who=autorun --why="unattended agent run" sleep $((MAX_SECS + 7200)) >/dev/null 2>&1 &
      KEEPAWAKE_PID=$!
    fi ;;
esac
cleanup() {
  [ -n "$KEEPAWAKE_PID" ] && kill "$KEEPAWAKE_PID" 2>/dev/null
  rm -rf "$LOCK_DIR" 2>/dev/null
}
trap cleanup EXIT
trap 'exit 130' INT TERM

# ---- network helpers (defined early: the worktree set-up needs them) ----
nap() {
  local s="$1" c
  while [ "$s" -gt 0 ]; do
    [ -f "$STOP_FILE" ] && return 1
    c=60; [ "$s" -lt 60 ] && c="$s"
    sleep "$c"; s=$((s - c))
  done
  return 0
}
wait_for_network() {
  local waited=0
  until git ls-remote --exit-code origin HEAD >/dev/null 2>&1; do
    [ "$waited" -ge $((NET_WAIT_MAX_MIN * 60)) ] && return 1
    [ -f "$STOP_FILE" ] && return 1
    sleep 60; waited=$((waited + 60))
  done
}
fetch_main() {
  git fetch origin main >/dev/null 2>&1 && return 0
  say "git fetch failed; waiting for the network"
  wait_for_network || return 1
  git fetch origin main >/dev/null 2>&1
}

# ---- worktree from origin/main (the human's uncommitted work is not needed and is never touched) ----
if [ ! -d "$WORKTREE" ]; then
  git -C "$MAIN_REPO" fetch origin main >/dev/null 2>&1 || { say "cannot fetch origin/main"; exit 1; }
  [ -n "$(git -C "$MAIN_REPO" status --porcelain)" ] && say "note: the main checkout has uncommitted work; it is ignored, sessions start from origin/main"
  [ "$(git -C "$MAIN_REPO" rev-list --count origin/main..main 2>/dev/null || echo 0)" -gt 0 ] && say "note: local main has commits not on origin/main; the driver works from origin/main"
  mkdir -p "$(dirname "$WORKTREE")"
  git -C "$MAIN_REPO" worktree add -B "$BRANCH" "$WORKTREE" origin/main || { say "cannot create worktree"; exit 1; }
  # Copy (never link) git-ignored essentials: environment files and dependency folders.
  for p in .env .env.* vendor node_modules $COPY_PATHS; do
    for f in "$MAIN_REPO"/$p; do
      [ -e "$f" ] || continue
      n="$(basename "$f")"
      git -C "$MAIN_REPO" check-ignore -q "$n" 2>/dev/null || continue
      [ -e "$WORKTREE/$n" ] || cp -r "$f" "$WORKTREE/$n"
    done
  done
fi
cd "$WORKTREE" || exit 1
export AUTORUN=1
if [ -n "${ANTHROPIC_API_KEY:-}${ANTHROPIC_AUTH_TOKEN:-}${OPENAI_API_KEY:-}" ]; then
  say "an API key is set in this shell; ignoring it so sessions use the normal plan login"
fi
unset ANTHROPIC_API_KEY ANTHROPIC_AUTH_TOKEN OPENAI_API_KEY
if [ "$RUNNER" = "claude" ]; then TRAILER="Claude <noreply@anthropic.com>"; else TRAILER="Codex <noreply@openai.com>"; fi

# ---- quality gates run by the DRIVER (independent of what the model claims) ----
GATE_FEEDBACK=""; gate_fail_streak=0; syntax_skipped=""
changed_files() { git diff --name-only --diff-filter=AM origin/main..HEAD; }
guard_commits() { # hard stop: secrets, key material, oversized files. Prints counts only, never the secret.
  local f bad="" n
  while IFS= read -r f; do
    case "$f" in *.env.example | *.env.sample | *.env.dist) continue ;; esac
    if [[ "$f" =~ (^|/)\.env($|\.) ]] || [[ "$f" =~ \.(jks|keystore|pem|p12|pfx)$ ]] || [[ "$f" =~ (^|/)id_(rsa|ed25519)$ ]]; then bad="$bad forbidden-file:$f"; fi
    if [ -f "$f" ] && [ -n "$(find "$f" -size +"${MAX_FILE_MB}"M 2>/dev/null)" ]; then bad="$bad oversized:$f"; fi
  done < <(changed_files)
  n="$(git diff origin/main..HEAD | grep -E '^\+' | grep -cE 'BEGIN ([A-Z]+ )?PRIVATE KEY|AKIA[0-9A-Z]{16}|sk-ant-[A-Za-z0-9_-]{20,}|gh[pousr]_[A-Za-z0-9]{30,}|xox[baprs]-[A-Za-z0-9-]{10,}')"
  [ "${n:-0}" -gt 0 ] && bad="$bad secret-like-lines:$n"
  [ -z "$bad" ] && return 0
  say "STATUS: BLOCKED - commit guard refused the push:$bad (a person must clean the history)"; return 1
}
syntax_gate() { # cheap deterministic parse checks on the files this batch changed
  local f out="" r
  while IFS= read -r f; do
    [ -f "$f" ] || continue
    r=0
    case "$f" in
      *.php) if command -v php >/dev/null 2>&1; then php -l "$f" >/dev/null 2>&1 || r=1; else syntax_skipped="$syntax_skipped php"; fi ;;
      *.sh) bash -n "$f" >/dev/null 2>&1 || r=1 ;;
      *.js | *.mjs | *.cjs) if command -v node >/dev/null 2>&1; then node --check "$f" >/dev/null 2>&1 || r=1; else syntax_skipped="$syntax_skipped node"; fi ;;
      *.py) command -v python3 >/dev/null 2>&1 && { python3 -m py_compile "$f" >/dev/null 2>&1 || r=1; } ;;
      *.json) command -v python3 >/dev/null 2>&1 && { python3 -c 'import json,sys;json.load(open(sys.argv[1],encoding="utf-8"))' "$f" >/dev/null 2>&1 || r=1; } ;;
    esac
    [ "$r" -eq 1 ] && out="$out $f"
  done < <(changed_files)
  [ -z "$out" ] && return 0
  GATE_FEEDBACK="Parse check failed in:$out. Fix these files first."; return 1
}
verify_gate() {
  [ -z "$VERIFY_CMD" ] && return 0
  local o; o="$(timeout -k 30 "$VERIFY_TIMEOUT" bash -c "$VERIFY_CMD" 2>&1)" && return 0
  GATE_FEEDBACK="The independent check '$VERIFY_CMD' failed. Last output: $(echo "$o" | tail -n 25 | cut -c1-300 | tr '\n' '|'). Fix the cause before any new work."; return 1
}
run_gates() { # 0 = pass; 1 = hold the push and feed back; 2 = hard stop
  guard_commits || return 2
  GATE_FEEDBACK=""
  syntax_gate && verify_gate && return 0
  return 1
}
# Throughput model: exponentially weighted moving average of session length, used to forecast what fits in the budget.
EWMA_SECS=0
update_ewma() { EWMA_SECS="$(awk -v a="$EWMA_ALPHA" -v x="$1" -v e="$EWMA_SECS" 'BEGIN{ if (e==0) printf "%d", x; else printf "%d", a*x+(1-a)*e }')"; }
forecast() {
  local left=$((MAX_SECS - ($(date +%s) - START_EPOCH)))
  [ "$EWMA_SECS" -gt 0 ] && echo "forecast: about $((left / (EWMA_SECS + 60))) more session(s) fit in the remaining $((left / 3600)) h (EWMA $((EWMA_SECS / 60)) min per session)"
  return 0
}
metric() { # metric <session> <model> <secs> <commits> <files> <exit> <gate>
  printf '{"t":%s,"session":%s,"model":"%s","secs":%s,"commits":%s,"files":%s,"exit":%s,"gate":"%s"}\n' "$(date +%s)" "$1" "$2" "$3" "$4" "$5" "$6" "$7" >>"$LOG_DIR/metrics-$RUNNER.jsonl"
}

# ---- plan discovery used by the driver (the agent does the real selection; this only detects new plans) ----
is_support() {
  case "$1" in
    archive | prompts | state | tools | _* | .* | contracts | review | reviews | evidence | templates | assets | screenshots | images) return 0 ;;
  esac
  case " $PLAN_SKIP " in *" $1 "*) return 0 ;; esac
  return 1
}
has_files() { [ -n "$(find "$1" -type f -name '*.md' 2>/dev/null | head -n 1)" ]; }
candidate_plans() {
  local d b sub sb n
  for d in docs/plans/*/; do
    d="${d%/}"; b="${d##*/}"
    is_support "$b" && continue
    n=0; for sub in "$d"/area-*/; do [ -d "$sub" ] && n=$((n + 1)); done
    if [ "$n" -ge 2 ]; then
      for sub in "$d"/*/; do
        sub="${sub%/}"; sb="${sub##*/}"
        is_support "$sb" && continue
        has_files "$sub" && echo "$sub"
      done
    else
      has_files "$d" && echo "$d"
    fi
  done
}
unregistered_plans() {
  local p
  while IFS= read -r p; do
    [ -n "$PLAN_FILTER" ] && case "$p" in *"$PLAN_FILTER"*) ;; *) continue ;; esac
    grep -qF "${p##*/}" "$LEDGER" 2>/dev/null || echo "$p"
  done < <(candidate_plans)
}

FILTER_TEXT=""
[ -n "$PLAN_FILTER" ] && FILTER_TEXT=" Consider only plans whose folder path contains '$PLAN_FILTER'."
PROMPT="You are running unattended on branch $BRANCH in $WORKTREE. Nobody can answer questions: decide from the plan or mark it blocked. ENTRY: read this repository's CLAUDE.md or AGENTS.md router section, then the ledger $LEDGER (create it if missing, see LEDGER). Use the Chwezi skill engines your user-level CLAUDE.md names: read the engine router section, then one skill for the task (development engine for code, design engine for UI, finance engine for accounting); search instead of loading. Follow the repository's conventions and the plan's recorded decisions. PLAN SELECTION (plans live under docs/plans; the team adds new ones at any time).$FILTER_TEXT List folder names only with a quick directory listing. Plan units are folders directly under docs/plans, and the area-NN folders inside a container such as area-plans; skip archive, prompts, state, tools, contracts, review and evidence folders. If the ledger names an Active plan, continue it. Otherwise choose the nearest unfinished plan: first one already part done, else the first by natural name order (area-01 before area-02), always honouring the plan's own dependency graph or stated prerequisites. Decide done or not-done from the plan's own state files (RESUME.md, handoff, execution.json, STATUS.md, phase status) and git log; never redo finished work. If the ledger is missing, survey first: one line per plan (todo, active, done, blocked, skip) with evidence, no implementation in that session beyond the survey. If STATUS: DONE is in the ledger but a plan folder is not listed in it, remove that line and register the new plan. LEDGER FORMAT: under 4 KB. Line 1 may be STATUS: DONE or STATUS: BLOCKED - reason. Then Updated date, Active plan path, Active next (one line: phase and the file to read first), and a table with rows of the form: | plan path | state | one-line note |. WORK: do exactly ONE phase (the plan's smallest deliverable unit) per session, completely and to the plan's acceptance criteria, in the repository's architecture. Commit each coherent slice as you go so a cut-off session loses nothing. AUTHORITY: you may edit the repository, install project dependencies, run tests and builds, and use the disposable or local test database the repository documents. You may NOT run git push, merge, rebase, stash, reset --hard, clean or switch branches (the driver pushes), and you may NOT touch production, deploy, use real third-party credentials, spend money, publish, or contact people. An open owner decision: apply the plan's stated default and record it; with no default, set that plan to blocked in the ledger with the reason and continue with the next plan in this same session. CLEAN UP: delete every scratch or temporary file you created outside version control and stop everything you started, so only commits remain. TOKENS: read the ledger and the active plan's handover file first, then only files the phase names; keep tool output short; run targeted tests, never the full gate unless the phase requires it; anything not run is NOT_ASSESSED, never implied passed. If the working tree is dirty at the start, an earlier session was cut off: inspect git status and git diff, finish or commit that work first. COMMITS (the driver refuses to push otherwise): subject '[module] type: Imperative summary' or 'type(scope): Imperative summary', type one of feature, feat, bugfix, fix, refactor, docs, test, chore, perf, build, ci, at least 15 characters after the colon; blank line; body of at least two sentences (what and why, files or tables that matter, tests run, what is NOT_ASSESSED); blank line; then the line 'Co-Authored-By: $TRAILER'. Follow any stricter commit rule in the repository's own instructions. END: update the ledger and the plan's handover file, commit them, leave the tree clean. Write STATUS: DONE at the top of the ledger only when every plan is done or skip and a fresh listing shows no unlisted plan folder. Write STATUS: BLOCKED - reason only when every remaining plan is blocked or a repository-wide blocker stops all work (test environment unavailable, or two failed acceptance checks on the same phase). SYNC AND CONFLICTS: the driver pulls origin/main before every session. If git status shows a merge in progress or conflict markers, resolve every conflict by keeping the intent of both sides, run the affected tests, git add the files and finish with git commit --no-edit. Never discard work: no reset --hard, no checkout of files over changes, no clean, no deleting a side wholesale. If you cannot resolve a conflict safely, leave the merge as it is and say so in the ledger. ENGINEERING STANDARD (apply to every phase): write or extend an automated test for each behaviour you add or change, and run it before you commit. State the invariants a change must preserve (tenant isolation on every query, money in integer minor units or exact decimals, idempotent migrations and retries, contiguous per-tenant document numbers allocated inside the save transaction, permission checks on the server) and test each. Prefer the simplest correct algorithm and note its time and space cost where data grows; avoid N+1 queries, unbounded loops and unindexed lookups. Treat auth, payments, ledgers and tenancy changes as high risk: threat-model briefly and add negative tests. Keep changes small, reversible and consistent with the repository architecture; never weaken a test to pass it. If an independent check failed last session its output is appended after this prompt: fix that first. ${EXTRA_PROMPT}"

hit_limit() { tail -n 60 "$1" 2>/dev/null | grep -qiE 'usage limit|rate limit|rate_limit|limit reached|too many requests|overloaded|quota|resets? (at|in)|HTTP 429|status.?429'; }
fatal_error() { tail -n 40 "$1" 2>/dev/null | grep -qiE 'credit balance is too low|invalid api key|not logged in|please run /login|authentication[_ ]error|http 401|status.?401'; }
limit_wait_secs() {
  local t now target add=86400
  t="$(tail -n 60 "$1" 2>/dev/null | grep -ioE 'resets? (at )?([A-Za-z]{3,9},? )?([A-Za-z]{3,9} [0-9]{1,2},? )?[0-9]{1,2}(:[0-9]{2})? ?(am|pm)' | tail -n 1 | sed -E 's/^[Rr][Ee][Ss][Ee][Tt][Ss]? ([Aa][Tt] )?//')"
  [ -z "$t" ] && return 1
  target="$(date -d "$t" +%s 2>/dev/null)" || return 1
  now="$(date +%s)"
  case "$t" in [Mm]on* | [Tt]ue* | [Ww]ed* | [Tt]hu* | [Ff]ri* | [Ss]at* | [Ss]un*) add=604800 ;; esac
  [ "$target" -le "$now" ] && target=$((target + add))
  [ $((target - now)) -gt $((8 * 86400)) ] && return 1
  echo $((target - now + LIMIT_MARGIN_SEC))
}
RESOLVE_MODE=0
sync_main() { # pull origin/main first, always; never discard local work
  fetch_main || { say "STATUS: BLOCKED - the remote is unreachable"; return 1; }
  [ -n "$(git status --porcelain)" ] && return 0                 # cut-off work or a merge in progress: leave it for the agent
  local behind ahead
  behind="$(git rev-list --count HEAD..origin/main)"; ahead="$(git rev-list --count origin/main..HEAD)"
  [ "$behind" -eq 0 ] && return 0
  if [ "$ahead" -eq 0 ]; then git merge -q --ff-only origin/main >/dev/null 2>&1 && { say "pulled $behind commit(s) from origin/main"; return 0; }; fi
  git branch -f "backup/autorun-$RUNNER-$(date +%Y%m%d-%H%M%S)" HEAD >/dev/null 2>&1   # safety ref: nothing can be lost
  if git rebase origin/main >/dev/null 2>&1; then say "rebased $ahead local commit(s) onto $behind new commit(s) from origin/main"; return 0; fi
  git rebase --abort >/dev/null 2>&1
  if git merge --no-edit origin/main >/dev/null 2>&1; then say "merged origin/main (rebase conflicted, merge was clean)"; return 0; fi
  RESOLVE_MODE=1
  say "origin/main conflicts with local work; a resolve session will merge it (backup branch kept)"
  return 0
}
first_bad=""
check_commits() {
  local sha subject body re='^(\[[A-Za-z0-9_-]+\] )?(feature|feat|bugfix|fix|refactor|docs|test|chore|perf|build|ci)(\([A-Za-z0-9_./-]+\))?!?: .{15,}$'
  first_bad=""
  for sha in $(git rev-list --reverse origin/main..HEAD); do
    [ "$(git rev-list --parents -n 1 "$sha" | wc -w)" -gt 2 ] && continue   # merge commit made by the driver
    subject="$(git log -1 --format=%s "$sha")"
    body="$(git log -1 --format=%b "$sha" | grep -v '^Co-Authored-By:' | tr -d '\n')"
    if ! [[ "$subject" =~ $re ]]; then
      say "commit ${sha:0:8} subject rejected: $subject"; [ -z "$first_bad" ] && first_bad="$sha"
    elif [ "${#body}" -lt 40 ]; then
      say "commit ${sha:0:8} body too short (${#body} chars): $subject"; [ -z "$first_bad" ] && first_bad="$sha"
    fi
  done
  [ -z "$first_bad" ]
}
push_main() {
  local n
  for n in 1 2 3; do
    git push -q origin HEAD:main >>"$SUMMARY" 2>&1 && return 0
    say "push attempt $n failed; re-syncing"
    sleep 20
    fetch_main || return 1
    git rebase origin/main >/dev/null 2>&1 || { git rebase --abort >/dev/null 2>&1; return 1; }
  done
  return 1
}
dirty_streak=0; LAST_GATE="n/a"; gate_rc=0
publish() {
  if [ -n "$(git status --porcelain)" ]; then
    dirty_streak=$((dirty_streak + 1))
    if [ "$dirty_streak" -ge 2 ]; then say "STATUS: BLOCKED - the tree is still dirty after two sessions, nothing pushed"; return 1; fi
    say "the session left uncommitted work; not pushed now, the next session finishes it"
    return 0
  fi
  dirty_streak=0
  sync_main || return 1
  local count good
  count="$(git rev-list --count origin/main..HEAD)"
  [ "$count" -eq 0 ] && return 0
  if ! check_commits; then
    good="$(git rev-list --count "origin/main..${first_bad}^")"
    if [ "$good" -gt 0 ] && git push -q origin "${first_bad}^:main" >>"$SUMMARY" 2>&1; then
      pushed_total=$((pushed_total + good))
      say "pushed $good commit(s) that precede the first rejected one"
    fi
    say "STATUS: BLOCKED - commit message check failed, the rest was not pushed"
    return 1
  fi
  run_gates; gate_rc=$?
  if [ "$gate_rc" -eq 2 ]; then return 1; fi
  if [ "$gate_rc" -eq 1 ]; then
    gate_fail_streak=$((gate_fail_streak + 1)); LAST_GATE="fail"
    say "gate failed ($gate_fail_streak of $GATE_MAX_FAIL): $GATE_FEEDBACK"
    if [ "$gate_fail_streak" -ge "$GATE_MAX_FAIL" ]; then say "STATUS: BLOCKED - the independent gate failed $GATE_MAX_FAIL sessions in a row, nothing pushed"; return 1; fi
    escalate=1; return 0
  fi
  gate_fail_streak=0; GATE_FEEDBACK=""; LAST_GATE="ok"
  if [ -n "$syntax_skipped" ]; then say "note: parse checks skipped (tool missing):$syntax_skipped - NOT_ASSESSED"; syntax_skipped=""; fi
  if push_main; then
    pushed_total=$((pushed_total + count))
    say "pushed $count commit(s) to main, now at $(git rev-parse --short HEAD)"
  else
    say "STATUS: BLOCKED - push to main failed after retries"; return 1
  fi
}
limit_streak=0
wait_out_limit() {
  local wait_s how remaining
  limit_streak=$((limit_streak + 1))
  if wait_s="$(limit_wait_secs "$1")"; then how="reset time read from the log"; else
    wait_s=$((LIMIT_WAIT_MIN * 60 * (1 << (limit_streak > 6 ? 5 : limit_streak - 1))))
    [ "$wait_s" -gt $((LIMIT_WAIT_MAX_MIN * 60)) ] && wait_s=$((LIMIT_WAIT_MAX_MIN * 60))
    wait_s=$((wait_s / 2 + (RANDOM * 32768 + RANDOM) % (wait_s / 2 + 1)))   # jitter in [w/2, w] so parallel drivers do not retry together
    how="backoff with jitter"
  fi
  remaining=$((MAX_SECS - ($(date +%s) - START_EPOCH)))
  if [ $((wait_s + 1200)) -gt "$remaining" ]; then
    say "the limit resets after the run budget ends (wait $((wait_s / 60)) min), ending"; return 1
  fi
  STATE="waiting for a usage limit until $(date -d "+${wait_s} seconds" '+%F %T') ($how)"
  say "usage or rate limit; sleeping $((wait_s / 60)) min ($how), session not counted"
  nap "$wait_s" || { say "STOP file found, ending"; return 1; }
}
respect_daily_cap() {
  [ "$DAILY_SESSION_CAP" -gt 0 ] || return 0
  local now cutoff n oldest
  now="$(date +%s)"; cutoff=$((now - 86400))
  n="$(awk -v c="$cutoff" '$1 > c' "$STARTS_FILE" 2>/dev/null | wc -l | tr -d ' ')"
  [ "$n" -lt "$DAILY_SESSION_CAP" ] && return 0
  oldest="$(awk -v c="$cutoff" '$1 > c {print $1}' "$STARTS_FILE" | sort -n | head -n 1)"
  say "daily cap of $DAILY_SESSION_CAP sessions reached; waiting for the window to move"
  nap $((oldest + 86400 - now + 60))
}
agent_run() { # agent_run <model> <effort> <prompt> <timeout> -> output on stdout/stderr
  if [ "$RUNNER" = "claude" ]; then
    # shellcheck disable=SC2086
    timeout -k 60 "$4" claude -p "$3" --model "$1" --effort "$2" \
      --permission-mode acceptEdits \
      --allowedTools "Read" "Glob" "Grep" "Edit" "Write" "Bash" "Skill" "WebFetch" "WebSearch" \
      --disallowedTools "Bash(git push:*)" "Bash(git checkout main:*)" "Bash(git switch:*)" "Bash(git stash:*)" "Bash(git reset --hard:*)" "Bash(git clean:*)" "Bash(git rebase:*)" "Bash(git merge:*)" \
      $CLAUDE_EXTRA_ARGS </dev/null
  else
    timeout -k 60 "$4" codex exec --sandbox danger-full-access "$3" </dev/null
  fi
}
preflight() {
  local pf="$LOG_DIR/preflight-$RUNNER.log" pfc
  while :; do
    say "preflight: checking the $RUNNER login and flags"
    agent_run "$CLAUDE_MODEL" "low" "Reply with the single word OK." 180 >"$pf" 2>&1; pfc=$?
    if [ "$pfc" -eq 0 ]; then say "preflight ok"; rm -f "$pf"; limit_streak=0; return 0; fi
    if ! fatal_error "$pf" && hit_limit "$pf"; then wait_out_limit "$pf" && continue; return 1; fi
    say "STATUS: BLOCKED - preflight failed (exit $pfc): $(grep -v 'connectors are disabled' "$pf" | tail -n 2 | tr '\n' ' ')"
    return 1
  done
}
preflight || { notify "autorun $REPO_NAME $RUNNER did not start: $(tail -n 1 "$SUMMARY")"; exit 1; }

no_progress=0; failures=0; escalate=0
for ((i = 1; i <= MAX_SESSIONS; i++)); do
  [ -f "$STOP_FILE" ] && { say "STOP file found, ending"; break; }
  elapsed=$(( $(date +%s) - START_EPOCH ))
  [ "$elapsed" -ge "$MAX_SECS" ] && { say "MAX_HOURS $MAX_HOURS reached, ending"; break; }
  [ $((MAX_SECS - elapsed)) -lt 1200 ] && { say "less than 20 minutes of the run budget left, ending"; break; }
  free_gb="$(df -Pk . | awk 'NR==2 {printf "%d", $4/1048576}')"
  [ "${free_gb:-999}" -lt "$MIN_FREE_GB" ] && { say "only ${free_gb} GB free, ending"; break; }
  sync_main || break
  if [ -f "$LEDGER" ]; then
    if grep -q '^STATUS: DONE' "$LEDGER"; then
      new="$(unregistered_plans)"
      if [ -n "$new" ]; then say "ledger says DONE but unlisted plan folder(s) exist: $(echo "$new" | tr '\n' ' ')- continuing"
      else say "the ledger says DONE: every plan is finished"; break; fi
    elif grep -q '^STATUS: BLOCKED' "$LEDGER"; then
      say "the ledger is BLOCKED: $(grep -m1 '^STATUS: BLOCKED' "$LEDGER")"; break
    fi
  fi
  respect_daily_cap || { say "STOP file found, ending"; break; }
  model="$CLAUDE_MODEL"; effort="$CLAUDE_EFFORT"
  [ "$escalate" -eq 1 ] && { model="$ESCALATE_MODEL"; effort="$ESCALATE_EFFORT"; }
  before="$(git rev-parse HEAD)"
  log="$LOG_DIR/$RUNNER-$(date '+%Y%m%d-%H%M%S').log"
  SESSION_NO="$i"; CUR_MODEL=""; [ "$RUNNER" = claude ] && CUR_MODEL="$model/$effort"
  STATE="session $i running since $(date '+%T')"
  say "session $i/$MAX_SESSIONS start ($RUNNER$([ "$RUNNER" = claude ] && echo ", $model/$effort")), head ${before:0:8}, $((elapsed / 3600)) h into the run"
  sess_start="$(date +%s)"
  RESOLVE_NOTE=""; [ "$RESOLVE_MODE" -eq 1 ] && RESOLVE_NOTE=" MERGE CONFLICT TASK: a merge of origin/main is in progress in this worktree. Resolve it first, as described under SYNC AND CONFLICTS, before any plan work."
  agent_run "$model" "$effort" "$PROMPT${RESOLVE_NOTE}${GATE_FEEDBACK:+ INDEPENDENT CHECK FAILED LAST SESSION: $GATE_FEEDBACK}" "$SESSION_TIMEOUT" >"$log" 2>&1
  code=$?
  sess_secs=$(( $(date +%s) - sess_start ))
  if git rev-parse -q --verify MERGE_HEAD >/dev/null 2>&1; then
    git merge --abort >/dev/null 2>&1
    say "STATUS: BLOCKED - the conflict with origin/main was not resolved; the merge was aborted and no work was lost (see the backup/autorun-$RUNNER-* branch)"; break
  fi
  RESOLVE_MODE=0
  STATE="session $i ended (exit $code), checking and publishing"
  if [ -n "$CLEANUP_CMD" ]; then bash -c "$CLEANUP_CMD" >>"$SUMMARY" 2>&1 || say "CLEANUP_CMD returned non-zero"; fi
  tidy
  after="$(git rev-parse HEAD)"
  say "session $i end: exit $code, head ${after:0:8}, log $(basename "$log")"
  if [ "$code" -ne 0 ] && fatal_error "$log"; then
    say "STATUS: BLOCKED - login or billing error: $(grep -v 'connectors are disabled' "$log" | tail -n 1)"; break
  fi
  if [ "$code" -ne 0 ] && hit_limit "$log"; then
    wait_out_limit "$log" || break
    i=$((i - 1)); continue
  fi
  limit_streak=0
  date +%s >>"$STARTS_FILE"
  LAST_GATE="n/a"
  nc="$(git rev-list --count "$before..$after" 2>/dev/null || echo 0)"
  nfiles="$(git diff --name-only "$before" "$after" 2>/dev/null | wc -l | tr -d ' ')"
  publish || { metric "$i" "$model" "$sess_secs" "$nc" "${nfiles:-0}" "$code" "blocked"; break; }
  metric "$i" "$model" "$sess_secs" "$nc" "${nfiles:-0}" "$code" "$LAST_GATE"
  update_ewma "$sess_secs"; fc="$(forecast)"; [ -n "$fc" ] && say "$fc"
  if [ "$code" -ne 0 ] && [ "$after" = "$before" ]; then failures=$((failures + 1)); else failures=0; fi
  if [ "$after" = "$before" ]; then no_progress=$((no_progress + 1)); else no_progress=0; fi
  if [ "$failures" -ge 2 ]; then say "two failed sessions in a row, ending"; break; fi
  if [ "$no_progress" -ge 2 ]; then say "two sessions without a commit, ending"; break; fi
  if [ "$failures" -ge 1 ] || [ "$no_progress" -ge 1 ]; then escalate=1; [ "$RUNNER" = claude ] && say "next session runs on $ESCALATE_MODEL/$ESCALATE_EFFORT"; else escalate=0; fi
done
git fetch origin main >/dev/null 2>&1
[ -f "$STOP_FILE" ] && rm -f "$STOP_FILE"
tidy
STATE="ended"
say "finished: $pushed_total commit(s) pushed this run, $(git rev-list --count origin/main..HEAD) unpushed; main is at $(git rev-parse --short origin/main). Review: git log --oneline -20 origin/main"
notify "autorun $REPO_NAME $RUNNER ended. $(tail -n 3 "$SUMMARY")"
