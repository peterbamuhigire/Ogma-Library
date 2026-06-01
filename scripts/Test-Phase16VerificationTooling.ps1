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

function Invoke-Script {
    param(
        [string]$Script,
        [string[]]$Arguments
    )

    $powerShellExe = (Get-Process -Id $PID).Path
    & $powerShellExe -NoProfile -ExecutionPolicy Bypass -File $Script @Arguments | Out-Host
    return $LASTEXITCODE
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $env:TEMP "phase16-verification-tooling-$timestamp"
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$verificationPath = Join-Path $OutputRoot 'phase16-lan-verification-smoke.md'
$verificationExit = Invoke-Script -Script (Join-Path $repoRoot 'scripts/Invoke-Phase16LanVerification.ps1') -Arguments @(
    '-SkipVerification',
    '-AllowDirtyWorktree',
    '-OutputPath',
    $verificationPath,
    '-SameSubnetPeerAddress',
    '192.168.10.20 / smoke-peer',
    '-MdnsObservation',
    'Smoke pending',
    '-HttpsHealthObservation',
    'Smoke pending',
    '-KeychainObservation',
    'Smoke pending',
    '-Notes',
    'Smoke test only')

Assert-True ($verificationExit -eq 0) "Phase 16 verification smoke exited $verificationExit."
Assert-True (Test-Path -LiteralPath $verificationPath) 'Verification smoke did not create an evidence file.'

$text = Get-Content -Raw -Path $verificationPath
Assert-True ($text -like '*# Phase 16 LAN Verification Evidence*') 'Evidence title missing.'
Assert-True ($text -like '*| Verification skipped | True |*') 'Skipped-verification flag missing.'
Assert-True ($text -like '*## Network Interfaces*') 'Network interface section missing.'
Assert-True ($text -like '*## Same-Subnet Verification*') 'Same-subnet section missing.'
Assert-True ($text -like '*192.168.10.20 / smoke-peer*') 'Peer evidence value missing.'
Assert-True ($text -like '*Smoke test only*') 'Notes value missing.'

$reportPath = Join-Path $OutputRoot 'README.md'
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Phase 16 Verification Tooling Smoke')
$lines.Add('')
$lines.Add('| Field | Value |')
$lines.Add('| --- | --- |')
$lines.Add("| Generated UTC | $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) |")
$lines.Add("| Output root | $OutputRoot |")
$lines.Add("| Evidence | $verificationPath |")
$lines.Add('| Result | Pass |')
Set-Content -Path $reportPath -Value $lines -Encoding UTF8

Write-Host "Phase 16 verification tooling smoke passed."
Write-Host "Report: $reportPath"
