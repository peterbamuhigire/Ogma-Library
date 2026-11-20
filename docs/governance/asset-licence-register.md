# Asset licence register

Every bundled visual asset (fonts, icons, images) is listed here with its source, licence,
version and date. An asset without a row here must not ship (`scripts/Test-DesignTokens.ps1`
and `Sept23Phase09DesignSystemTests` enforce the icon side). Owner decision D-07 (Sept-23
Kaizen): an open-licence, consistent, colourful icon set with a recorded licence is the interim;
the owner's premium purchase is a later drop-in that replaces the SVGs key for key.
Machine-learning model weights and the native inference packages that run them are recorded here
too (Sept-23 Phase 14), whether they ship or are downloaded on first use.

| Asset | Path | Source | Version | Licence | Redistribution in the app | Recorded |
|---|---|---|---|---|---|---|
| Icon set "Lamplit" duotone (85 SVGs, one per `IconCatalog` key) | `src/OgmaLibrary.App/Assets/icons/**` | Lucide outlines (<https://github.com/lucide-icons/lucide>) taken from the npm package `@iconify-json/lucide` (package.json `"license": "ISC"`; info.json licence URL <https://github.com/lucide-icons/lucide/blob/main/LICENSE>); recoloured as duotones by `scripts/icons/New-DuotoneIcons.py`; glyph-per-key map in `scripts/icons/icon-map.txt`; source bodies vendored in `scripts/icons/lucide-subset.json` | 1.2.110 | ISC (portions MIT, Feather) | Permitted, notice retained in `Assets/icons/LICENSE-ICONS.txt` | 2026-09-25, Phase 09 |
| Spectral (display) | `src/OgmaLibrary.App/Assets/Fonts/Spectral/` | Production Type / Google Fonts | as bundled | SIL OFL 1.1 (`OFL.txt`) | Permitted with licence file | Phase 18 (grand plan), re-confirmed Phase 09 |
| Public Sans (body, UI) | `src/OgmaLibrary.App/Assets/Fonts/PublicSans/` | USWDS / Google Fonts | Static Regular/Medium/SemiBold/Bold instances (wght 400/500/600/700) cut from the bundled variable font with fontTools 4.63 `varLib.instancer`; the variable file was removed because Avalonia ignores its axes and rendered its Thin default | SIL OFL 1.1 (`OFL.txt`); no Reserved Font Name, so the modified files keep the family name | Permitted with licence file | Phase 18, re-confirmed and instanced Phase 09 |
| JetBrains Mono (data) | `src/OgmaLibrary.App/Assets/Fonts/JetBrainsMono/` | JetBrains | as bundled | SIL OFL 1.1 (`OFL.txt`) | Permitted with licence file | Phase 18, re-confirmed Phase 09 |
| Generated covers, progress rings, paper grain | code (`Views/Catalogue/GeneratedCover.cs`, `ProgressRing.cs`, `Themes/Tokens.axaml`) | Original work in this repository | n/a | Repository licence | Permitted | 2026-09-25, Phase 09 |
| App icon | `src/OgmaLibrary.App/Assets/app*.png`, `app.ico`, `app.icns` | Pre-existing | n/a | NOT ASSESSED (provenance not recorded before Phase 09) | NOT ASSESSED | Owner to confirm |
| Embedding model weights `intfloat/multilingual-e5-small`: `onnx/model_qint8_avx512_vnni.onnx` (118,346,824 B, SHA-256 `dd476dd0c2514e9b9be83aeb3853fac0763e0bdf4a71645407587d77c48a2d88`) and `sentencepiece.bpe.model` (5,069,051 B, SHA-256 `cfc8146abe2a0488e9e2a0c56de7952f7c11ab059eca145a0a727afce0db2865`) | **Not in git and not in the installer.** Downloaded on first use, after the user's explicit consent, into the app-data folder (D-103, D-104) | <https://huggingface.co/intfloat/multilingual-e5-small> | Revision `614241f622f53c4eeff9890bdc4f31cfecc418b3` (pinned; files pinned by SHA-256) | MIT (model card `license: mit`) | Permitted; the MIT notice is shown in Settings and kept beside the model files (Phase 14 slice 3) | 2026-11-20, Phase 14 slice 1 (CUR-25) |
| Fallback embedding model `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` (`onnx/model_quint8_avx2.onnx`, SHA-256 `98a01d88b7de996cdea58c32ca71208c09968d143798814b2ea09d3439dc334f`) | Not used; recorded as the fallback only | <https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2> | Revision `e8f8c211226b894fcb81acc59f3b34ba3efd5f42` | Apache-2.0 (model card) | Would need the Apache-2.0 notice if adopted | 2026-11-20, Phase 14 slice 1 (CUR-26) |
| NuGet `Microsoft.ML.OnnxRuntime` (CPU inference, native runtimes) | Planned for `src/OgmaLibrary.Infrastructure` (Phase 14 slice 3; not referenced yet) | <https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/1.32.0> | 1.32.0 | MIT (package `LICENSE`: "MIT License, Copyright (c) Microsoft Corporation") | Permitted with notice | 2026-11-20, Phase 14 slice 1 (CUR-23) |
| NuGet `Microsoft.ML.Tokenizers` (SentencePiece unigram tokenizer) | Planned for `src/OgmaLibrary.Infrastructure` (Phase 14 slice 3; not referenced yet) | <https://www.nuget.org/packages/Microsoft.ML.Tokenizers/3.0.0> | 3.0.0 | MIT (nuspec licence expression) | Permitted with notice | 2026-11-20, Phase 14 slice 1 (CUR-24) |

## Superseded

| Asset | Reason | Date |
|---|---|---|
| Previous per-key SVGs (Flaticon downloads with `fi_*` ids and a Freepik "Capa_1" export) | No purchase record or licence file in the repository, so their redistribution right could not be verified; mixed styles. Replaced key for key by the Lamplit set. | 2026-09-25, Phase 09 |

## Replacing the icon set (premium drop-in)

1. Record the purchase (vendor, licence, order reference, redistribution terms) in this table first.
2. Drop the new files at the same `Assets/icons/<category>/<key>.svg` paths (or extend
   `IconCatalog` for PNG), then update `LICENSE-ICONS.txt`.
3. Run `./scripts/Test-DesignTokens.ps1` and the UI tests.
