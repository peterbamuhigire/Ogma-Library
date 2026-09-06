# Phase 33 3D Contract Freeze

Date: 2026-09-06

## Frozen boundary

The desktop-to-renderer protocol is frozen as `shelf3d-v1`. The v1 outbound
message family is `SetScene`, `UpdateBook`, `RemoveBook`, `SetCamera`,
`SetTheme`, `SetLayout`, and `FocusBook`. The inbound family is `BookClicked`,
`BookDoubleClicked`, `BookHovered`, `CameraChanged`, `WebGl2Status`,
`PerformanceWarning`, and `PerformanceMetrics`; unknown inbound types are
represented only for fail-closed rejection.

All messages use the validated application-owned catalogue book identifier.
The existing parser, validator, serializer, scheme-handler, and build-manifest
tests remain part of the contract evidence.

## Compatibility rule

A breaking message removal, rename, field/type change, identifier semantic
change, or default-behavior change requires a new protocol version and an
explicit migration of both C# and TypeScript consumers. Additive fields must be
optional/defaultable and still require conformance review. The old version
must not be emitted for changed semantics.

The generated renderer remains reproducible from `src/shelf3d`. Build schema
`ogma-shelf3d-build-v1` binds the TypeScript source, lockfile, and packaged
bundle SHA-256 digests. Bundle-integrity rejection remains executable in
`Shelf3DAssetPublisher_RejectsBundleTamperingAgainstBuildManifest`.

## Executable proof

`Phase33ContractFreeze_ProtocolAndMessageFamiliesMatchV1` guards the protocol
identifier and exact concrete message families. `BridgeMessageTests` also
guards representative serialized shapes, unsupported-version rejection,
message validation, bounded metrics, bootstrap, and navigation containment.

## Residual gates

This closes the repository contract freeze only. Real GPU/WebView frame
metrics, reference-hardware performance, physical WebView2/WKWebView behavior,
context-loss recovery, and cross-platform accessibility remain `NOT ASSESSED`.
