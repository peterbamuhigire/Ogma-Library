# Phase 29 Unsupported-Claim and Abstention Benchmark Evidence

Date: 2026-09-04

## Result

The grounded-answer safety path now has an executable bounded benchmark. It
feeds 24 fabricated title claims through the provenance validator and confirms
that every result is replaced with the local verified value plus an uncertainty
label. It also runs a no-result local evidence query and confirms that the
answer abstains with no citations.

This benchmark covers deterministic safety behavior only; it is not a live
provider quality evaluation or a substitute for human-labelled relevance data.

## Verification

    dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore -p:BaseOutputPath=tmp/phase29-abstention-build-1/ --filter "FullyQualifiedName~Phase29GroundedEvidenceTests" --logger "console;verbosity=minimal" --results-directory tmp/phase29-abstention-results-1

Result: 5 passed, 0 failed. The benchmark-specific test records 24/24
fabricated claims marked uncertain and a no-local-evidence abstention with an
empty citation set.

## Gate disposition

Closed: unsupported-claim sanitization and deterministic no-evidence
abstention benchmark.

Still open: live/human-labelled evaluation and physical UI acceptance.
