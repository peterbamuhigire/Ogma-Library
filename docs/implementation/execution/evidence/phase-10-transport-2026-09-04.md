# Phase 10 transport evidence

The rebuilt infrastructure/worker binaries passed six `PdfWorkerIsolationTests`
and three `Phase10PdfInputBrokerTests` in Release mode. The worker now receives
an input copied into its per-operation sandbox, and the persistent-worker test
continues rendering after the source file is deleted. Passwords are sent only
through the startup stdin handshake; the former password environment variable
is no longer set.

This closes the input-copy and password-transport sub-gates only. Output
manifest verification, CPU/memory ceilings, true OS sandbox enforcement and
physical Windows/macOS escape tests remain explicitly open.
