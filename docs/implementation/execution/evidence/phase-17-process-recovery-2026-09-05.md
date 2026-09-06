# Phase 17 Cross-Platform PDF Worker Process-Recovery Evidence

Date: 2026-09-05
Reviewer: Peter Bamuhigire, Lead Consultant

## Scope

The production persistent PDF worker was first started on the current Windows host,
terminated through the operating-system process API, and then reopened through
the same `PdfWorkerClient` boundary. The exercise used a uniquely generated
temporary PDF and the test sandbox; no repository or user library file was
affected.

## Verification

`PdfWorkerIsolationTests` passed 10/10. The process-recovery case:

1. opened a worker session and waited for its ready response;
2. killed the actual worker process tree and waited for termination;
3. verified the dead session surfaced an operation failure; and
4. opened a new session and successfully rendered a page.

The complete serialized Release core suite subsequently passed 924/924, with
no failures or skips.

Protected-`main` CI run
[34012939882](https://github.com/peterbamuhigire/Ogma-Library/actions/runs/34012939882)
then executed the same unguarded process-termination regression on Windows and
macOS hosted runners. Both jobs passed the complete 930-core-test suite plus 41
architecture and 159 UI tests.

## Gate disposition

The Windows/macOS hosted-runner process-kill detection and PDF-worker restart
subgate is CLOSED. This does not close long-duration soak, crash recovery under
the full application queue, physical reference-machine process behavior, or
physical accessibility evidence.
