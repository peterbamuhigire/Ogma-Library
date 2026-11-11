#!/usr/bin/env bash
# Show the progress of the unattended driver for this repository (read-only).
# Usage: scripts/autorun/status.sh [claude|codex] [watch]
RUNNER="${1:-claude}"
MAIN_REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
AUTORUN_HOME="${AUTORUN_HOME:-$(dirname "$MAIN_REPO")/_autorun}"
BASE="$AUTORUN_HOME/$(basename "$MAIN_REPO")"
show() {
  echo "== $(basename "$MAIN_REPO") / $RUNNER =="
  cat "$BASE/logs/status-$RUNNER.txt" 2>/dev/null || echo "no status file yet (driver never started)"
  echo "-- last summary lines --"
  tail -n 12 "$BASE/logs/summary-$RUNNER.log" 2>/dev/null
  echo "-- ledger --"
  head -n 25 "$BASE/wt-$RUNNER/docs/plans/AUTORUN.md" 2>/dev/null || echo "no ledger yet"
  echo "-- commits on origin/main from the driver (latest 8) --"
  git -C "$MAIN_REPO" fetch -q origin main 2>/dev/null
  git -C "$MAIN_REPO" log --oneline -8 --author="" --grep="Co-Authored-By" origin/main 2>/dev/null
}
if [ "${2:-}" = "watch" ]; then while :; do clear; show; sleep 30; done; else show; fi
