param(
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Add-Result {
    param(
        [System.Collections.Generic.List[object]]$Results,
        [string]$Gate,
        [string]$Status,
        [string]$Evidence,
        [string]$NextAction
    )

    $Results.Add([PSCustomObject]@{
        Gate = $Gate
        Status = $Status
        Evidence = $Evidence
        NextAction = $NextAction
    })
}

function Escape-TableCell {
    param([string]$Value)

    if ($null -eq $Value) {
        return ''
    }

    return ($Value -replace '\|', '\|').Replace("`r", ' ').Replace("`n", ' ')
}

function Get-PendingTableRows {
    param(
        [string]$Path,
        [string]$Document,
        [string]$StopAtHeading = ''
    )

    $rows = [System.Collections.Generic.List[object]]::new()
    foreach ($line in Get-Content -Path $Path) {
        if (-not [string]::IsNullOrWhiteSpace($StopAtHeading) -and $line.Trim() -eq $StopAtHeading) {
            break
        }

        $trimmed = $line.Trim()
        if ($trimmed.StartsWith('|') -and $trimmed -match '\bPending\b') {
            $rows.Add([PSCustomObject]@{
                Document = $Document
                Row = $trimmed
            })
        }
    }

    return @($rows)
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$manualPacketPath = Join-Path $repoRoot 'docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md'
$a11yPath = Join-Path $repoRoot 'docs/qa/PHASE-09-A11Y-SIGNOFF.md'
$phaseEvidencePath = Join-Path $repoRoot 'docs/plans/grand-plan/phase-09/evidence.md'
$workflowPath = Join-Path $repoRoot '.github/workflows/ci.yml'
$preflightDir = Join-Path $repoRoot 'docs/qa/evidence'
$currentCommit = (& git rev-parse HEAD).Trim()

$results = [System.Collections.Generic.List[object]]::new()
$pendingDetails = [System.Collections.Generic.List[object]]::new()
$requiredFiles = @($manualPacketPath, $a11yPath, $phaseEvidencePath, $workflowPath)
foreach ($path in $requiredFiles) {
    if (Test-Path $path) {
        Add-Result $results 'Required file exists' 'Pass' $path 'None'
    }
    else {
        Add-Result $results 'Required file exists' 'Fail' $path 'Restore or create the missing file.'
    }
}

$preflight = Get-ChildItem -Path $preflightDir -Filter 'phase09-preflight-*.md' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $preflight) {
    Add-Result $results 'Automated preflight evidence' 'Fail' $preflightDir 'Run scripts/Phase09-Preflight.ps1 and commit the generated evidence file.'
}
else {
    $preflightText = Get-Content -Raw -Path $preflight.FullName
    $preflightChecks = @(
        '| Verification skipped | False |',
        '| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | 0 |',
        '| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | 0 |',
        '| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | 0 |',
        'Passed:   236',
        'Passed:    93',
        'Passed:    15',
        '0 Warning(s)',
        '0 Error(s)'
    )
    $missing = @($preflightChecks | Where-Object {
            $preflightText.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0
        })
    if ($missing.Count -eq 0) {
        Add-Result $results 'Automated preflight evidence' 'Pass' $preflight.FullName 'None'
    }
    else {
        Add-Result $results 'Automated preflight evidence' 'Fail' $preflight.FullName "Regenerate preflight evidence; missing markers: $($missing -join '; ')"
    }
}

$manualText = Get-Content -Raw -Path $manualPacketPath
$a11yText = Get-Content -Raw -Path $a11yPath
$phaseEvidenceText = Get-Content -Raw -Path $phaseEvidencePath

$manualPendingRows = @(Get-PendingTableRows -Path $manualPacketPath -Document 'Manual signoff packet' -StopAtHeading '## Automated Evidence Snapshot')
foreach ($row in $manualPendingRows) {
    $pendingDetails.Add($row)
}
if ($manualPendingRows.Count -eq 0) {
    Add-Result $results 'Manual signoff packet pending rows' 'Pass' $manualPacketPath 'None'
}
else {
    Add-Result $results 'Manual signoff packet pending rows' 'Pending' $manualPacketPath "Complete or explicitly waive $($manualPendingRows.Count) pending table row(s); see Pending Detail Rows."
}

$a11yPendingRows = @(Get-PendingTableRows -Path $a11yPath -Document 'Accessibility signoff')
foreach ($row in $a11yPendingRows) {
    $pendingDetails.Add($row)
}
if ($a11yPendingRows.Count -eq 0) {
    Add-Result $results 'Accessibility signoff pending rows' 'Pass' $a11yPath 'None'
}
else {
    Add-Result $results 'Accessibility signoff pending rows' 'Pending' $a11yPath "Complete or explicitly waive $($a11yPendingRows.Count) pending table row(s); see Pending Detail Rows."
}

$remoteEvidence = Get-ChildItem -Path $preflightDir -Filter 'phase09-remote-ci-*.md' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $remoteEvidence) {
    Add-Result $results 'Remote CI evidence' 'Pending' $preflightDir 'Run scripts/Get-Phase09RemoteCiEvidence.ps1 with GitHub Actions read access, or attach a dated Actions result manually.'
}
else {
    $remoteEvidenceText = Get-Content -Raw -Path $remoteEvidence.FullName
    if ($remoteEvidenceText -like '*| Status | Pass |*' -and
        $remoteEvidenceText -like '*| Conclusion | All completed workflow runs passed |*' -and
        $remoteEvidenceText -like "*| Commit | $currentCommit |*") {
        Add-Result $results 'Remote CI evidence' 'Pass' $remoteEvidence.FullName 'None'
    }
    else {
        Add-Result $results 'Remote CI evidence' 'Pending' $remoteEvidence.FullName 'Attach a passing remote CI evidence file for the current commit.'
    }
}

