# Phase 38 format-gate evidence

Date: 2026-09-05
Verified source commit: `820d9cd384db8e0b86d6baec99c1a9dbec3b759d`

Command:

```text
dotnet format OgmaLibrary.sln --verify-no-changes --no-restore
```

Result: failed with exit code 1. A clean detached worktree at the verified
source commit reproduced the result without any user working-tree changes.
Diagnostics include CRLF end-of-line mismatches, migration charset/BOM
mismatches, import ordering, and a smaller set of whitespace diagnostics across
the committed solution. The formatter's fix-all pass also reported unsupported
compiler-code-fix diagnostics and proposed a duplicate `ConfigureAwait(false)`
in one path, so its output was discarded rather than imported wholesale.

Gate disposition:

- CI format gate: OPEN.
- Release build and test gates remain separately evidenced; this failure does
  not invalidate the 1,104-test result.
- No user-owned files were modified, staged, or committed during the check.
