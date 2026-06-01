param(
    [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-PendingTableRows {
    param(
        [string]$Path,
        [string]$StopAtHeading = ''
    )

    $rows = [System.Collections.Generic.List[string]]::new()
    foreach ($line in Get-Content -Path $Path) {
        if (-not [string]::IsNullOrWhiteSpace($StopAtHeading) -and $line.Trim() -eq $StopAtHeading) {
            break
        }

        $trimmed = $line.Trim()
        if ($trimmed.StartsWith('|') -and $trimmed -match '\bPending\b') {
            $rows.Add($trimmed)
        }
    }

    return @($rows)
}

function Invoke-Script {
    param(
        [string]$Script,
        [string[]]$Arguments
    )

    & powershell -NoProfile -ExecutionPolicy Bypass -File $Script @Arguments | Out-Host
    return $LASTEXITCODE
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $env:TEMP "phase09-evidence-tooling-$timestamp"
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$manualPacketPath = Join-Path $repoRoot 'docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md'
$a11yPath = Join-Path $repoRoot 'docs/qa/PHASE-09-A11Y-SIGNOFF.md'
$preflightPath = Join-Path $OutputRoot 'phase09-preflight-smoke.md'
$signoffPath = Join-Path $OutputRoot 'phase09-signoff-smoke.md'
$packagePath = Join-Path $OutputRoot 'manual-package'
$draftPreflightPath = Join-Path $repoRoot 'docs/qa/evidence/phase09-preflight-99999999-smoke-draft.md'

$checks = [System.Collections.Generic.List[object]]::new()
try {
    $preflightExit = Invoke-Script -Script (Join-Path $repoRoot 'scripts/Phase09-Preflight.ps1') -Arguments @(
        '-SkipVerification',
        '-AllowDirtyWorktree',
        '-OutputPath',
        $preflightPath)
    Assert-True ($preflightExit -eq 0) "Preflight smoke exited $preflightExit."
    $preflightText = Get-Content -Raw -Path $preflightPath
    Assert-True ($preflightText -like '*| Verification skipped | True |*') 'Preflight smoke did not record skipped verification.'
    Assert-True ($preflightText -like '*| OS |*') 'Preflight smoke did not record OS details.'
    $checks.Add([PSCustomObject]@{ Check = 'Preflight smoke'; Result = 'Pass'; Evidence = $preflightPath })

    $manualPendingCount = @(Get-PendingTableRows -Path $manualPacketPath -StopAtHeading '## Automated Evidence Snapshot').Count
    $a11yPendingCount = @(Get-PendingTableRows -Path $a11yPath).Count
    $expectedNoteCount = $manualPendingCount + $a11yPendingCount
    $packageExit = Invoke-Script -Script (Join-Path $repoRoot 'scripts/New-Phase09ManualEvidencePackage.ps1') -Arguments @(
        '-OutputRoot',
        $packagePath,
        '-ReviewerInitials',
        'SMOKE')
    Assert-True ($packageExit -eq 0) "Manual evidence package exited $packageExit."
    $noteCount = @(Get-ChildItem -Path (Join-Path $packagePath 'notes') -Filter '*.md').Count
    Assert-True ($noteCount -eq $expectedNoteCount) "Manual package note count $noteCount did not match pending row count $expectedNoteCount."
    Assert-True (Test-Path (Join-Path $packagePath 'README.md')) 'Manual package README was not generated.'
    $checks.Add([PSCustomObject]@{ Check = 'Manual evidence package smoke'; Result = 'Pass'; Evidence = $packagePath })

    $shadowPreflightExit = Invoke-Script -Script (Join-Path $repoRoot 'scripts/Phase09-Preflight.ps1') -Arguments @(
        '-SkipVerification',
        '-AllowDirtyWorktree',
        '-OutputPath',
        $draftPreflightPath)
    Assert-True ($shadowPreflightExit -eq 0) "Draft preflight setup exited $shadowPreflightExit."

    $signoffExit = Invoke-Script -Script (Join-Path $repoRoot 'scripts/Test-Phase09Signoff.ps1') -Arguments @(
        '-OutputPath',
        $signoffPath)
    Assert-True ($signoffExit -eq 1) "Signoff smoke expected pending exit 1, got $signoffExit."
    $signoffText = Get-Content -Raw -Path $signoffPath
    Assert-True ($signoffText -like '*Summary: 0 failing gate(s), 3 pending gate(s).*') 'Signoff smoke summary changed unexpectedly.'
    Assert-True ($signoffText -like '*phase09-preflight-20260601-072040.md*') 'Signoff smoke did not keep using the committed passing preflight evidence.'
    Assert-True ($signoffText -notlike '*phase09-preflight-99999999-smoke-draft.md*') 'Signoff smoke allowed a newer untracked draft preflight to shadow committed evidence.'
    $checks.Add([PSCustomObject]@{ Check = 'Signoff evidence shadow smoke'; Result = 'Pass'; Evidence = $signoffPath })
}
finally {
    if (Test-Path -LiteralPath $draftPreflightPath) {
        Remove-Item -LiteralPath $draftPreflightPath -Force
    }
}

$reportPath = Join-Path $OutputRoot 'README.md'
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Phase 09 Evidence Tooling Smoke')
$lines.Add('')
$lines.Add('| Field | Value |')
$lines.Add('| --- | --- |')
$lines.Add("| Generated UTC | $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) |")
$lines.Add("| Output root | $OutputRoot |")
$lines.Add('')
$lines.Add('| Check | Result | Evidence |')
$lines.Add('| --- | --- | --- |')
foreach ($check in $checks) {
    $lines.Add("| $($check.Check) | $($check.Result) | $($check.Evidence) |")
}
Set-Content -Path $reportPath -Value $lines -Encoding UTF8

Write-Host "Phase 09 evidence tooling smoke passed."
Write-Host "Report: $reportPath"
