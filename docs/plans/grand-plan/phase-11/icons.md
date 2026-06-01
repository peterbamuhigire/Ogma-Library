# Phase 11 — Icon Manifest

New icons for Semantic Search, Match-Location Explanation Badges,
Hybrid Ranking indicators, and Embedding Erasure.

Phase 11 has a modest UI footprint compared to Phases 08–10. The icons here
are primarily small badge/chip-scale icons (16/24 px primary) plus a few
action icons (24/32/48 px).

Color family mapping:
- Semantic search — `accent/plum` (soft plum): intelligence / AI surface.
- Match-location badges — differentiated by source type:
  - Title/Author match — `accent/ink` (deep ink blue).
  - Tag match — `accent/oak` (warm oak amber).
  - Note/Text-page match — `accent/ink`.
  - Semantic match — `accent/plum`.
- Confidence High/Medium/Low — `accent/sage`, `accent/oak`, `accent/clay`.
- Embedding generation progress — `accent/plum`.
- Erasure — `accent/clay` (terracotta): destructive / warning action.

---

## Icon manifest table

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_search_semantic` | Search bar — semantic mode indicator | Semantic search is active (Ollama available) | Brain / neural-network dot pattern; `accent/plum` | 16/24/32 @1x–3x | ⬜ to procure |
| `ic_search_semantic_unavailable` | Search bar — degraded mode indicator | Semantic search unavailable (Ollama not found) | Brain with slash or warning dot; `accent/clay` | 16/24/32 @1x–3x | ⬜ to procure |
| `ic_match_title` | Match-location badge | Match found in book title | Book with underline / title-bar; `accent/ink` | 16/24 @1x–3x | ⬜ to procure |
| `ic_match_author` | Match-location badge | Match found in author name | Person / author outline; `accent/ink` | 16/24 @1x–3x | ⬜ to procure |
| `ic_match_tag` | Match-location badge | Match found in a tag | Label / tag; `accent/oak` | 16/24 @1x–3x | ⬜ to procure |
| `ic_match_description` | Match-location badge | Match found in description | Paragraph lines; `accent/slate` | 16/24 @1x–3x | ⬜ to procure |
| `ic_match_toc` | Match-location badge | Match found in table of contents | Indented list; `accent/ink` | 16/24 @1x–3x | ⬜ to procure |
| `ic_match_text_page` | Match-location badge | Match found in extracted page text (page N) | Document page with magnifier; `accent/ink` | 16/24 @1x–3x | ⬜ to procure |
| `ic_match_note` | Match-location badge | Match found in an annotation note | Speech bubble; `accent/ink` | 16/24 @1x–3x | ⬜ to procure |
| `ic_match_semantic` | Match-location badge | Semantic similarity match | Atom / connected dots; `accent/plum` | 16/24 @1x–3x | ⬜ to procure |
| `ic_confidence_high` | Search result — confidence label | High confidence match (≥ 0.8) | Filled circle; `accent/sage` | 16/24 @1x–3x | ⬜ to procure |
| `ic_confidence_medium` | Search result — confidence label | Medium confidence match (0.5–0.8) | Half-filled circle; `accent/oak` | 16/24 @1x–3x | ⬜ to procure |
| `ic_confidence_low` | Search result — confidence label | Low confidence match (< 0.5) | Outline circle; `accent/clay` | 16/24 @1x–3x | ⬜ to procure |
| `ic_embedding_generating` | Index Manager / settings panel | Embeddings are being generated | Progress wave + brain; `accent/plum` animated | 16/24 @1x–3x | ⬜ to procure |
| `ic_embedding_erase` | Privacy Settings — erase action | Erase all stored embeddings | Brain with delete X; `accent/clay` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_ranking_hybrid` | Search results header | Hybrid ranking active indicator | Blend/mix icon (overlapping shapes); `accent/slate` | 16/24 @1x–3x | ⬜ to procure |

---

## Accessible label keys (en + fr required)

