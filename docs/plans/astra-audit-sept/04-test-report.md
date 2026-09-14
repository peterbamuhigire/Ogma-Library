# Test and platform audit report

Audit date: 2026-09-14 (Africa/Kampala)  
Repository: `C:\\wamp64\\www\\Ogma-Library`  
Commit under test: `a93900a2bd7313a2948a29d2813ec3e14696f877`  
Host observed: Windows 11 build `10.0.26200`, x64; .NET SDK `10.0.101`; MSBuild `18.0.6`; VSTest `18.0.1`; Node `v24.8.0`; npm `11.6.0`.

This is an audit record, not a release approval. Commands were run against the
working tree without changing application source, tests, thresholds, package
versions, or platform configuration. Generated logs and TRX files are under
`evidence/tests/`.

## Gate results

| Gate | Exact command | Result |
| --- | --- | --- |
| Requirement accountability | `./scripts/Test-RequirementAccountability.ps1` | **FAIL, exit 1.** The required canonical SRS `docs/references/Ogma-Library_SRS_v2.1_2026-08-13.docx` is absent. No substitute was used. |
| Locked restore | `dotnet restore OgmaLibrary.sln --locked-mode` | **PASS, exit 0.** All 10 solution projects restored. |
| Release build | `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | **PASS, exit 0.** 0 warnings, 0 errors; elapsed 1:29.88. |
| Serial solution tests | `dotnet test OgmaLibrary.sln --configuration Release --no-build --verbosity normal -m:1` | **PASS, exit 0.** Architecture 42/42, core 955/955, UI 163/163: 1,160 passed, 0 failed. The full-run log also records 0 aborted/not-executed tests. |
| Architecture replay | `dotnet test tests/OgmaLibrary.Tests.Architecture/OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --verbosity normal -m:1` | **PASS, exit 0.** 42/42; dedicated TRX saved. |
| Core replay | `dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-build --verbosity normal -m:1` | **PASS, exit 0.** 955/955; dedicated TRX saved; elapsed 9.097 minutes. |
| Format verification | `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | **FAIL, exit 2.** Extensive existing whitespace/end-of-line diagnostics and import-order diagnostics were reported; no formatting changes were applied. |
| Analyzer verification | `dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn --verbosity minimal` | **PASS, exit 0.** |
| NuGet vulnerability query | `dotnet list OgmaLibrary.sln package --vulnerable --include-transitive` | **PASS, exit 0.** Every listed project reported no vulnerable packages from `https://api.nuget.org/v3/index.json` at run time. |
| shelf3d dependency install | `npm ci` in `src/shelf3d` | **PASS, exit 0.** 37 packages added; npm reported 0 vulnerabilities. |
| shelf3d typecheck | `npm run typecheck` | **PASS, exit 0.** `tsc --noEmit -p tsconfig.json`. |
| shelf3d build | `npm run build` | **PASS, exit 0.** Generated `shelf3d.js` and `shelf3d.build.json` in the ignored/generated asset output. |
| shelf3d performance budget | `npm run perf:budget` | **PASS, exit 0.** All printed shelf/grid3d p95 samples and residency checks completed; raw values are in the evidence log. |

Evidence files:

- `requirement-accountability.log`
- `dotnet-restore-locked.log`, `dotnet-build-release.log`
- `dotnet-test-release-serial.log`, `dotnet-test-architecture-serial.log`, `dotnet-test-core-serial.log`
- `trx/OgmaLibrary-Architecture.trx`, `trx/OgmaLibrary-Core.trx`, `trx/OgmaLibrary-Ui.trx`
- `dotnet-format-verify.log`, `dotnet-format-analyzers.log`, `dotnet-vulnerable-packages.log`
- `shelf3d-npm-ci.log`, `shelf3d-typecheck.log`, `shelf3d-build.log`, `shelf3d-perf-budget.log`

## What the green tests actually prove

The test projects are xUnit tests run under the Avalonia headless Skia-backed
harness (`tests/OgmaLibrary.Tests.Ui/TestAppBuilder.cs`). The UI suite proves
selected view-model and headless control behavior, accessibility names for
some controls, culture/resource behavior, and screenshot generation. It does
not prove physical Windows or macOS compositor layout, native keyboard focus,
window hit-testing, packaging, signing, notarization, or a real WebView host.

The command-palette test at
`tests/OgmaLibrary.Tests.Ui/SearchViewModelTests.cs:246` opens the
`MainShellViewModel`, filters an item, executes `search`, and asserts state at
`:265-276`. It does not instantiate `DesktopShellWindow`; it does not assert
the overlay's measured bounds, focus transfer, mouse hit-testing, a visible
close affordance, or physical Escape handling. The production handlers are in
`src/OgmaLibrary.App/Views/DesktopShellWindow.axaml.cs:80-114`, while the
overlay is declared at `src/OgmaLibrary.App/Views/DesktopShellWindow.axaml:22-66`.
Therefore the green unit/headless command-palette test is not evidence against
the native observations recorded by the root audit.

Several tests use generated or synthetic content. For example,
`PdfiumAdapterPasswordTests` creates a one-page password fixture at runtime,
and `Phase24RealOcrCorpusTests` renders a generated scanned fixture. These
prove the bounded fixture paths exercised by those tests, not an arbitrary
user PDF corpus. The test suite contains benchmark-tagged checks, but those
are local runtime baselines, not cross-machine performance certification.

## Platform and provider boundaries

The execution host was Windows. Windows build, headless tests, and the
Windows-only packaged Tesseract path were exercised. macOS physical execution,
macOS native PDFium/Skia loading, WKWebView, Keychain, app bundle layout,
codesigning, notarization, Gatekeeper launch, and a macOS real-PDF corpus are
**NOT ASSESSED**. No macOS machine or signing identity was available.

This distinction is material in the test code: the packaged OCR test returns
after printing `NOT ASSESSED` when the OS is not Windows
(`tests/OgmaLibrary.Tests/Ocr/Phase24RealOcrCorpusTests.cs:19-24`), and the
Windows permission-denial write-back case returns immediately off Windows
(`tests/OgmaLibrary.Tests/Metadata/PdfWriteBackTests.cs:277-280`). A future
macOS run must report those cases as platform evidence, not infer them from a
Windows green count.

External metadata providers, cloud AI providers, Ollama, live LAN peers,
physical classroom devices, and arbitrary user-owned PDFs were not used in
this run. Stubbed/fail-closed provider tests are evidence of the tested
adapter contracts only. Live provider correctness, rate limits, credentials,
and network interoperability are **NOT ASSESSED**.

## Native audit linkage and strongest finding

The root audit captured fresh native Windows evidence in this same audit
directory (PNG/UIA pairs named `fresh-*` and reader evidence). Those artifacts
are the source for native visual and interaction findings. In particular, the
root reported fresh native dismissal failures for command-palette Escape/
command interaction and detail Close, plus visual concerns about the palette's
prominence and clarity. Those observations remain findings even though the
headless suite is green, because the relevant headless test does not cover the
native window route described above. This report does not claim to reproduce
or fix those native findings.

## Disposition

Observed: restore, Release build, serial tests, analyzer verification, NuGet
vulnerability query, and all shelf3d gates passed on the Windows host.

Observed: requirement accountability and format verification failed with the
exit codes above.

Inference: the green automated suite demonstrates substantial bounded contract
coverage but cannot establish Windows/macOS release readiness or visual/native
interaction correctness.

Unassessed: macOS execution and packaging/signing/notarization; live providers,
physical devices, and arbitrary user PDF corpus behavior; any gate not listed
as executed in this report.
