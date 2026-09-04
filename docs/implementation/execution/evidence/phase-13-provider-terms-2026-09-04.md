# Phase 13 Provider Terms and Privacy Evidence

Date: 2026-09-04
Reviewer: Peter Bamuhigire, Lead Consultant
Review mode: read-only source verification; official provider pages only

## Scope and decision

The gateway's local privacy and resilience controls are checked against the
current official provider guidance available on 2026-09-04. This closes the
documentation subgate for provider-specific request constraints, but it does
not constitute legal advice, provider approval, or production-network
certification. Phase 13 remains `IN PROGRESS` until the legal/privacy owner
and live network/production evidence are recorded.

## Verified source register

| Source ID | Provider and source | Locator / verified claim | Tier | Freshness and liveness | Archive status |
| --- | --- | --- | --- | --- | --- |
| OL-API-2026-09-04 | Open Library, [APIs](https://openlibrary.org/developers/api) | Usage Guidelines and Rate Limits: human-facing, low-volume use; cache where possible; identify the application with `User-Agent` and contact email; default 1 request/second and identified 3 requests/second; bulk API harvesting and high-traffic backend use are disallowed and may be rate-limited or blocked. | Tier 2 institutional | Page live and crawled 2026-09-04; reviewed page history shows last edit 2026-05-05. | No independent archive snapshot verified; `NOT ASSESSED`. |
| OL-LIC-2026-09-04 | Open Library, [Licensing](https://openlibrary.org/developers/licensing) | Internet Archive does not assert new proprietary rights over the database, while existing rights issues may remain for contributions and jurisdictions. | Tier 2 institutional | Page live and crawled 2026-09-04; reviewed page history shows last edit 2021-06-27. This is not treated as current legal clearance. | No independent archive snapshot verified; `NOT ASSESSED`. |
| GB-API-2026-09-04 | Google Books, [Using the API](https://developers.google.com/books/docs/v1/using) | Public requests require an API key or access token; private user data requires OAuth 2.0; the API key identifies the project and supports quota/reporting; volume search supports pagination and ISBN queries; result availability can vary by server/client IP because of legal and location restrictions. | Tier 2 institutional | Page live and crawled 2026-09-04; no publication date was presented in the reviewed locator. | No independent archive snapshot verified; `NOT ASSESSED`. |
| GB-BRAND-2026-09-04 | Google Books, [Branding Guidelines](https://developers.google.com/books/branding) | Current use is governed by Google's Terms; displayed Google results/previews/content require attribution and prominent links; results/content must not be altered; guidelines may change without prior notice. | Tier 2 institutional | Page live and crawled 2026-09-04; page expressly warns that guidelines can change. | No independent archive snapshot verified; `NOT ASSESSED`. |
| GB-TERMS-2026-09-04 | Google, [Google APIs Terms of Service](https://developers.google.com/terms) | API use is subject to terms, applicable law, privacy obligations, API limits, and restrictions concerning content use, retention, and portability. | Tier 2 institutional | Page live and crawled 2026-09-04; reviewed source reports last modification 2021-11-09. Current applicability must be rechecked before release. | No independent archive snapshot verified; `NOT ASSESSED`. |
| GB-PRIV-2026-09-04 | Google, [Privacy Policy](https://policies.google.com/privacy?hl=en-US) | Google describes collection/use/sharing/retention and user controls including export and deletion; the policy applies broadly to Google services and may have service-specific policies. This is provider context, not a finding about Ogma's processing. | Tier 2 institutional | Page live and crawled 2026-09-04; reviewed copy reports effective 2026-05-26. | No independent archive snapshot verified; `NOT ASSESSED`. |

## Implementation disposition

| Requirement | Existing Ogma control | Disposition |
| --- | --- | --- |
| Avoid Open Library bulk/high-traffic use | Durable normalized cache, bounded quota, circuit state, retry telemetry, and stale-cache fallback exist in the gateway. | Code-level support documented; real traffic profile and provider approval remain open. |
| Identify Open Library requests | Provider handler must carry an application `User-Agent` and contact value before production use. | Configuration/release evidence required; not closed by local tests. |
| Meet Google request identification and quota rules | Provider profile persistence stores endpoint/configuration without raw API-key material; gateway health exposes quota state. | Code-level support documented; valid production credential, project quota, and live call evidence remain open. |
| Preserve Google attribution/linking and result integrity | Provider values are kept as provider-labelled data for review; end-user attribution/link behavior is a UI/release gate. | UI acceptance and legal/privacy owner sign-off required. |
| Avoid retaining or disclosing unnecessary user data | Recorded lookup requests contain bibliographic keys only, use `GET`, and exclude notes/content; cache retention is bounded. | Local privacy-disclosure subgate closed; provider-specific retention and erasure terms remain open. |
| Handle rights and location restrictions | Provider outputs remain reviewable metadata, not an assertion of content rights; Google location-dependent availability is not assumed universal. | Legal review and representative network/region tests required. |

## Unresolved gates

The following remain explicitly open and are not converted to `PASS` by this
research wave:

1. Written legal/privacy owner review of provider terms, licensing, data
   retention, attribution, and jurisdictional implications.
2. Current archived snapshots or an approved evidence-retention mechanism for
   release-bound terms evidence.
3. Live network tests from the supported deployment environment, including
   Open Library identification/rate behavior and Google credentials/quota.
4. UI acceptance proving provider attribution, links, stale labeling, and
   result integrity.

## Verification limitations

URL liveness and claim support were checked against the official pages. No
archive snapshot, provider contract, API credential, production network, or
independent legal review was available in this workspace. Those absences are
recorded as `NOT ASSESSED`, not inferred as compliant.
