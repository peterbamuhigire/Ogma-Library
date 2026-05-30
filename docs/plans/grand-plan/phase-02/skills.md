# Phase 02 — Skills & Slash Commands

> Phase-scoped invocation guide. Bird's-eye map: `SKILLS-INDEX.md §Part I Phase 02`.

---

## Always-on

| Skill / command | When | Expected artifact |
| --- | --- | --- |
| `superpowers:test-driven-development` | Before WP4 (architecture tests), WP7 (i18n analyzer tests), WP8 (benchmark interface tests) | Tests written before the implementation; each test asserts a specific behavioral claim traceable to an FR/NFR/ADR ID |
| `superpowers:verification-before-completion` | At the end of each WP and before Phase 02 close | Checklist run; specifically: CI matrix green on both runners before any "done" claim |
| `superpowers:systematic-debugging` | If any WP fails (build error, test failure, CI failure) | Root-cause analysis before modifying the code; hypothesis → reproduction → fix cycle |
| `superpowers:requesting-code-review` + `/code-review --effort high` | WP10 (final review) | High-effort review of the domain model, architecture tests, and i18n analyzer; findings resolved before merge |

---

## WP1 — Solution + build configuration

| Skill | Task | What to produce |
| --- | --- | --- |
| `language-standards` (C# / .NET) | P02-WP1-T2 (`Directory.Build.props`) | A `Directory.Build.props` that encodes the full set of enforced properties; the skill provides the canonical set of analyzer NuGet packages and their `PrivateAssets="all"` configuration |
| `sdlc-meta:sdlc-design` | P02-WP1-T1 (solution structure) | Validate the 9-project layout against the HLD §F 9-project specification and the SOURCE-SUMMARY §F bounded-context model before creating any files |

### Concrete invocation: Directory.Build.props

Use `language-standards` to generate the full `Directory.Build.props` including:
- All enforced properties (see README §6).
- The exact NuGet package references for SonarAnalyzer.CSharp,
  StyleCop.Analyzers, and Roslynator.Analyzers, pinned to the versions
  confirmed in Phase 01 Spike 1.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` must appear before the
  package references so that analyzer warnings are errors from first import.

---

## WP2 — Domain model skeleton

| Skill | Task | What to produce |
| --- | --- | --- |
| `architecture:system-architecture-design` | P02-WP2-T1..T8 | Validate each entity design against bounded-context discipline: no entity in `Domain` references a type from `Application` or `Infrastructure`; no persistence attributes (EF Core) in `Domain` entities |
| `architecture:validation-contract` | P02-WP2-T8 (repository interfaces) | Repository interfaces that follow the contract design principles: all methods are `Task`-returning (async-first), accept `CancellationToken`, and return domain types (not EF Core proxies or DTOs); the skill confirms the interface contracts are correct before implementation in Phase 04 |
| `sdlc-meta:world-class-engineering` | P02-WP2-T9 (XML doc coverage) | The standard for XML doc comments: every `///` block must include `<summary>`, and for value objects, `<remarks>` on validation invariants; `<exception cref="...">` for factory methods that throw |

### Key design constraint for domain model

The `superpowers:brainstorming` skill is recommended before P02-WP2-T2 to
explore the `Book` / `BookFile` / `Work` / `Edition` cardinality model (CON-5
from Phase 00). The brainstorming output feeds the domain model design and the
owner sign-off request (Owner ask #3 in README §13).

---

## WP4 — Architecture tests

| Skill | Task | What to produce |
| --- | --- | --- |
| `comprehensive-review:architect-review` | P02-WP4-T1..T5 | An architect review of the three proposed architecture rules; confirmation that they are sufficient for Phase 02 or an expanded rule set (e.g. adding "no UI code in Domain or Application"). The review is an input, not an output — the skill informs the rule design before the tests are written. |
| `sdlc-meta:advanced-testing-strategy` | P02-WP4-T2..T4 | The test design: what `NetArchTest` predicates to use (`HaveDependencyOnAny`, `HaveNameEndingWith`, `ResideInNamespaceContaining`); how to handle false positives (types that are legitimately in a different assembly but look like violations) |

---

## WP5 — CI workflow

| Skill | Task | What to produce |
| --- | --- | --- |
| `cicd-pipelines` / `cicd-pipeline-design` | P02-WP5-T1..T5 | A GitHub Actions workflow file (`ci.yml`) with: matrix strategy, caching of NuGet packages (use `actions/cache` keyed on `Directory.Build.props` hash to avoid re-downloads), and a clear step naming convention so CI output is readable |
| `cicd-devsecops` | P02-WP5-T1 | A `ci.yml` step that runs `dotnet list package --vulnerable --include-transitive` and fails the build if any high-severity vulnerability is found; this is the baseline dependency security check |

### Concrete invocation: NuGet caching in ci.yml

Use `cicd-pipelines` to generate the NuGet cache step:
```yaml
- name: Cache NuGet
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: nuget-${{ runner.os }}-${{ hashFiles('**/Directory.Build.props', '**/*.csproj') }}
    restore-keys: nuget-${{ runner.os }}-
```
This reduces CI time from ~3 min (cold restore) to ~30 s (warm cache) per runner.

---

## WP6 — Golden-corpus test harness

| Skill | Task | What to produce |
| --- | --- | --- |
| `sdlc-meta:e2e-testing` | P02-WP6-T2..T4 (ManifestVerifier, SyntheticCorpusGenerator) | The fixture framework design: how to share the `ManifestVerifier` across all integration test classes using xUnit's `IClassFixture<GoldenCorpusFixture>`; how to make the `SyntheticCorpusGenerator` produce the same output regardless of thread scheduling (pure-function, no side effects) |
| `sdlc-meta:advanced-testing-strategy` | P02-WP6-T3 (determinism test) | The determinism test design: run `SyntheticCorpusGenerator(seed: 42, count: 500)` twice; serialize both outputs to JSON; assert the JSON strings are equal (structural equality, not reference equality) |

---

## WP7 — i18n analyzer + pseudolocale

| Skill | Task | What to produce |
| --- | --- | --- |
| `sdlc-meta:advanced-testing-strategy` | P02-WP7-T3 (analyzer unit test) | Use Roslyn's `Microsoft.CodeAnalysis.CSharp.Testing` to write a `CSharpAnalyzerVerifier<HardCodedStringAnalyzer>` test; the skill provides the test template and the pattern for specifying expected diagnostics by location |

### Concrete invocation: Roslyn analyzer test

The `superpowers:test-driven-development` skill must be invoked **before**
writing `HardCodedStringAnalyzer.cs` (WP7-T2). The TDD cycle:
1. Write the failing test (`HardCodedStringAnalyzerTests.cs`): a class with
   `myButton.Content = "Submit";` that expects `OGMA0001` at a specific
   character offset.
2. Write the minimal analyzer that makes the test pass.
3. Write the passing test (no diagnostic on `_loc.Get("key")`).
4. Confirm both tests pass with `dotnet test`.

This is the only TDD cycle in Phase 02 that produces a Roslyn component; the
other TDD cycles (architecture tests, benchmark interface tests) are simpler.

---

## WP8 — Performance instrumentation

| Skill | Task | What to produce |
| --- | --- | --- |
| `devops-cloud:reliability-engineering` | P02-WP8-T1..T4 | The `IBenchmarkContext` interface design: specifically, confirm that `IDisposable`-based scope (rather than a start/stop method pair) is the right abstraction; the skill confirms this is a standard pattern for wall-clock instrumentation in .NET |

---

## WP9 — Open-source documentation

| Skill / command | Task | What to produce |
| --- | --- | --- |
| `/init` | P02-WP9-T1 (CLAUDE.md) | A `CLAUDE.md` file that a new Claude Code session can read to understand the project's build, test, and contribution conventions without asking the user |
| `documentation-generation:docs-architect` | P02-WP9-T2 (developer guide) | A `docs/developer-guide/README.md` that a new contributor can follow from clone to first green `dotnet test` without additional help; the skill ensures the guide is complete, technically accurate, and follows the project's documentation style |
| `documentation-generation:changelog-automation` | P02-WP9-T3 (CHANGELOG.md) | A `CHANGELOG.md` entry following the Keep a Changelog format; the skill generates the entry from the Phase 02 PR commit list |

---

## WP10 — Code review + merge

| Skill / command | Task | What to produce |
| --- | --- | --- |
| `superpowers:requesting-code-review` | P02-WP10-T1 | A code-review request that frames the review scope: domain model correctness, architecture test coverage, i18n analyzer correctness, CI completeness |
| `/code-review --effort high` | P02-WP10-T1 | A high-effort review of the Phase 02 PR; findings logged as review comments; resolved before merge |
| `superpowers:finishing-a-development-branch` | P02-WP10-T3 | Guides the merge decision (squash vs merge commit), the branch cleanup, and the `CHANGELOG.md` entry confirmation |

---

## Notes on Avalonia standards

Phase 02 creates the Avalonia `App` project but implements only a hello-world
main window. The `avalonia-desktop-development` skill and
`docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` (being authored in
parallel) should be consulted for:

- The correct `AppBuilder` configuration pattern for .NET 10 + Avalonia 11.x.
- The `UseHeadless()` test configuration pattern (used in WP7-T5 pseudolocale
  test).
- The MVVM binding pattern (`CommunityToolkit.Mvvm` vs ReactiveUI vs vanilla
  `INotifyPropertyChanged`) — a decision that must be made in Phase 02 and
  confirmed against AVALONIA-STANDARDS.md before the hello-world window is
  written.

If `AVALONIA-STANDARDS.md` is not yet available when WP3 starts, default to
`CommunityToolkit.Mvvm` (the lighter dependency) and file a tracking note to
align with the standards document when it is published.
