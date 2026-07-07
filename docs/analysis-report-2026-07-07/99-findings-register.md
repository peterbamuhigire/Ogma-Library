# Findings Register

Date: 2026-07-07
Baseline score: 57.0 / 100

| ID | Dimension | Severity | Status | Primary location | Rule violated | Consequence |
| --- | --- | --- | --- | --- | --- | --- |
| F-BLD-001 | Build | Critical | Resolved - Phase 01: SQLite native dependency resolves to audited 3.0.3 bundle with warnings-as-errors preserved. | `Directory.Build.props:14`; `src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj:36` | NuGet advisories must be fixed without weakening warnings-as-errors. | Restore/build/test are blocked. |
| F-BLD-002 | Build | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_ADRs.txt:1` | Runtime/package alignment must be ratified. | EF Core/net10 support decision remains open. |
| F-BLD-003 | Build | Medium | Open | Repo-wide package policy | Dependency graph must be reproducible and auditable. | Transitive risk can recur. |
| F-ARCH-001 | Architecture | High | Open | `docs/plans/grand-plan/phase-19/README.md:163` | Release-critical controls must be implemented before beta. | Architecture is not beta-ready. |
| F-ARCH-002 | Architecture | High | Open | `src/OgmaLibrary.Application/Ai/AdvisorService.cs:56` | V2 scaffolds must not masquerade as complete workflows. | AI answer mode is incomplete. |
| F-ARCH-003 | Architecture | Medium | Open | `docs/plans/grand-plan/phase-15/README.md:50` | Placeholder workflows must be completed or removed. | Split-view workflow is incomplete. |
| F-ARCH-004 | Architecture | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_ADRs.txt:1` | Signature 3D platform gate must pass. | macOS 3D risk remains. |
| F-SEC-001 | Security | Critical | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | Security hardening gate must close before beta. | Unsafe release risk. |
| F-SEC-002 | Security | High | Open | `docs/plans/grand-plan/phase-19/README.md:282` | At-rest encryption decision/proof required. | Lost-device data exposure. |
| F-SEC-003 | Privacy | Critical | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DPIA.txt:1` | Minor/student processing requires jurisdiction/controller decisions. | Legal/privacy exposure. |
| F-SEC-004 | Security | High | Open | `src/OgmaLibrary.Infrastructure/LanHost/KestrelHostModeListener.cs:219` | LAN host attack surface requires threat-tested controls. | Local network abuse risk. |
| F-SEC-005 | Security | Medium | Open | `src/OgmaLibrary.Application/Ai/IAiProviderFactory.cs:14` | Provider secrets need explicit lifecycle controls. | Misconfiguration or leakage risk. |
| F-DATA-001 | Data | Medium | Open | `src/OgmaLibrary.Infrastructure/Catalogue/CatalogueMigrator.cs:139` | Migrations must be deterministic and repair paths bounded. | Schema drift risk. |
| F-DATA-002 | Data | Medium | Open | `src/OgmaLibrary.Infrastructure/Catalogue/Entities/EnrolledProfileRow.cs:18` | Comments/schema must match sensitive behavior. | Maintainer confusion on token storage. |
| F-DATA-003 | Data | Medium | Open | `src/OgmaLibrary.Infrastructure/Catalogue/CatalogueDbContext.cs:220` | SQLite durability needs operational proof. | Backup/restore confidence incomplete. |
| F-FUNC-001 | Functionality | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_TestCompletionReport.txt:1` | Large-library health tests must pass. | Metadata health unreliable at scale. |
| F-FUNC-002 | Functionality | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | PRD product requires beta gates to close. | Users cannot receive working released software. |
| F-FUNC-003 | Functionality | Medium | Open | `src/OgmaLibrary.Application/Ai/AdvisorService.cs:56` | V1 must not rely on V2 placeholders. | Reader/AI workflows stop early. |
| F-FUNC-004 | Functionality | Medium | Open | `src/OgmaLibrary.App/Views/Catalogue/CatalogueGridView.axaml:43` | Placeholder assets cannot ship as finished product. | Product feels unfinished. |
| F-UI-001 | UI/UX | High | Open | `docs/plans/grand-plan/phase-10/icons.md:96` | Placeholder icons are release blockers. | Premium UI bar not met. |
| F-UI-002 | UI/UX | Medium | Open | `src/OgmaLibrary.App/Views/Classroom/StudentSmartSearchView.axaml:60` | User-facing copy must be localized. | Localization incomplete. |
| F-UI-003 | UI/UX | Medium | Open | `src/OgmaLibrary.App/App.axaml:7`; `src/OgmaLibrary.App/Program.cs:23` | Premium visual language requires audit evidence. | Default-toolkit feel risk. |
| F-UI-004 | Accessibility | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | WCAG 2.2 AA gate must close. | Accessibility compliance unproven. |
| F-UI-005 | UI/UX | Medium | Open | `src/OgmaLibrary.App/Views/Reader/ReaderView.axaml:39` | Toolbar controls need proper icon assets and labels. | Reader polish/accessibility inconsistency. |
| F-TEST-001 | Testing | Critical | Resolved - Phase 01: canonical restore/build/test suite passes with NuGet audit enabled. | `dotnet restore OgmaLibrary.sln` | Canonical tests must run with audit enabled. | Regression baseline invalid. |
| F-TEST-002 | Testing | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_TestCompletionReport.txt:1` | Full suite must be green. | Known failure remains. |
| F-TEST-003 | Testing | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_TestStrategy.txt:1` | Public-beta gates need platform evidence. | Local tests cannot authorize beta. |
| F-TEST-004 | Testing | Medium | Open | `docs/qa/PHASE-09-A11Y-SIGNOFF.md:75` | Automated render tests do not replace manual a11y. | Assistive-tech confidence incomplete. |
| F-PERF-001 | Performance | High | Open | `docs/plans/grand-plan/phase-20/README.md:424` | 3D FPS gate must pass on macOS. | Signature feature may degrade. |
| F-PERF-002 | Performance | High | Open | `docs/governance/REFERENCE-HARDWARE.md:77` | NFRs require reference-hardware measurements. | Performance readiness unproven. |
| F-PERF-003 | Observability | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | SLOs need instrumentation. | Beta regressions hard to detect. |
| F-PERF-004 | Reliability | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_TestCompletionReport.txt:1` | Load/retry path must pass. | Large-library reliability gap. |
| F-REL-001 | Release | Critical | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | Packaging/channel feeds must exist. | No supported install/update path. |
| F-REL-002 | Release | Critical | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | Signing/notarization must be operational. | Builds are untrusted. |
| F-REL-003 | Release | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | Update trust and rollback must be tested. | Bad update recovery unproven. |
| F-REL-004 | Release | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | Beta requires go/no-go and drills. | Launch unsupported. |
| F-DOC-001 | Docs | High | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_ADRs.txt:1` | Proposed ADRs must be resolved. | Binding decisions remain open. |
| F-DOC-002 | Docs | Medium | Open | `docs/plans/grand-plan/phase-10/evidence.md:35` | Status must distinguish partial and blocked work. | Implementation agents can miss blockers. |
| F-DOC-003 | Docs | Medium | Open | `artifacts/reference-extracts-2026-07-07/Ogma-Library_TestCompletionReport.txt:1` | Docs and repo state must remain synchronized. | Plan needs consolidated evidence. |
