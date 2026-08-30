# Skills Engine Application

## Audit basis

This audit applies the canonical Chwezi Core Systems engines in place. Engine repositories were read as referenced sources and were not copied or modified. The stack was deliberately narrow: formal requirements and traceability, desktop software architecture, design-system review, host-platform operations, and evidence discipline. Website guidance is used only where its quality gates transfer to an embedded desktop UI; Ogma is not being planned as a website, PWA, or mobile application.

| Skill Engine | Relevance | Principles Applied | Ogma Areas Influenced |
| --- | --- | --- | --- |
| `srs-skills` | Primary | Requirement atomicity; normative IDs; conflict logging; source-to-code-to-test traceability; conservative status; state-transition and acceptance-evidence checks | SDLC reconciliation, RTM, processing states, NFRs, acceptance criteria, risk and test strategy |
| `skills-web-dev` | Primary | Modular boundaries; dependency direction; composition roots; DTO/validation boundaries; database evolution; provider adapters; defensive error handling; background work; security; Kaizen | C# architecture, Avalonia composition, EF/SQLite, workers, search, AI/RAG, testing and release engineering |
| `design-system-skills` | Primary | Tokenised visual language; typography and hierarchy; platform-appropriate interaction; accessible equivalents; responsive density; loading/empty/error states; performance budgets | Application shell, catalogue, reader, advisor, metadata review, covers, 3D fallback and accessibility |
| `windows-admin-engine-skills` | High | Canonical-path handling; DPAPI/credential protection; child-process containment; permissions; signing; clean-machine packaging; Windows operational evidence | Windows filesystem security, PDF worker, secrets, MSIX/Velopack, diagnostics and install validation |
| `linux-skills` | Conditional | Idempotent workers; service lifecycle; structured logs; resource limits; least privilege; backup/restore and production runbooks | Transferable worker/process concepts and any future CI/host operations; not a Linux client target |
| `digital-research-skills` | High | Evidence hierarchy; source evaluation; separate fact from inference; record unknowns; avoid unsupported external claims | Reconciliation of DOCX claims, code evidence, dependency evidence, risk statements and confidence levels |
| `website-skills` | Limited | Quality gates for navigation, content clarity, progressive disclosure, performance and accessibility | Embedded WebView and desktop UI quality only; public website delivery is excluded from the 39 application phases |

## How the guidance changed this audit

- A passing test, route, view, table, or service registration is not treated as an implemented capability unless its user trigger, validation, domain behavior, persistence, failure handling, result, and acceptance evidence are connected.
- Historical documentation evidence is not carried forward automatically. The v2.1 evidence pack describes commit `26df983...`; this audit revalidates current commit `5514276...` and labels unrerun physical or operational gates `NOT ASSESSED`.
- AI quality is evaluated as retrieval, grounding, explanation and failure isolation, not as the ability to return text.
- Visual review separates the existence of Avalonia controls from a coherent, accessible, production-quality design system.
- Windows and macOS are the only client platforms in scope. There are no mobile phases and no mobile-readiness architecture requirement.

## Evidence rules used

1. Latest controlled SDLC documents under `docs/references/` lead, subject to documented conflicts.
2. Executable code and migrations outrank README or plan claims about implementation.
3. Tests prove only what their assertions exercise; mock and headless tests do not prove physical-platform, provider, GPU, signing, or usability behavior.
4. Build, analyzer, dependency and test results are dated and commit-specific.
5. Missing evidence is not a pass. Where execution was impossible in this environment, the report states the gap rather than inferring success.

