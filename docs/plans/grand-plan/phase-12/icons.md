# Phase 12 — Icon Manifest

All icons introduced by the AI Gateway & Privacy Center phase. Style tokens are
defined in `ICON-SYSTEM.md §4`. Sizes: 16, 24, 32, 48 px at @1x, @2x, @3x.

Category path: `OgmaLibrary.App/Assets/icons/ai/` and `settings/`.

---

## Icon table

| Icon key | Used on | Meaning | Style / color note | Sizes | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_ai_tier_offline` | Privacy Center tier selector; status chip | AI is disabled / offline tier | Outlined cloud with a cross; `accent/slate` grey | 16/24/32/48 @1x-3x | ⬜ to procure |
| `ic_ai_tier_metadata` | Privacy Center tier selector; status chip | Metadata-only tier (cloud default) | Filled cloud with a tag/label symbol; `accent/oak` amber | 16/24/32/48 @1x-3x | ⬜ to procure |
| `ic_ai_tier_content` | Privacy Center tier selector; status chip | Content-aware tier (opt-in) | Filled cloud with a document symbol; `accent/plum` | 16/24/32/48 @1x-3x | ⬜ to procure |
| `ic_ai_tier_local` | Privacy Center tier selector; status chip | Local Ollama tier (no egress) | Computer/chip icon with a check; `accent/sage` green | 16/24/32/48 @1x-3x | ⬜ to procure |
| `ic_ai_payload_preview` | Payload preview dialog header | Inspect what will be sent | Magnifier over a document with data lines; `accent/ink` blue | 24/32/48 @1x-3x | ⬜ to procure |
| `ic_ai_provider_openai` | Provider selector; Privacy Center key row | OpenAI / compatible provider | OpenAI-style logomark (check licensing); `accent/slate` | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_ai_provider_anthropic` | Provider selector; Privacy Center key row | Anthropic / Claude provider | Anthropic-style logomark (check licensing); `accent/plum` | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_ai_provider_ollama` | Provider selector; Privacy Center key row | Local Ollama provider | Llama icon or local-server symbol; `accent/sage` | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_ai_key` | Privacy Center API key field | Secret key / credential | Key icon; `accent/clay` terracotta | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_ai_audit` | Privacy Center audit section header; export button | Audit trail | List with a clock/check badge; `accent/ink` | 24/32/48 @1x-3x | ⬜ to procure |
| `ic_ai_cost` | Per-call cost chip; session cost footer | Estimated AI cost | Coin or price-tag with a spark; `accent/clay` | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_ai_disable` | AI disable toggle; empty state when AI disabled | AI features turned off | Brain or spark icon with an off/slash overlay; `accent/slate` | 24/32/48 @1x-3x | ⬜ to procure |
| `ic_ai_consent` | Consent prompt dialog; consent status badge | User consent / privacy agreement | Shield with a checkmark; `accent/sage` | 24/32/48 @1x-3x | ⬜ to procure |
| `ic_ai_delete_history` | Delete-history action button | Erase query history | Trash icon with a clock/history badge; `accent/clay` | 24/32 @1x-3x | ⬜ to procure |
| `ic_ai_delete_embeddings` | Delete-embeddings action button | Erase local embeddings | Trash icon with a network/vector badge; `accent/clay` | 24/32 @1x-3x | ⬜ to procure |
| `ic_ai_export_audit` | Export-audit action button | Export audit log to file | Download arrow with a list/log symbol; `accent/ink` | 24/32 @1x-3x | ⬜ to procure |
| `ic_ai_no_training` | No-training consent toggle default indicator | Data not used for model training | Database with a prohibition/lock; `accent/plum` | 16/24/32 @1x-3x | ⬜ to procure |

---

## Accessible label resource keys

Every icon above has a paired localized label. Keys are in
`OgmaLibrary.App/Assets/i18n/en.resx` and `fr.resx`:

