param(
    [string]$OutputRoot,
    [string]$ReviewerInitials = 'Pending'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
        if (-not $trimmed.StartsWith('|') -or $trimmed -notmatch '\bPending\b') {
            continue
        }

        $cells = @($trimmed.Trim('|').Split('|') | ForEach-Object { $_.Trim() })
        $title = 'pending-row'
        foreach ($cell in $cells) {
            if (-not [string]::IsNullOrWhiteSpace($cell) -and $cell -ne 'Pending') {
                $title = $cell
                break
            }
        }

        $rows.Add([PSCustomObject]@{
            Document = $Document
            Title = $title
            Row = $trimmed
        })
    }

    return @($rows)
}

function ConvertTo-Slug {
    param([string]$Value)

    $slug = $Value.ToLowerInvariant()
    $slug = $slug -replace '`[^`]+`', ''
    $slug = $slug -replace '[^a-z0-9]+', '-'
    $slug = $slug.Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) {
        return 'pending-row'
    }

    if ($slug.Length -gt 72) {
        return $slug.Substring(0, 72).Trim('-')
    }

    return $slug
}

function Get-SuggestedEvidencePath {
    param(
        [string]$Slug,
        [string]$Row
    )

    if ($Row -match 'Audio note') {
        return "audio/$Slug.m4a"
    }

    if ($Row -match 'Screenshot') {
        return "screenshots/$Slug.png"
    }

    if ($Row -match 'export|sidecar|Pasted citation') {
        return "exports/$Slug.txt"
    }

    return "notes/$Slug.md"
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "docs/qa/evidence/phase09-manual-$timestamp"
}

$manualPacketPath = Join-Path $repoRoot 'docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md'
$a11yPath = Join-Path $repoRoot 'docs/qa/PHASE-09-A11Y-SIGNOFF.md'
$commit = (& git rev-parse HEAD).Trim()
$branch = (& git branch --show-current).Trim()

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
foreach ($child in @('screenshots', 'audio', 'exports', 'notes')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $OutputRoot $child) | Out-Null
}

$manualRows = @(Get-PendingTableRows -Path $manualPacketPath -Document 'Manual signoff packet' -StopAtHeading '## Automated Evidence Snapshot')
$a11yRows = @(Get-PendingTableRows -Path $a11yPath -Document 'Accessibility signoff')
$allRows = @($manualRows + $a11yRows)

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Phase 09 Manual Evidence Package')
$lines.Add('')
$lines.Add('| Field | Value |')
$lines.Add('| --- | --- |')
$lines.Add("| Generated UTC | $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) |")
$lines.Add("| Branch | $branch |")
$lines.Add("| Commit | $commit |")
$lines.Add("| Reviewer initials | $ReviewerInitials |")
$lines.Add("| Manual packet | docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md |")
$lines.Add("| Accessibility packet | docs/qa/PHASE-09-A11Y-SIGNOFF.md |")
$lines.Add('')
$lines.Add('## Use')
$lines.Add('')
$lines.Add('Store screenshots, audio notes, exported citation text, and reviewer notes in this folder. After review, copy the durable evidence paths back into the pending rows in the manual signoff packet and accessibility signoff file, then run `scripts/Test-Phase09Signoff.ps1`.')
$lines.Add('')
$lines.Add('## Pending Rows')
$lines.Add('')
$lines.Add('| Document | Item | Source row | Suggested evidence path |')
$lines.Add('| --- | --- | --- | --- |')

$usedSlugs = @{}
foreach ($row in $allRows) {
    $baseSlug = ConvertTo-Slug -Value $row.Title
    $slug = $baseSlug
    $index = 2
    while ($usedSlugs.ContainsKey($slug)) {
        $slug = "$baseSlug-$index"
        $index++
    }
    $usedSlugs[$slug] = $true

    $suggestedPath = Get-SuggestedEvidencePath -Slug $slug -Row $row.Row
    $lines.Add("| $(Escape-TableCell $row.Document) | $(Escape-TableCell $row.Title) | $(Escape-TableCell $row.Row) | $suggestedPath |")

    $notePath = Join-Path (Join-Path $OutputRoot 'notes') "$slug.md"
    $noteLines = @(
        "# $($row.Title)",
        '',
        '| Field | Value |',
        '| --- | --- |',
        "| Source document | $($row.Document) |",
        "| Reviewer initials | $ReviewerInitials |",
        '| Review date | Pending |',
        '| Result | Pending |',
        '| Evidence reference | Pending |',
        '',
        '## Source Row',
        '',
        $row.Row,
        '',
        '## Reviewer Notes',
        '',
        'Pending.'
    )
    Set-Content -Path $notePath -Value $noteLines -Encoding UTF8
}

$readmePath = Join-Path $OutputRoot 'README.md'
Set-Content -Path $readmePath -Value $lines -Encoding UTF8

Write-Host "Phase 09 manual evidence package written to $OutputRoot"
Write-Host "Pending rows included: $($allRows.Count)"
Write-Host "README: $readmePath"
