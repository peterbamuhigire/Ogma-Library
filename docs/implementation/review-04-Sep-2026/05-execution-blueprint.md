# Execution Blueprint for Remaining Work

## Order of operations

1. Finish code-level gates that do not require physical hardware: provider
   policy UX/configuration, feedback UI, citation UI, metadata review
   accessibility, asset variants, and remaining backend retention contracts.
2. Establish evidence environments: W-REF-01 Windows, M-REF-01 macOS, a real
   mixed-PDF corpus, a two-machine classroom network, native credential stores,
   and reproducible artifact/signing inputs.
3. Run risky validation in isolation: hostile PDFs, provider live calls,
   GPU/WebView, network/firewall/mDNS, backup/restore, upgrade interruption,
   rollback, and long-duration soak.
4. Repair only evidence-backed failures, rerun affected focused and regression
   slices, and update the phase record with exact commands and outputs.
5. Create the Phase 39 acceptance record only after all required artifact,
   hardware, migration, approval, and residual-risk fields are true.

## Operating rules

- Keep `main` deployable and push every three completed phase increments.
- Commit each phase increment separately with DCO sign-off.
- Do not stage or overwrite the user's concurrent UI/images/reader changes.
- Keep unavailable physical, legal, signing, and owner facts as `NOT ASSESSED`.
- Do not use synthetic corpus or headless results as substitutes for reference
  machine acceptance.
- Any schema change after release freeze restarts migration and acceptance
  gates.

## Immediate next actions

- Add the feedback/citation/metadata-review UI acceptance records after the
  current user UI work is reconciled.
- Supply or authorize the real mixed-PDF corpus and reference hardware runs.
- Execute provider configuration and live terms/network checks with a named
  privacy/legal owner.
- Run CI `actionlint`/workflow validation and signed candidate checks.
- Populate and verify the `ogma-release-acceptance-v1` record.
