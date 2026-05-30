# Phase 19 — Icon Manifest (Security Hardening & Privacy / Compliance)

Phase 19 adds a modest set of icons for the **Security & Privacy settings**
surface and the compliance/audit views. Style follows `DECISIONS.md` **D‑001
(flat full‑color)** and is sourced from **Flaticon** in **SVG** (with exported
PNG @1x/2x/3x for Win + macOS HiDPI) per **D‑004 / D‑005**. Color families map to
function: `accent/slate` for settings, `accent/clay` for warnings/threats,
`accent/sage` for verified/OK, `accent/plum` for AI‑privacy, `accent/ink` for
audit/records. Every icon pairs with a localized text/`aria` label (en/fr now;
es/it/de in Phase 21) — color is never the sole carrier of meaning (WCAG 2.2 AA,
NFR‑PROD‑008).

| Icon key | Used on | Meaning | Style/color note | Sizes | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_security_center` | Settings nav, Security section | Entry to the Security & Privacy center | Shield, `accent/slate`+`sage` | 16/24/32/48 | ⬜ to procure |
| `ic_threat_model` | Security docs/dev surface | Threat model / STRIDE view (CTRL‑OGMA set) | Map + shield, `accent/slate` | 24/32 | ⬜ to procure |
| `ic_credential_store` | Provider keys / credentials | OS credential storage (CTRL‑OGMA‑001) | Key in vault, `accent/slate` | 16/24/32 | ⬜ to procure |
| `ic_key_remove` | Remove provider | Delete secret + audit (CTRL‑OGMA‑003) | Key with minus, `accent/clay` | 16/24 | ⬜ to procure |
| `ic_encryption_at_rest` | Catalogue encryption toggle | At‑rest encryption on (CTRL‑OGMA‑014/015) | Locked DB, `accent/sage` | 16/24/32 | ⬜ to procure |
| `ic_encryption_off` | Catalogue encryption toggle | At‑rest encryption off | Unlocked DB, `accent/slate` | 16/24/32 | ⬜ to procure |
| `ic_path_guard` | Library‑root validation surface | Path / library‑root validation (CTRL‑OGMA‑008..010) | Folder + shield, `accent/slate` | 16/24 | ⬜ to procure |
| `ic_untrusted_pdf` | Import / worker status | Untrusted‑PDF isolation (CTRL‑OGMA‑004..007) | Document + shield, `accent/clay` | 16/24/32 | ⬜ to procure |
| `ic_signed_verified` | Update / install | Signed build verified (CTRL‑OGMA‑012/013) | Badge + check, `accent/sage` | 16/24/32 | ⬜ to procure |
| `ic_signed_failed` | Update / install | Signature/tamper rejected | Badge + cross, `accent/clay` | 16/24/32 | ⬜ to procure |
| `ic_audit_trail` | Audit log view | Local tamper‑evident audit trail (CTRL‑OGMA‑018, NFR‑PROD‑013) | Ledger/list, `accent/ink` | 16/24/32 | ⬜ to procure |
| `ic_audit_export` | Audit log view | Export audit log | Ledger + arrow, `accent/ink` | 16/24 | ⬜ to procure |
| `ic_dpia` | Compliance surface | DPIA screening (CTRL‑OGMA‑024) | Clipboard + shield, `accent/slate` | 24/32 | ⬜ to procure |
| `ic_redaction` | Logs / telemetry | Secret redaction (CTRL‑OGMA‑002) | Eye‑off / blackbar, `accent/slate` | 16/24 | ⬜ to procure |
| `ic_consent_region` | Provider first‑use | Processing region / cross‑border notice (CTRL‑OGMA‑021) | Globe + check, `accent/plum` | 16/24 | ⬜ to procure |
| `ic_minor_data` | School / classroom compliance | Minors' data safeguard (GDPR‑K / COPPA / FERPA / DPPA) | Shield + person, `accent/clay` | 16/24/32 | ⬜ to procure |

> Several security states reuse icons defined in Phase 12 (Privacy Center:
> `ic_privacy_tier_*`, `ic_payload_preview`, `ic_no_training`) — do **not**
> re‑procure; reference the Phase 12 keys. The 16 keys above are net‑new to
> Phase 19.

## Owner procurement request

**Source:** Flaticon (your account) · **Style:** flat full‑color · **Format:**
SVG master + exported PNG @1x/2x/3x at the sizes listed · **Theme:** light + dark
treatment. Please add the 16 icons above to a single Flaticon **collection**
named `ogma-phase19-security` so the pack stays visually coherent with the rest
of Ogma (one consistent grid/stroke/corner radius). Map each `icon_key` to one
chosen Flaticon asset and record the asset URL/ID in
`_icons/MASTER-MANIFEST.md`. Confirm the Flaticon **Premium** licence (no
attribution required, commercial + app‑store redistribution permitted) before
these ship — see `DECISIONS.md` D‑005.