| Icon key | Label resource key | en label | fr label |
| --- | --- | --- | --- |
| `ic_search_semantic` | `Search.Semantic.Active` | "Semantic search active" | "Recherche sémantique active" |
| `ic_search_semantic_unavailable` | `Search.Semantic.Unavailable` | "Semantic search unavailable" | "Recherche sémantique non disponible" |
| `ic_match_title` | `Match.Location.Title` | "In title" | "Dans le titre" |
| `ic_match_author` | `Match.Location.Author` | "In author" | "Dans l'auteur" |
| `ic_match_tag` | `Match.Location.Tag` | "In tag" | "Dans l'étiquette" |
| `ic_match_description` | `Match.Location.Description` | "In description" | "Dans la description" |
| `ic_match_toc` | `Match.Location.Toc` | "In table of contents" | "Dans la table des matières" |
| `ic_match_text_page` | `Match.Location.TextPage` | "On page {0}" | "À la page {0}" |
| `ic_match_note` | `Match.Location.Note` | "In notes" | "Dans les notes" |
| `ic_match_semantic` | `Match.Location.Semantic` | "Semantic match" | "Correspondance sémantique" |
| `ic_confidence_high` | `Search.Confidence.High` | "High confidence" | "Confiance élevée" |
| `ic_confidence_medium` | `Search.Confidence.Medium` | "Medium confidence" | "Confiance moyenne" |
| `ic_confidence_low` | `Search.Confidence.Low` | "Low confidence" | "Faible confiance" |
| `ic_embedding_generating` | `Embedding.Generating` | "Generating embeddings…" | "Génération des embeddings…" |
| `ic_embedding_erase` | `Embedding.Erase` | "Erase all embeddings" | "Supprimer tous les embeddings" |
| `ic_ranking_hybrid` | `Search.Ranking.Hybrid` | "Hybrid ranking" | "Classement hybride" |

---

## Owner procurement request

**To: Peter Bamuhigire**
**Re: Phase 11 Semantic Search — Premium PNG Icon Procurement**

Phase 11 introduces **16 new icons**, primarily small badge/chip icons plus
the semantic search indicator and the embedding erasure action icon.

**Color families required:**
- `accent/plum` (soft plum) — semantic search, embedding generation, semantic
  match badge. This is the "intelligence / AI" color; must be visually distinct
  from other phases' icon colors.
- `accent/ink` (deep ink blue) — title, author, text-page, note, TOC match badges.
- `accent/oak` (warm oak amber) — tag match badge, medium confidence.
- `accent/sage` (muted green) — high confidence.
- `accent/clay` (terracotta) — erasure action, low confidence, unavailable.
- `accent/slate` — hybrid ranking indicator, description badge.
- Light and dark variants for all icons.

**Key design note for match-location badges:** these icons appear at 16 px in
lists alongside text. They must remain legible and distinguishable from one
another at 16 px. Please ensure the vendor set includes a crisp 16 px version
of each badge icon with sufficient color differentiation.

**Animated icon note:** `ic_embedding_generating` should have an animated
variant (CSS animation or an Avalonia-driven frame sequence) for the "in
progress" state, in addition to the static PNG. If the vendor does not supply
animation, a static spinning-variant PNG series (8 frames @1x) is acceptable.

**Size matrix:** 16/24/32/48 px @1x/2x/3x (badges primarily 16/24; action
icons 24/32/48).

**License:** same redistribution terms as prior phases.

**Delivery:** `OgmaLibrary.App/Assets/icons/search/semantic/` and
`OgmaLibrary.App/Assets/icons/search/badges/`.

Shipping with `🟨` placeholder icons is a release blocker.
## Current implementation note

As of 2026-06-01, Phase 11 UI uses existing catalog icons as local development
placeholders so the semantic-search surfaces are visually inspectable:

- Semantic active/degraded indicator: `ic_ai_advisor` / `ic_status_unavailable`.
- Match-location badges: Phase 10 search/filter chip icons plus `ic_ai_advisor`.
- Confidence indicators: status available/loading/unavailable icons.
- Embedding erasure action: `ic_ai_privacy`.

These placeholders keep the product usable during implementation, but they do
not satisfy the premium icon procurement gate for public beta.
