#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the fast test suite: every test except Category=Benchmark and Category=E2E.
.DESCRIPTION
    Benchmark tests exercise 10k/50k-scale budgets and run nightly through
    scripts/Test-Performance.ps1. The fast suite is the per-commit gate.
    Category=E2E tests drive the real app window and need a Release E2E build and
    a desktop session; run them with tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1.
    Assumes a Release build already exists (use -Build to build first).
#>
[CmdletBinding()]
param(
    [switch]$Build,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if ($Build) {
        dotnet build OgmaLibrary.sln --configuration $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    dotnet test OgmaLibrary.sln --configuration $Configuration --no-build -m:1 --filter 'Category!=Benchmark&Category!=E2E'
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
