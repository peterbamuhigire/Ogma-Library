#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the Ogma Library real-window golden journeys (Sept-23 Phase 01, T01.12 runner contract).

.DESCRIPTION
    Builds an E2E copy of the app (Release, -p:OgmaE2EHooks=true, into artifacts/e2e/app), builds
    the E2E test project, then runs `dotnet test` with a filter and OGMA_E2E_* settings built from
    the parameters below. Evidence goes to artifacts/e2e/<run-id>/ (screenshots, UIA dumps,
    timings.json, app logs on failure, e2e.trx). The summary (results.md / results.json) labels each
    journey x size PASS, FAIL, BASELINE-FAIL (a known defect listed in baseline.json),
    PASS (baseline cleared) or NOT ASSESSED. A parameter a journey cannot honour is NOT ASSESSED for
    that journey, never a pass.

    Runs under Windows PowerShell 5.1 and PowerShell 7. Real-window journeys need an interactive
    Windows desktop session; on other operating systems every journey is NOT ASSESSED (Phase 27).

.EXAMPLE
    ./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -All -Sizes 1280x800,1920x1080
.EXAMPLE
    ./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Journey G1 -Sizes 1280x800
.EXAMPLE
    ./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Reader
#>
[CmdletBinding()]
param(
    # Every journey and tag.
    [switch]$All,
    # G1..G12, or a named scenario trait (A11yNames, Smoke, LaunchCycles, Harness, HostStartStop).
    [string[]]$Journey,
    # An xUnit Trait("Tag", ...): Reader, Search, Catalogue, Detail, Settings, Visual, Ai,
    # SemanticSearch, Shelf3D, Accessibility, Classroom, Harness.
    [string[]]$Tag,
    # Window sizes WIDTHxHEIGHT (outer window, physical pixels). Below 860x560 is rejected.
    [string[]]$Sizes = @('1280x800', '1920x1080'),
    [ValidateSet('Light', 'Dark')]
    [string[]]$Themes = @('Light'),
    [ValidateSet('en', 'fr', 'qps-ploc')]
    [string]$Culture = 'en',
    [switch]$KeyboardOnly,
    [switch]$UiaAudit,
    [ValidateSet(100, 200)]
    [int]$TextScale = 100,
    [switch]$ClippingCheck,
    [switch]$UseMockAiServer,
    # Skip the builds (use the existing artifacts/e2e/app and test build).
    [switch]$NoBuild,
    # Fail (exit 1) on BASELINE-FAIL and NOT ASSESSED too, not only on new failures.
    [switch]$Strict,
    # Drive a different exe (for example an older build) instead of the E2E build.
    [string]$Exe,
    [string]$RunId
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $PSScriptRoot 'OgmaLibrary.Tests.E2E.csproj'
$minWidth = 860
$minHeight = 560

function Fail([string]$message) {
    Write-Error $message -ErrorAction Continue
    exit 2
}

# ---- Validate parameters -------------------------------------------------------------------
if (-not $All -and -not $Journey -and -not $Tag) {
    Fail 'Specify -All, -Journey <id> or -Tag <name>.'
}

$sizeList = @()
foreach ($entry in $Sizes) {
    foreach ($size in ($entry -split ',')) {
        $size = $size.Trim().ToLowerInvariant()
        if (-not $size) { continue }
        if ($size -notmatch '^(\d+)x(\d+)$') {
            Fail "Window size '$size' is not WIDTHxHEIGHT (for example 1280x800)."
        }
        $w = [int]$Matches[1]; $h = [int]$Matches[2]
        if ($w -lt $minWidth -or $h -lt $minHeight) {
            Fail "Window size '$size' is below the shell minimum ${minWidth}x${minHeight} (DesktopShellWindow MinWidth/MinHeight). Use 1280x800 or larger."
        }
        $sizeList += $size
    }
}

$journeyList = @()
foreach ($entry in $Journey) { $journeyList += ($entry -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
foreach ($id in $journeyList) {
    if ($id -notmatch '^(G([1-9]|1[0-2])|[A-Za-z][A-Za-z0-9]+)$') {
        Fail "Journey '$id' is not G1..G12 or a named scenario trait."
    }
}
$tagList = @()
foreach ($entry in $Tag) { $tagList += ($entry -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }

$filter = 'Category=E2E'
if (-not $All) {
    $clauses = @($journeyList | ForEach-Object { "Journey=$_" }) + @($tagList | ForEach-Object { "Tag=$_" })
    $filter = "Category=E2E&(" + ($clauses -join '|') + ")"
}

if (-not $RunId) { $RunId = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss') }
$runRoot = Join-Path $repo "artifacts\e2e\$RunId"
New-Item -ItemType Directory -Force $runRoot | Out-Null

$isWindowsHost = ($PSVersionTable.PSEdition -eq 'Desktop') -or ($IsWindows -eq $true)
if (-not $isWindowsHost) {
    $note = "NOT ASSESSED: real-window journeys need a Windows desktop session (macOS automation is Phase 27)."
    Set-Content -Path (Join-Path $runRoot 'results.md') -Value $note -Encoding UTF8
    Write-Output $note
    exit 0
}

# ---- Build ---------------------------------------------------------------------------------
$appOut = Join-Path $repo 'artifacts\e2e\app'
if (-not $Exe) { $Exe = Join-Path $appOut 'OgmaLibrary.App.exe' }
$Exe = [IO.Path]::GetFullPath($Exe)
$exeDir = Split-Path -Parent $Exe

# Only ever stop processes started from the exe under test (never another lane's or the owner's app).
Get-Process OgmaLibrary.App, OgmaLibrary.Workers -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and ((Split-Path -Parent $_.Path) -eq $exeDir) } |
    Stop-Process -Force -ErrorAction SilentlyContinue

if (-not $NoBuild) {
    Push-Location $repo
    try {
        if ($Exe -eq [IO.Path]::GetFullPath((Join-Path $appOut 'OgmaLibrary.App.exe'))) {
            & dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration Release -p:OgmaE2EHooks=true -o $appOut
            if ($LASTEXITCODE -ne 0) { Fail 'The E2E app build failed.' }
        }
        & dotnet build $project --configuration Release
        if ($LASTEXITCODE -ne 0) { Fail 'The E2E test project build failed.' }
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path $Exe)) { Fail "App under test not found: $Exe" }

# ---- Run -----------------------------------------------------------------------------------
$settings = @{
    OGMA_E2E_EXE            = $Exe
    OGMA_E2E_SIZES          = ($sizeList -join ',')
    OGMA_E2E_CULTURE        = $Culture
    OGMA_E2E_TEXT_SCALE     = [string]$TextScale
    OGMA_E2E_KEYBOARD_ONLY  = $(if ($KeyboardOnly) { '1' } else { '0' })
    OGMA_E2E_UIA_AUDIT      = $(if ($UiaAudit) { '1' } else { '0' })
    OGMA_E2E_CLIPPING_CHECK = $(if ($ClippingCheck) { '1' } else { '0' })
    OGMA_E2E_MOCK_AI        = $(if ($UseMockAiServer) { '1' } else { '0' })
}
$saved = @{}
$testExit = 0
$runDirs = @()
try {
    foreach ($theme in $Themes) {
        $themeRunId = $(if ($Themes.Count -gt 1 -or $theme -ne 'Light') { "$RunId\$theme" } else { $RunId })
        $artifacts = Join-Path $repo "artifacts\e2e\$themeRunId"
        New-Item -ItemType Directory -Force $artifacts | Out-Null
        $runDirs += $artifacts
        $vars = $settings.Clone()
        $vars['OGMA_E2E_THEME'] = $theme
        $vars['OGMA_E2E_RUN_ID'] = ($themeRunId -replace '\\', '-')
        $vars['OGMA_E2E_ARTIFACTS'] = $artifacts
        foreach ($key in $vars.Keys) {
            if (-not $saved.ContainsKey($key)) { $saved[$key] = [Environment]::GetEnvironmentVariable($key) }
            [Environment]::SetEnvironmentVariable($key, $vars[$key])
        }

        Write-Output "Running journeys: filter '$filter', sizes $($settings.OGMA_E2E_SIZES), theme $theme"
        & dotnet test $project --configuration Release --no-build --filter $filter `
            --logger "trx;LogFileName=e2e.trx" --results-directory $artifacts
        if ($LASTEXITCODE -ne 0) { $testExit = $LASTEXITCODE }
    }
}
finally {
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
}

# ---- Summarise -----------------------------------------------------------------------------
$baseline = (Get-Content (Join-Path $PSScriptRoot 'baseline.json') -Raw -Encoding UTF8 | ConvertFrom-Json).entries
$rows = @()
foreach ($dir in $runDirs) {
    $resultsFile = Join-Path $dir 'results.jsonl'
    if (-not (Test-Path $resultsFile)) { continue }
    foreach ($line in (Get-Content $resultsFile -Encoding UTF8)) {
        if (-not $line.Trim()) { continue }
        $r = $line | ConvertFrom-Json
        $known = @($baseline | Where-Object { $_.journey -eq $r.journey -and (-not $_.test -or $_.test -eq $r.test) }) | Select-Object -First 1
        $label = $r.status
        $defects = ''
        if ($known) { $defects = ($known.defects -join ', ') + ' (' + $known.owner + ')' }
        if ($r.status -eq 'FAIL' -and $known) { $label = 'BASELINE-FAIL' }
        if ($r.status -eq 'PASS' -and $known) { $label = 'PASS (baseline cleared)' }
        $rows += [pscustomobject]@{
            Theme    = (Split-Path -Leaf $dir)
            Journey  = $r.journey
            Test     = $r.test
            Size     = $r.size
            Result   = $label
            Defects  = $defects
            Seconds  = [math]::Round($r.elapsedMs / 1000.0, 1)
            Evidence = $r.evidence
            Message  = $r.message
        }
    }
}

$rows | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $runRoot 'results.json') -Encoding UTF8
$md = @('# Golden journey results', '', "Run ``$RunId``; filter ``$filter``; sizes $($settings.OGMA_E2E_SIZES); themes $($Themes -join ', '); exe ``$Exe``.", '',
    '| Journey | Test | Size | Result | Known defects | s | Evidence |', '|---|---|---|---|---|---|---|')
foreach ($row in ($rows | Sort-Object Journey, Test, Size)) {
    $md += "| $($row.Journey) | $($row.Test) | $($row.Size) | $($row.Result) | $($row.Defects) | $($row.Seconds) | $($row.Evidence) |"
}
$failures = @($rows | Where-Object { $_.Result -eq 'FAIL' })
if ($failures.Count -gt 0) {
    $md += ''
    $md += '## New failures'
    foreach ($f in $failures) { $md += "- **$($f.Journey) $($f.Size)** ($($f.Test)): $($f.Message)" }
}
if ($rows.Count -eq 0) { $md += ''; $md += 'No journey produced a result (the test host failed or the filter matched nothing).' }
$md | Set-Content (Join-Path $runRoot 'results.md') -Encoding UTF8
$rows | Sort-Object Journey, Test, Size | Format-Table Journey, Size, Result, Defects, Seconds -AutoSize | Out-String -Width 200 | Write-Output
Write-Output "Summary: $(Join-Path $runRoot 'results.md')"

if ($rows.Count -eq 0) { exit 1 }
if ($failures.Count -gt 0) { exit 1 }
if ($Strict -and @($rows | Where-Object { $_.Result -ne 'PASS' }).Count -gt 0) { exit 1 }
exit 0