| Icon key | `en` label | `fr` label |
| --- | --- | --- |
| `ic_ai_tier_offline` | AI disabled (offline) | IA désactivée (hors ligne) |
| `ic_ai_tier_metadata` | Metadata only | Métadonnées uniquement |
| `ic_ai_tier_content` | Content-aware (opt-in) | Contenu activé (sur demande) |
| `ic_ai_tier_local` | Local AI (Ollama) | IA locale (Ollama) |
| `ic_ai_payload_preview` | Preview what will be sent | Aperçu des données envoyées |
| `ic_ai_provider_openai` | OpenAI-compatible provider | Fournisseur compatible OpenAI |
| `ic_ai_provider_anthropic` | Anthropic (Claude) provider | Fournisseur Anthropic (Claude) |
| `ic_ai_provider_ollama` | Local Ollama provider | Fournisseur Ollama local |
| `ic_ai_key` | API key | Clé API |
| `ic_ai_audit` | AI audit log | Journal d'audit IA |
| `ic_ai_cost` | Estimated cost | Coût estimé |
| `ic_ai_disable` | Disable AI features | Désactiver les fonctions IA |
| `ic_ai_consent` | Consent granted | Consentement accordé |
| `ic_ai_delete_history` | Delete query history | Supprimer l'historique des requêtes |
| `ic_ai_delete_embeddings` | Delete embeddings | Supprimer les embeddings |
| `ic_ai_export_audit` | Export audit log | Exporter le journal d'audit |
| `ic_ai_no_training` | Do not train on my data | Ne pas utiliser mes données pour l'entraînement |

---

## Owner procurement request

**To: Peter Bamuhigire**
**Re: Phase 12 icon set — AI Gateway & Privacy Center**

Please procure (or commission) a **17-icon set** for the AI Gateway and Privacy
Center surfaces in Phase 12. All icons must match the style family chosen in
Phase 03 (single vendor, consistent grid/stroke/corner radius).

**Style requirements (from `ICON-SYSTEM.md §4`):**

- Colorful "duotone" or "flat-color" style; warm, library-like aesthetic.
- Primary accent colors: `accent/plum` (AI surfaces), `accent/slate` (disabled/
  settings), `accent/sage` (success/local), `accent/clay` (warnings/cost/delete),
  `accent/ink` (audit/navigation), `accent/oak` (metadata-only tier).
- Light and dark variants where available from the vendor.

**Sizes and density matrix:**

| Size | Windows @1x | Windows @2x | Windows @3x | macOS Retina |
| --- | --- | --- | --- | --- |
| 16 px | 16×16 px | 32×32 px | 48×48 px | 32×32 px |
| 24 px | 24×24 px | 48×48 px | 72×72 px | 48×48 px |
| 32 px | 32×32 px | 64×64 px | 96×96 px | 64×64 px |
| 48 px | 48×48 px | 96×96 px | 144×144 px | 96×96 px |

**License requirement:** The license must permit redistribution inside a signed
desktop application distributed via the Microsoft Windows Store, the Mac App
Store, and as direct/GitHub downloads.

**Icon keys to procure (17 icons):**
`ic_ai_tier_offline`, `ic_ai_tier_metadata`, `ic_ai_tier_content`,
`ic_ai_tier_local`, `ic_ai_payload_preview`, `ic_ai_provider_openai`,
`ic_ai_provider_anthropic`, `ic_ai_provider_ollama`, `ic_ai_key`, `ic_ai_audit`,
`ic_ai_cost`, `ic_ai_disable`, `ic_ai_consent`, `ic_ai_delete_history`,
`ic_ai_delete_embeddings`, `ic_ai_export_audit`, `ic_ai_no_training`.

**Note on provider logos (`ic_ai_provider_openai`, `ic_ai_provider_anthropic`):**
Check vendor licensing carefully. If OpenAI or Anthropic trademarks restrict
redistribution in app-store products, use a generic "cloud AI" icon with the
provider name in text only. This is a legal question for the owner to confirm
before these icons are commissioned.

Placeholders are currently in use (`🟨 placeholder in use`) so build is not
blocked. Premium PNGs must be wired (status `✅`) before shipping any release.
