# Phase 10 platform-sandbox currentness evidence — 2026-09-06

## Decision scope

This research wave evaluates whether the current Windows and macOS platform
contracts support replacing the PDF worker's process-only/resource-bounded
boundary with a supported OS-enforced filesystem and network sandbox.

## Source register

| Source ID | Owner and source | Published/updated | Accessed | Freshness and status | Claim admitted |
| --- | --- | --- | --- | --- | --- |
| MS-SBX-2026-06 | Microsoft, [Create Process In Sandbox APIs](https://learn.microsoft.com/en-us/windows/win32/secauthz/createprocessinsandbox) | 2026-06-01 | 2026-09-06 | Current but experimental; primary platform documentation | Windows documents experimental `Experimental_CreateProcessInSandbox` APIs with AppContainer, filesystem, network, and process restrictions. The API requires dynamic lookup from `processmodel.dll`, an undocumented public header, and a compiled `SandboxSpec.fbs` FlatBuffer; production support and schema stability are **NOT ASSESSED**. |
| MS-AC-2025-07 | Microsoft, [AppContainer isolation](https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation) | 2025-07-08 | 2026-09-06 | Current primary platform documentation | AppContainer is the relevant Windows isolation model for least-privilege process, file, network, and resource access; this is the architectural target, not a self-reported environment flag. |
| APP-SBX-2026-09 | Apple, [Configuring the macOS App Sandbox](https://developer.apple.com/documentation/xcode/configuring-the-macos-app-sandbox) | Crawled 2026-09-04 | 2026-09-06 | Current primary platform documentation; exact publication date not presented | macOS App Sandbox is kernel-enforced and configured through application capabilities/entitlements, including file and network access; signed app packaging and runtime verification are part of the supported delivery path. |
| APP-NET-2026-09 | Apple, [`com.apple.security.network.client`](https://developer.apple.com/documentation/bundleresources/entitlements/com.apple.security.network.client) | Crawled 2026-08 | 2026-09-06 | Current primary platform documentation; exact publication date not presented | The network entitlement controls outgoing connections for a sandboxed app; it is not a portable per-child-process switch exposed by .NET. |

Archive snapshots for these live official locators were not independently
verified: **NOT ASSESSED**. The links are retained as live source locators and
must be rechecked before a signed release decision.

## Engineering disposition

1. The existing Windows Job Object remains valid evidence for resource limits
   and process-tree termination, but it is not evidence of filesystem or
   network isolation.
2. The experimental Windows API is not adopted as a production default. Its
   undocumented schema/header, experimental status, Windows 11 minimum, and
   unverified runtime availability require a separate prototype, security
   review, and Windows reference-machine escape test.
3. A macOS adapter cannot be claimed from a .NET launch wrapper alone. It needs
   a signed/notarized app/helper packaging design with explicit entitlements,
   a controlled file-access model, and physical macOS verification.
4. The worker's `OGMA_PDF_WORKER_NETWORK` and
   `OGMA_PDF_WORKER_CHILD_PROCESSES` variables remain policy metadata only; they
   must not be used as proof of OS enforcement.

## Gate result

Currentness review: **PASS for decision quality**.

Supported, physically verified Windows and macOS sandbox adapters: **NOT
ASSESSED**. Phase 10 remains `IN PROGRESS`; no security or release gate is
closed by this record.
