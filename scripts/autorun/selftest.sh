#!/usr/bin/env bash
# Self-test for run_plan.sh with a fake agent on throwaway repositories. Usage: selftest.sh   (needs git, bash 4+, GNU timeout)
# Scenarios: A normal run to DONE; B upstream moves (pull-first, rebase); C conflicting upstream (resolve session);
#            D parse error then fix (gate feedback loop); E secret in a commit (hard stop, nothing pushed).
SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
T="$(mktemp -d)"; pass=0; fail=0
ok() { if eval "$2"; then echo "PASS $1"; pass=$((pass + 1)); else echo "FAIL $1"; fail=$((fail + 1)); fi; }
mkagent() { # mkagent <dir> <scenario>
  mkdir -p "$1/bin"
  cat >"$1/bin/claude" <<EOF
#!/usr/bin/env bash
SC="$2"
case "\$*" in *"single word OK"*) echo OK; exit 0;; esac
c=\$(git rev-list --count HEAD)
commit() { git add -A && git commit -q -m "\$1" -m "Body sentence one explains the change. Sentence two lists tests and NOT_ASSESSED items." -m "Co-Authored-By: Claude <noreply@anthropic.com>"; }
done_ledger() { printf 'STATUS: DONE\n| area-01-a | done | x |\n| area-02-b | done | x |\n' > docs/plans/AUTORUN.md; }
if git rev-parse -q --verify MERGE_HEAD >/dev/null; then
  printf 'resolved\n' > shared.txt; git add shared.txt; git commit -q --no-edit; done_ledger; commit "docs: Finish ledger after resolving the merge"; exit 0
fi
case "\$SC" in
 A|B) echo "w\$c" >> docs/plans/area-plans/area-01-a/plan.md; [ "\$c" -ge 3 ] && done_ledger || printf '| area-01-a | active | x |\n' > docs/plans/AUTORUN.md; commit "feature: Implement phase \$c of the fixture plan" ;;
 C) echo "local" > shared.txt; commit "feature: Change shared file locally in the session"; R="\$(git remote get-url origin)"; O="\$(mktemp -d)"; git clone -q -b main "\$R" "\$O"; (cd "\$O" && git config user.email o@o && git config user.name o && echo teammate > shared.txt && git add -A && git commit -qm "chore: Teammate edits the same file" && git push -q origin main) ;;
 D) if [ -f bad.php ] && grep -q 'INDEPENDENT CHECK' <<<"\$*"; then echo '<?php echo 1;' > bad.php; done_ledger; commit "fix: Repair the PHP file after the check failed"; else echo '<?php echo (;' > bad.php; commit "feature: Add a PHP file that does not parse yet"; fi ;;
 E) echo "key=AKIA1234567890ABCDEF" > notes.txt; commit "feature: Add notes that contain a secret like value" ;;
esac
EOF
  chmod +x "$1/bin/claude"
}
mkrepo() { # mkrepo <name> <scenario>
  local d="$T/$1"; mkdir -p "$d/remote.git" "$d/work"
  git init -q --bare "$d/remote.git"; (cd "$d/work" && git init -q -b main && git config user.email t@t && git config user.name t && git config core.autocrlf false
    mkdir -p docs/plans/area-plans/area-01-a docs/plans/area-plans/area-02-b scripts/autorun
    echo a >docs/plans/area-plans/area-01-a/plan.md; echo b >docs/plans/area-plans/area-02-b/plan.md; echo base >shared.txt
    cp "$SRC"/run_plan.sh "$SRC"/status.sh scripts/autorun/; git add -A; git commit -qm "chore: initial fixture commit for self test"
    git remote add origin "$d/remote.git"; git push -q origin main)
  mkagent "$d" "$2"
}
upstream() { # push a commit to the remote from a second clone: upstream <name> <file> <text>
  local d="$T/$1"; git clone -q -b main "$d/remote.git" "$d/other" && (cd "$d/other" && git config user.email o@o && git config user.name o && echo "$3" >"$2" && git add -A && git commit -qm "chore: Upstream change made by a teammate" && git push -q origin main)
}
run() { (cd "$T/$1/work" && PATH="$T/$1/bin:$PATH" MAX_HOURS=2 timeout 240 bash scripts/autorun/run_plan.sh claude "${2:-6}" >"$T/$1/out.txt" 2>&1); }

mkrepo A A; run A 8; ok "A: reaches DONE and pushes 3 commits" "grep -q 'ledger says DONE' $T/A/out.txt && [ \$(git -C $T/A/remote.git rev-list --count main) -ge 4 ]"
mkrepo B B; upstream B newfile.txt hello; run B 8; ok "B: pulled upstream first and kept both histories" "git -C $T/B/remote.git show main:newfile.txt | grep -q hello && grep -q 'ledger says DONE' $T/B/out.txt"
mkrepo C C; run C 3; ok "C: conflict resolved by a resolve session, no work lost" "git -C $T/C/remote.git show main:shared.txt | grep -q resolved; "
mkrepo D D; run D 6; ok "D: parse error held back, fixed next session, then pushed" "grep -q 'gate failed' $T/D/out.txt && git -C $T/D/remote.git show main:bad.php | grep -q 'echo 1'"
mkrepo E E; run E 3; ok "E: secret-like value blocks the push" "grep -q 'commit guard refused' $T/E/out.txt && ! git -C $T/E/remote.git show main:notes.txt >/dev/null 2>&1"
echo "passed $pass, failed $fail   (outputs in $T)"
[ "$fail" -eq 0 ]
