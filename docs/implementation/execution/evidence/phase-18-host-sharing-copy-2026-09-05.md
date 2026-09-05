# Phase 18 Host-sharing copy evidence

Date: 2026-09-05

The Host-sharing view model's static control labels now use the shared
English/French localization surface. The model subscribes to culture changes
so an open shell refreshes its labels, and its subscription is released when
the shell is disposed. This covers the previously identified Host-sharing
label-copy slice; dynamic connection, discovery, sync, and error status text
remain outside this slice.

Verification:

- `HostSharingViewModelTests`: 15 passed, 0 failed, 0 skipped.
- The new regression test verifies English labels, French labels, and
  culture-change property notifications.
- The Release test build completed with 0 warnings and 0 errors.

Gate disposition:

- Host-sharing static control-copy subgate: CLOSED locally.
- Phase 18 application-wide copy inventory, contrast snapshots, physical
  keyboard/screen-reader journeys, and complete dynamic Host-sharing status
  localization: OPEN.