$workflowText = Get-Content -Raw -Path $workflowPath
if ($workflowText -like '*windows-latest*' -and $workflowText -like '*macos-latest*' -and $workflowText -like '*dotnet test OgmaLibrary.sln*') {
    Add-Result $results 'CI workflow shape' 'Pass' $workflowPath 'None'
}
else {
    Add-Result $results 'CI workflow shape' 'Fail' $workflowPath 'Restore Windows/macOS matrix with dotnet test OgmaLibrary.sln.'
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    New-Item -ItemType Directory -Force -Path $preflightDir | Out-Null
    $OutputPath = Join-Path $preflightDir "phase09-signoff-gate-$timestamp.md"
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Phase 09 Signoff Gate')
$lines.Add('')
$lines.Add("| Field | Value |")
$lines.Add("| --- | --- |")
$lines.Add("| Generated UTC | $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) |")
$lines.Add("| Commit | $currentCommit |")
$lines.Add("| Branch | $((& git branch --show-current).Trim()) |")
$lines.Add('')
$lines.Add('| Gate | Status | Evidence | Next action |')
$lines.Add('| --- | --- | --- | --- |')
foreach ($result in $results) {
    $lines.Add("| $(Escape-TableCell $result.Gate) | $($result.Status) | $(Escape-TableCell $result.Evidence) | $(Escape-TableCell $result.NextAction) |")
}

$failCount = @($results | Where-Object { $_.Status -eq 'Fail' }).Count
$pendingCount = @($results | Where-Object { $_.Status -eq 'Pending' }).Count
$lines.Add('')
$lines.Add("Summary: $failCount failing gate(s), $pendingCount pending gate(s).")
$lines.Add('')
if ($failCount -eq 0 -and $pendingCount -eq 0) {
    $lines.Add('Phase 09 signoff gate passed.')
}
else {
    $lines.Add('Phase 09 signoff gate is not complete.')
}

if ($pendingDetails.Count -gt 0) {
    $lines.Add('')
    $lines.Add('## Pending Detail Rows')
    $lines.Add('')
    $lines.Add('| Document | Row |')
    $lines.Add('| --- | --- |')
    foreach ($detail in $pendingDetails) {
        $lines.Add("| $(Escape-TableCell $detail.Document) | $(Escape-TableCell $detail.Row) |")
    }
}

Set-Content -Path $OutputPath -Value $lines -Encoding UTF8

foreach ($result in $results) {
    Write-Host "[$($result.Status)] $($result.Gate): $($result.NextAction)"
}
Write-Host "Phase 09 signoff gate report written to $OutputPath"

if ($failCount -gt 0) {
    exit 2
}
if ($pendingCount -gt 0) {
    exit 1
}
