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

function Get-TableValue {
    param(
        [string]$Markdown,
        [string]$Field
    )

    $pattern = "(?m)^\|\s*$([regex]::Escape($Field))\s*\|\s*(?<Value>.*?)\s*\|\s*$"
    $match = [regex]::Match($Markdown, $pattern)
    if (-not $match.Success) {
        return ''
    }

    return $match.Groups['Value'].Value.Trim()
}

function Test-VerificationImpactingPath {
    param([string]$Path)

    return (
        $Path -like 'src/*' -or
        $Path -like 'tests/*' -or
        $Path -like '.github/*' -or
        $Path -like '*.sln' -or
        $Path -like '*.slnx' -or
        $Path -like '*.csproj' -or
        $Path -like '*.props' -or
        $Path -like '*.targets' -or
        $Path -like '*.editorconfig' -or
        $Path -eq 'global.json' -or
        $Path -eq 'NuGet.config' -or
        $Path -like 'Directory.Build.*' -or
        $Path -like 'Directory.Packages.*'
    )
}

function Invoke-GitQuiet {
    param([string[]]$Arguments)

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & git @Arguments *> $null
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Test-EvidenceCommitCoverage {
    param(
        [string]$EvidenceName,
        [string]$EvidenceCommit,
        [string]$CurrentCommit,
        [string]$CurrentReason,
        [string]$CoveredReason
    )

    if ([string]::IsNullOrWhiteSpace($EvidenceCommit)) {
        return [PSCustomObject]@{
            IsCovered = $false
            Reason = "$EvidenceName evidence does not record a commit."
        }
    }

    if ($EvidenceCommit -eq $CurrentCommit) {
        return [PSCustomObject]@{
            IsCovered = $true
            Reason = $CurrentReason
        }
    }

    $ancestorExitCode = Invoke-GitQuiet -Arguments @('merge-base', '--is-ancestor', $EvidenceCommit, $CurrentCommit)
    if ($ancestorExitCode -ne 0) {
        return [PSCustomObject]@{
            IsCovered = $false
            Reason = "$EvidenceName commit $EvidenceCommit is not an ancestor of current commit $CurrentCommit."
        }
    }

    $changedFiles = @((& git diff --name-only $EvidenceCommit $CurrentCommit) | ForEach-Object { $_.ToString() })
    $requiresFreshEvidence = @($changedFiles | Where-Object { Test-VerificationImpactingPath $_ })

    if ($requiresFreshEvidence.Count -gt 0) {
        return [PSCustomObject]@{
            IsCovered = $false
            Reason = "Fresh $($EvidenceName.ToLowerInvariant()) evidence required because verification-impacting files changed afterward: $($requiresFreshEvidence -join ', ')"
        }
    }

    return [PSCustomObject]@{
        IsCovered = $true
        Reason = $CoveredReason
    }
}

function Test-CommittedCleanEvidenceFile {
    param(
        [string]$Path,
        [string]$RepoRoot
    )

    $resolvedPath = (Resolve-Path -Path $Path).Path
    $rootPath = (Resolve-Path -Path $RepoRoot).Path.TrimEnd('\', '/')
    if (-not $resolvedPath.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        return [PSCustomObject]@{
            IsCommittedClean = $false
            Reason = "Evidence file is outside the repository root: $resolvedPath"
        }
    }

    $relativePath = $resolvedPath.Substring($rootPath.Length).TrimStart('\', '/').Replace('\', '/')

    $trackedExitCode = Invoke-GitQuiet -Arguments @('ls-files', '--error-unmatch', '--', $relativePath)
    if ($trackedExitCode -ne 0) {
        return [PSCustomObject]@{
            IsCommittedClean = $false
            Reason = "Evidence file is not tracked in git: $relativePath"
        }
    }

    $worktreeDiffExitCode = Invoke-GitQuiet -Arguments @('diff', '--quiet', '--', $relativePath)
    if ($worktreeDiffExitCode -ne 0) {
        return [PSCustomObject]@{
            IsCommittedClean = $false
            Reason = "Evidence file has uncommitted working-tree changes: $relativePath"
        }
    }

    $cachedDiffExitCode = Invoke-GitQuiet -Arguments @('diff', '--cached', '--quiet', '--', $relativePath)
    if ($cachedDiffExitCode -ne 0) {
        return [PSCustomObject]@{
            IsCommittedClean = $false
            Reason = "Evidence file has staged changes not present in HEAD: $relativePath"
        }
    }

    return [PSCustomObject]@{
        IsCommittedClean = $true
        Reason = "Evidence file is tracked and clean in HEAD: $relativePath"
    }
}

function Test-PreflightEvidenceFile {
    param(
        [System.IO.FileInfo]$EvidenceFile,
        [string]$RepoRoot,
        [string]$CurrentCommit
    )

    $text = Get-Content -Raw -Path $EvidenceFile.FullName
    $requiredMarkers = @(
        '| Verification skipped | False |',
        '| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | 0 |',
        '| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | 0 |',
        '| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | 0 |',
        '0 Warning(s)',
        '0 Error(s)'
    )
    $missing = @($requiredMarkers | Where-Object {
            $text.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0
        })
    if (-not [regex]::IsMatch($text, '(?m)^Passed!\s+-\s+Failed:\s+0,')) {
        $missing += 'at least one successful dotnet test summary with Failed: 0'
    }

    $commit = Get-TableValue -Markdown $text -Field 'Commit'
    $coverage = Test-EvidenceCommitCoverage `
        -EvidenceName 'Preflight' `
        -EvidenceCommit $commit `
        -CurrentCommit $CurrentCommit `
        -CurrentReason 'Preflight evidence was generated for the current commit.' `
        -CoveredReason "Preflight commit $commit is an ancestor and no verification-impacting files changed afterward."
    $fileState = Test-CommittedCleanEvidenceFile -Path $EvidenceFile.FullName -RepoRoot $RepoRoot

    $reasons = @()
    if ($missing.Count -gt 0) {
        $reasons += "missing markers: $($missing -join '; ')"
    }
    if (-not $coverage.IsCovered) {
        $reasons += $coverage.Reason
    }
    if (-not $fileState.IsCommittedClean) {
        $reasons += $fileState.Reason
    }

    if ($reasons.Count -eq 0) {
        return [PSCustomObject]@{
            Status = 'Pass'
            Evidence = $EvidenceFile.FullName
            IsCommittedClean = $fileState.IsCommittedClean
            Message = "$($coverage.Reason) $($fileState.Reason)"
        }
    }

    return [PSCustomObject]@{
        Status = 'Fail'
        Evidence = $EvidenceFile.FullName
        IsCommittedClean = $fileState.IsCommittedClean
        Message = "Regenerate preflight evidence; $($reasons -join '; ')"
    }
}

function Test-RemoteCiEvidenceFile {
    param(
        [System.IO.FileInfo]$EvidenceFile,
        [string]$RepoRoot,
        [string]$CurrentCommit
    )

    $text = Get-Content -Raw -Path $EvidenceFile.FullName
    $commit = Get-TableValue -Markdown $text -Field 'Commit'
    $coverage = Test-EvidenceCommitCoverage `
        -EvidenceName 'Remote CI' `
        -EvidenceCommit $commit `
        -CurrentCommit $CurrentCommit `
        -CurrentReason 'Remote CI evidence was collected for the current commit.' `
        -CoveredReason "Remote CI commit $commit is an ancestor and no verification-impacting files changed afterward."
    $fileState = Test-CommittedCleanEvidenceFile -Path $EvidenceFile.FullName -RepoRoot $RepoRoot

    $remoteStatus = Get-TableValue -Markdown $text -Field 'Status'
    $remoteConclusion = Get-TableValue -Markdown $text -Field 'Conclusion'
    $reasons = @("latest remote CI status is $remoteStatus / $remoteConclusion")
    if (-not $coverage.IsCovered) {
        $reasons += $coverage.Reason
    }
    if (-not $fileState.IsCommittedClean) {
        $reasons += $fileState.Reason
    }

    if ($text -like '*| Status | Pass |*' -and
        $text -like '*| Conclusion | All completed workflow runs passed |*' -and
        $coverage.IsCovered -and
        $fileState.IsCommittedClean) {
        return [PSCustomObject]@{
            Status = 'Pass'
            Evidence = $EvidenceFile.FullName
            Message = "$($coverage.Reason) $($fileState.Reason)"
        }
    }

    return [PSCustomObject]@{
        Status = 'Pending'
        Evidence = $EvidenceFile.FullName
        Message = "Attach a passing remote CI evidence file for the current commit or an ancestor with no verification-impacting changes afterward; $($reasons -join '; ')"
    }
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

$preflightCandidates = @(Get-ChildItem -Path $preflightDir -Filter 'phase09-preflight-*.md' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending)

if ($preflightCandidates.Count -eq 0) {
    Add-Result $results 'Automated preflight evidence' 'Fail' $preflightDir 'Run scripts/Phase09-Preflight.ps1 and commit the generated evidence file.'
}
else {
    $preflightEvaluations = @($preflightCandidates | ForEach-Object {
            Test-PreflightEvidenceFile -EvidenceFile $_ -RepoRoot $repoRoot -CurrentCommit $currentCommit
        })
    $selectedPreflight = @($preflightEvaluations | Where-Object { $_.Status -eq 'Pass' } | Select-Object -First 1)
    if ($selectedPreflight.Count -eq 0) {
        $selectedPreflight = @($preflightEvaluations | Where-Object { $_.IsCommittedClean } | Select-Object -First 1)
    }
    if ($selectedPreflight.Count -eq 0) {
        $selectedPreflight = @($preflightEvaluations | Select-Object -First 1)
    }
    if ($selectedPreflight[0].Status -eq 'Pass') {
        Add-Result $results 'Automated preflight evidence' 'Pass' $selectedPreflight[0].Evidence $selectedPreflight[0].Message
    }
    else {
        Add-Result $results 'Automated preflight evidence' 'Fail' $selectedPreflight[0].Evidence $selectedPreflight[0].Message
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

$remoteCandidates = @(Get-ChildItem -Path $preflightDir -Filter 'phase09-remote-ci-*.md' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending)

if ($remoteCandidates.Count -eq 0) {
    Add-Result $results 'Remote CI evidence' 'Pending' $preflightDir 'Run scripts/Get-Phase09RemoteCiEvidence.ps1 with GitHub Actions read access, or attach a dated Actions result manually.'
}
else {
    $remoteEvaluations = @($remoteCandidates | ForEach-Object {
            Test-RemoteCiEvidenceFile -EvidenceFile $_ -RepoRoot $repoRoot -CurrentCommit $currentCommit
        })
    $selectedRemote = @($remoteEvaluations | Where-Object { $_.Status -eq 'Pass' } | Select-Object -First 1)
    if ($selectedRemote.Count -eq 0) {
        $selectedRemote = @($remoteEvaluations | Select-Object -First 1)
    }
    if ($selectedRemote[0].Status -eq 'Pass') {
        Add-Result $results 'Remote CI evidence' 'Pass' $selectedRemote[0].Evidence $selectedRemote[0].Message
    }
    else {
        Add-Result $results 'Remote CI evidence' 'Pending' $selectedRemote[0].Evidence $selectedRemote[0].Message
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
