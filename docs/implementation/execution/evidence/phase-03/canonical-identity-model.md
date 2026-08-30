# Canonical identity model and terminology

This is the Phase 3 terminology contract. Product copy, diagnostics, APIs and
later duplicate-resolution screens must use the most specific applicable term.

| Term | Meaning | User-facing language |
| --- | --- | --- |
| Library root | Approved folder/volume boundary | Library location |
| File occurrence | One observed PDF locator inside one root | File |
| Content asset | One exact sequence of bytes | Exact file copy |
| Edition | A particular publication of a work | Edition |
| Work | The intellectual work across editions | Work |
| Catalogue item | Stable presentation selection used by all 2D/3D clients | Library item |
| Exact content copy | Two occurrences with equal verified complete-file hashes | Exact copy |
| Same edition, different asset | Different bytes with shared strong edition evidence | Another file of this edition |
| Same work, different edition | Shared work evidence with conflicting edition evidence | Another edition of this work |
| Possible match | Similarity or incomplete evidence needing review | Possible match - review required |
| Unavailable | File is not presently accessible; no deletion intent is inferred | File unavailable |

Do not use the word "duplicate" alone in a decision. It hides whether the
evidence describes bytes, an edition or a work. Do not describe an unavailable
file as deleted unless a later explicit delete workflow establishes that fact.

## Invariants

1. A file occurrence belongs to exactly one root and may have zero or one known
   content asset.
2. A content asset requires a genuine SHA-256; paths, filenames, size and mtime
   cannot substitute for it.
3. An edition belongs to exactly one work. A work may own many editions.
4. Edition-to-asset is many-to-many so several renderings may represent one
   edition without erasing file identity.
5. Unknown and ambiguous states are valid first-class states.
6. Only exact hash equality is automatically actionable in Phase 3 policy.
7. Bibliographic identifiers are source-attributed and explicitly scoped.
8. Presentation identity contains no path and is shared by grid, list, search,
   advisor and 3D consumers.
9. Identity decisions retain policy version, evidence tier and confidence.
10. Merge/split behavior in Phase 9 must be reversible; this phase records no
    irreversible bibliographic merge.

## Ownership boundaries

- Domain owns identity types, relationships and conservative decision policy.
- Infrastructure owns root-relative locators and persistence mapping.
- Processing produces verified file facts; it does not manufacture identities
  from a title or path.
- Metadata proposes edition/work relationships with provenance.
- Presentation clients consume catalogue item IDs and cannot become an identity
  authority.
