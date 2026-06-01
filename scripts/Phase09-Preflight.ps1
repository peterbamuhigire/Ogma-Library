param(
    [string]$OutputPath,
    [switch]$SkipVerification,
    [switch]$AllowDirtyWorktree
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $started = Get-Date
    $output = & $Command @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $finished = Get-Date

    [PSCustomObject]@{
        Command = "$Command $($Arguments -join ' ')"
        ExitCode = $exitCode
        Started = $started
        Finished = $finished
        DurationSeconds = [Math]::Round(($finished - $started).TotalSeconds, 1)
        Output = @($output | ForEach-Object { $_.ToString() })
    }
}

function Add-CodeBlock {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Language,
        [string[]]$Content
    )

    $items = @($Content)
    $Lines.Add("``````$Language")
    if ($items.Count -eq 0) {
        $Lines.Add("(no output)")
    }
    else {
        foreach ($line in $items) {
            $Lines.Add($line)
        }
    }
    $Lines.Add("``````")
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $evidenceDir = Join-Path $repoRoot 'docs/qa/evidence'
    New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
    $OutputPath = Join-Path $evidenceDir "phase09-preflight-$timestamp.md"
}

$gitStatus = @((& git status --short) | ForEach-Object { $_.ToString() })
if (-not $AllowDirtyWorktree -and $gitStatus.Count -gt 0) {
    throw "Working tree is not clean. Commit or stash changes, or pass -AllowDirtyWorktree for a draft preflight record."
}

$commit = (& git rev-parse HEAD).Trim()
$branch = (& git branch --show-current).Trim()
$remote = (& git remote get-url origin).Trim()
$dotnetVersion = (& dotnet --version).Trim()
$os = Get-CimInstance Win32_OperatingSystem
$appProcesses = @(Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -in @('OgmaLibrary.App.exe', 'dotnet.exe') -and
        ($_.CommandLine -like '*OgmaLibrary.App*' -or $_.CommandLine -like '*OgmaLibrary.App.csproj*')
    } |
    Select-Object ProcessId, Name, CommandLine)

$results = @()
if (-not $SkipVerification) {
    $results += Invoke-LoggedCommand -Command 'dotnet' -Arguments @(
        'format',
        'OgmaLibrary.sln',
        '--verify-no-changes',
        '--no-restore')
    $results += Invoke-LoggedCommand -Command 'dotnet' -Arguments @(
        'build',
        'OgmaLibrary.sln',
        '--configuration',
        'Release',
        '--no-restore')
    $results += Invoke-LoggedCommand -Command 'dotnet' -Arguments @(
        'test',
        'OgmaLibrary.sln',
        '--configuration',
        'Release',
        '--no-build')
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Phase 09 Preflight Evidence')
$lines.Add('')
$lines.Add("| Field | Value |")
$lines.Add("| --- | --- |")
$lines.Add("| Generated UTC | $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) |")
$lines.Add("| Repository | $remote |")
$lines.Add("| Branch | $branch |")
$lines.Add("| Commit | $commit |")
$lines.Add("| OS | $($os.Caption) $($os.Version) build $($os.BuildNumber) |")
$lines.Add("| .NET SDK | $dotnetVersion |")
$lines.Add("| Verification skipped | $($SkipVerification.IsPresent) |")
$lines.Add('')
$lines.Add('## Working Tree')
$lines.Add('')
Add-CodeBlock -Lines $lines -Language 'text' -Content $gitStatus
$lines.Add('')
$lines.Add('## Running App Processes')
$lines.Add('')
if ($appProcesses.Count -eq 0) {
    $lines.Add('No Ogma Library app processes were detected.')
}
else {
    $lines.Add('| PID | Name | Command line |')
    $lines.Add('| --- | --- | --- |')
    foreach ($process in $appProcesses) {
        $commandLine = ($process.CommandLine -replace '\|', '\|')
        $lines.Add("| $($process.ProcessId) | $($process.Name) | ``$commandLine`` |")
    }
}
$lines.Add('')
$lines.Add('## Verification Commands')
$lines.Add('')
if ($SkipVerification) {
    $lines.Add('Verification commands were skipped for this draft evidence record.')
}
else {
    $lines.Add('| Command | Exit code | Duration seconds |')
    $lines.Add('| --- | ---: | ---: |')
    foreach ($result in $results) {
        $lines.Add("| ``$($result.Command)`` | $($result.ExitCode) | $($result.DurationSeconds) |")
    }
    foreach ($result in $results) {
        $lines.Add('')
        $lines.Add("### $($result.Command)")
        $lines.Add('')
        Add-CodeBlock -Lines $lines -Language 'text' -Content $result.Output
    }
}
$lines.Add('')
$lines.Add('## Manual Signoff Linkage')
$lines.Add('')
$lines.Add('Attach this file as the preflight evidence reference in `docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md` before completing the manual walkthrough rows.')

Set-Content -Path $OutputPath -Value $lines -Encoding UTF8
Write-Host "Phase 09 preflight evidence written to $OutputPath"

if (-not $SkipVerification) {
    $failed = @($results | Where-Object { $_.ExitCode -ne 0 })
    if ($failed.Count -gt 0) {
        exit 1
    }
}
