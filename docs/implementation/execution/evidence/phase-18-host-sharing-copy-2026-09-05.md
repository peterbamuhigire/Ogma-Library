# Phase 18 Host-sharing copy evidence

Date: 2026-09-05

The Host-sharing view model's static control labels and emitted runtime status
formats now use the shared English/French localization surface. This covers
host state, connection/discovery, sync, clipboard, enrollment, and school
administration outcomes while preserving provider/network error details as
format arguments. The model subscribes to culture changes so an open shell
refreshes its bindings, and its subscription is released when the shell is
disposed.

Verification:

- `HostSharingViewModelTests`: 16 passed, 0 failed, 0 skipped.
- The regression tests verify English labels, French labels, culture-change
  property notifications, and French runtime status formatting.
- The Release test build completed with 0 warnings and 0 errors.

Gate disposition:

- Host-sharing copy subgate: CLOSED locally for the covered view-model surface.
- Phase 18 application-wide copy inventory, contrast snapshots, physical
  keyboard/screen-reader journeys, and live physical UI confirmation: OPEN.
