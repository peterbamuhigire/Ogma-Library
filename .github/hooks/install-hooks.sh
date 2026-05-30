#!/bin/sh
# Install Ogma Library git hooks (macOS / Linux).
# Run once after cloning:  sh .github/hooks/install-hooks.sh
set -e

repo_root=$(git rev-parse --show-toplevel)
src="$repo_root/.github/hooks"
dst="$repo_root/.git/hooks"

mkdir -p "$dst"
for hook in commit-msg; do
  cp "$src/$hook" "$dst/$hook"
  chmod +x "$dst/$hook"
  echo "✓ installed $hook"
done

echo "Ogma Library git hooks installed."
