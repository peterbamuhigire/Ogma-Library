# Install Ogma Library git hooks (Windows / PowerShell).
# Run once after cloning:  pwsh .github/hooks/install-hooks.ps1
$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel).Trim()
$src = Join-Path $repoRoot '.github/hooks'
$dst = Join-Path $repoRoot '.git/hooks'

New-Item -ItemType Directory -Force -Path $dst | Out-Null
foreach ($hook in @('commit-msg')) {
    Copy-Item -Path (Join-Path $src $hook) -Destination (Join-Path $dst $hook) -Force
    Write-Host "OK installed $hook"
}

Write-Host 'Ogma Library git hooks installed.'
Write-Host 'Note: Git for Windows runs hooks via its bundled sh; no extra setup needed.'
