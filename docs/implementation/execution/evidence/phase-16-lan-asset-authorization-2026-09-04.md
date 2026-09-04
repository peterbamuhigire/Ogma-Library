# Phase 16 LAN asset authorization evidence

Date: 2026-09-04

The LAN visual-asset endpoint now accepts only variants defined by the visual
asset contract. Cover routes allow the default route plus provider and detail
variants; spine routes allow default and retina; thumbnails currently expose
only the default route until a bounded variant contract is added. All routes
still require an authenticated session and an active published content hash.

`LanHostEndpointTests.HostListener_HealthAuthAndCatalogueProjection_WorkOverHttps`
passed after adding a request for an unsupported cover variant and asserting a
`400 Bad Request` before asset resolution. Existing authorized asset delivery
and unpublished-hash rejection remained green.

The LAN asset-authorization sub-gate is CLOSED locally. Provider/embedded
source acquisition, target-scale asset budgets, physical accessibility, and
cross-platform evidence remain open.
