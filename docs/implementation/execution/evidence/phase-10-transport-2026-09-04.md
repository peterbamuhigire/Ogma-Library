# Phase 10 transport evidence

The rebuilt infrastructure/worker binaries passed six `PdfWorkerIsolationTests`
and three `Phase10PdfInputBrokerTests` in Release mode. The worker now receives
an input copied into its per-operation sandbox, and the persistent-worker test
continues rendering after the source file is deleted. Passwords are sent only
through the startup stdin handshake; the former password environment variable
is no longer set.

Worker page outputs are required to remain inside the sandbox, non-empty, and
below the configured output ceiling; page bytes receive a SHA-256 manifest
calculation before they are returned to the reader. Windows workers are
assigned active-process, CPU-time, and process-memory limits through a Job
Object; the cross-platform fallback remains timeout and process-tree
termination.

This closes the input-copy, password-transport, output ceiling/manifest, and
Windows process-ceiling sub-gates. True OS sandbox enforcement and physical
Windows/macOS escape tests remain explicitly open.
