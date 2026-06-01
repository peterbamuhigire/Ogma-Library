param(
    [string]$CommitSha,
    [string]$Repo = 'peterbamuhigire/Ogma-Library',
    [string]$OutputPath,
    [string]$GitHubToken = $env:GITHUB_TOKEN
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

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($CommitSha)) {
    $CommitSha = (& git rev-parse HEAD).Trim()
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$evidenceDir = Join-Path $repoRoot 'docs/qa/evidence'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
    $OutputPath = Join-Path $evidenceDir "phase09-remote-ci-$timestamp.md"
}

$headers = @{
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'Ogma-Library-Phase09-CI-Evidence'
}
if (-not [string]::IsNullOrWhiteSpace($GitHubToken)) {
    $headers.Authorization = "Bearer $GitHubToken"
}

$url = "https://api.github.com/repos/$Repo/actions/runs?head_sha=$CommitSha&per_page=20"
$status = 'Pending'
$conclusion = 'Unavailable'
$nextAction = 'Attach a GitHub Actions run result manually, or rerun this script with a token that can read Actions for the repository.'
$runs = @()
$errorText = ''

try {
    $response = Invoke-RestMethod -Method Get -Uri $url -Headers $headers
    $runs = @($response.workflow_runs)

    if ($runs.Count -eq 0) {
        $status = 'Pending'
        $conclusion = 'No runs found'
        $nextAction = 'Confirm the workflow triggered for this commit, then rerun this script.'
    }
    elseif (@($runs | Where-Object { $_.status -ne 'completed' }).Count -gt 0) {
        $status = 'Pending'
        $conclusion = 'Run in progress'
        $nextAction = 'Wait for all workflow runs for this commit to complete, then rerun this script.'
    }
    elseif (@($runs | Where-Object { $_.conclusion -ne 'success' }).Count -gt 0) {
        $status = 'Fail'
        $conclusion = 'At least one run did not pass'
        $nextAction = 'Open the failed GitHub Actions run, fix or rerun it, then regenerate this evidence.'
    }
    else {
        $status = 'Pass'
        $conclusion = 'All completed workflow runs passed'
        $nextAction = 'None'
    }
}
catch {
    $status = 'Pending'
    $conclusion = 'GitHub API unavailable'
    $errorText = $_.Exception.Message
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Phase 09 Remote CI Evidence')
$lines.Add('')
$lines.Add('| Field | Value |')
$lines.Add('| --- | --- |')
$lines.Add("| Generated UTC | $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) |")
$lines.Add("| Repository | $Repo |")
$lines.Add("| Commit | $CommitSha |")
$lines.Add("| Status | $status |")
$lines.Add("| Conclusion | $conclusion |")
$lines.Add("| Next action | $(Escape-TableCell $nextAction) |")
$lines.Add("| API URL | ``$url`` |")
if (-not [string]::IsNullOrWhiteSpace($errorText)) {
    $lines.Add("| API error | $(Escape-TableCell $errorText) |")
}
$lines.Add('')
$lines.Add('## Workflow Runs')
$lines.Add('')
if ($runs.Count -eq 0) {
    $lines.Add('No workflow runs were available from the GitHub API response.')
}
else {
    $lines.Add('| Name | Status | Conclusion | Run URL | Created UTC | Updated UTC |')
    $lines.Add('| --- | --- | --- | --- | --- | --- |')
    foreach ($run in $runs) {
        $lines.Add("| $(Escape-TableCell $run.name) | $(Escape-TableCell $run.status) | $(Escape-TableCell $run.conclusion) | $($run.html_url) | $($run.created_at) | $($run.updated_at) |")
    }
}

Set-Content -Path $OutputPath -Value $lines -Encoding UTF8
Write-Host "Phase 09 remote CI evidence written to $OutputPath"
Write-Host "Status: $status"
Write-Host "Conclusion: $conclusion"

if ($status -eq 'Pass') {
    exit 0
}
if ($status -eq 'Fail') {
    exit 2
}
exit 1
