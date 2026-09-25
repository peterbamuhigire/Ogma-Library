#Requires -Version 5.1
<#
.SYNOPSIS
    Runs only the Category=Benchmark tests (10k/50k-scale performance budgets).
.DESCRIPTION
    Run nightly in CI and locally before any release candidate. Run on an otherwise
    idle machine: competing load distorts the latency budgets.
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
    dotnet test OgmaLibrary.sln --configuration $Configuration --no-build -m:1 --filter 'Category=Benchmark'
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
