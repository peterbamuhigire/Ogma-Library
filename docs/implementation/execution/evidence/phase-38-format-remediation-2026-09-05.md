# Phase 38 format remediation evidence

Date: 2026-09-05
Verified source commit: `17871b99453f52fb7b1e068144b9b213287ef2b5` for the safe
subset; the residual import-order correction is committed separately below.

The safe formatter subset corrected 50 committed files for import ordering,
whitespace, migration charset/BOM, and the repository's CRLF policy. The
formatter's semantic-looking async and test-cast edits were deliberately
excluded. The subset passed the complete Release solution regression:
908 core, 41 architecture, and 155 UI tests; 1,104 passed, 0 failed, 0
skipped.

Fresh-checkout verification with:

```text
dotnet format OgmaLibrary.sln --verify-no-changes --no-restore
```

previously reported one remaining diagnostic: import ordering in
`src/OgmaLibrary.App/Composition/ReaderModule.cs`. The file's content hash
matched `HEAD` before correction; its working-tree difference was line-ending
only, so the mechanical import-order correction was safe to apply.

Gate disposition:

- Committed-source format remediation: CLOSED locally, including the residual
  import-order correction.
- CI format gate: CLOSED locally after the clean-checkout verification below;
  hosted CI and release acceptance remain separate gates.

## Residual correction verification

Source commit after correction: `c180fa07f5284690cb61dadd93dd476008c0fa76`.

A fresh checkout of that commit was verified with:

```text
dotnet format OgmaLibrary.sln --verify-no-changes --no-restore
```

and produced no formatter diagnostics.

Verification result: locked restore exit `0`; formatter exit `0`.
