# Phase 8 acceptance evidence

Date: 2026-09-04
Environment: Windows, .NET 10.0.1 SDK, Release configuration

`Phase08FilesystemReconciliationTests` passed five tests in Release mode:

- healthy complete scans mark only evidence-backed absence after the grace
  window and restore reappearing occurrences;
- root outage and incomplete scans perform no availability mutation;
- unique exact-hash moves preserve the occurrence and replacements clear the
  stale asset binding;
- replacements queue a pending versioned `FileProcessing` stage;
- ambiguous exact-hash candidates remain available, create a durable pending
  `ReconciliationReviewRow`, and expose a counted path-free audit summary.

The phase also adds `MissingSinceUtc` and `ReconciliationReviews` through
`20260904093000_Phase08ReconciliationRecovery`. `dotnet ef migrations list`
reports both Phase 7 and Phase 8 recovery migrations.

Physical disconnected-volume and ACL scenarios, cross-OS behavior and operator
review UI remain explicitly unassessed release gates.
