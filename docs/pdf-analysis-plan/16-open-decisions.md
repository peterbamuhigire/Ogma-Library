# Open decisions

These decisions must be resolved by the named phase owner before the related
gate can close.

| Decision | Why it matters | Due phase | Default recommendation |
|---|---|---:|---|
| What exact PDF 2.0 feature subset is supported? | Prevents an impossible compliance claim | 1 | Reader baseline above; safe refusal for active content |
| Which errata/technical extensions are covered? | Currentness and reproducibility | 1/3 | Pin and publish versioned profile |
| Is PdfPig retained for extraction, upgraded, or replaced? | Current 0.1.9 may lag fixes; behavior must be measured | 3/7 | Keep behind adapter until corpus comparison |
| How is effective page geometry represented? | Fit, overlays, selection and navigation depend on it | 4/5 | One immutable canonical page model |
| Are annotations/forms rendered, interactive, or view-only? | Current render explicitly disables them | 9 | View-only safe rendering first; no active execution |
| Do we support optional-content toggles? | Affects fidelity and cache identity | 5/9 | Detect/report first, then add controlled toggles |
| Is continuous multi-page scroll required for v1? | Changes session and virtualisation model | 6 | Yes for premium reader; retain page fallback |
| What writer profile is supported? | Read support does not prove safe write-back | 9/12 | None until separately validated; DB-first remains default |
| Which PDF/UA behavior is promised? | Tagged input and UI accessibility are distinct | 7/11 | Preserve tags/structure where possible; publish UI/reader limits |
| Which PDFs are permissible in the corpus? | Legal/privacy ownership controls the evidence | 1/12 | Synthetic/open/licensed fixtures only |
