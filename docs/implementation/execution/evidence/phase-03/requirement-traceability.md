# Phase 3 requirement traceability

| Requirement | Phase 3 implementation evidence | Failure/ambiguity behavior | Test evidence | Phase status |
| --- | --- | --- | --- | --- |
| FR-LIB-003 | `FileOccurrence`, `ContentAsset` and verified `ContentHash` separate locator from bytes; ADR 0016 fixes rename/move continuity semantics | Unknown hash remains null; a path never substitutes for content identity | `CanonicalIdentityModelTests`; `LegacyAdapter_DoesNotFabricateUnknownFileFacts`; filesystem scenario specification | Domain contract COMPLETE; schema/scanner/reconciliation continue in Phases 4, 7 and 8 |
| FR-LIB-004 | `FileOccurrence.Availability` represents unavailable state independently of identity and curation | Unavailability has no delete transition in the domain contract | unknown-content test; disconnected-root scenario | Domain contract COMPLETE; root/reconciliation runtime evidence remains Phases 5 and 8 |
| FR-CAT-007 | `Work` owns editions; edition/assets remain distinct; decision policy classifies exact copy, same edition, same work/different edition and possible match | All bibliographic/similarity relationships require review; no silent merge | exact-hash property loop; edition/work/similarity/provider-scope tests | Domain classification COMPLETE; persistence and reversible merge/split remain Phases 4 and 9 |
| Metadata identity scope | `BibliographicIdentifier` records source, kind, scope and normalized value | Invalid work/edition scopes throw; different provider namespaces do not match | `BibliographicIdentifiers_EnforceWorkAndEditionScopes`; provider namespace test | Phase 3 identity scope COMPLETE; provenance/provider flows remain Phases 12-14 |
| Search/AI/3D identity boundary | `CataloguePresentationIdentity` provides work/edition/preferred occurrence IDs without a path | Consumers cannot use a canonical path property as an identity authority | `Architecture_CanonicalIdentityModel_IsPathIndependentAndExplicit` | Contract COMPLETE; consumer adoption remains their scheduled phases |

Trace chain: requirement -> identity invariant -> domain type/policy -> explicit
failure or review behavior -> executable test -> scheduled downstream owner.
No downstream persistence, filesystem, UI, search, AI or 3D feature is claimed
complete by this domain freeze.
