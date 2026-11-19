#Requires -Version 5.1
<#
.SYNOPSIS
    Checks that Invoke-GoldenJourneys.ps1 writes its summary and keeps its exit code when the
    caller redirects all output (K103).

.DESCRIPTION
    Builds two synthetic runs under artifacts/e2e (one passing, one with a new failure), then
    re-summarises each with -ReportOnly in a child Windows PowerShell process whose output is
    redirected with `2>&1 | Out-File`, exactly as the Kaizen runner calls the script. Asserts that
    results.md is written and the child exit code is 0 for the passing run and 1 for the failing
    run. When the Release E2E test build exists, a third case runs the real redirected `dotnet test`
    call with a filter that matches nothing and expects the summary plus exit 1. The synthetic run
    folders are removed afterwards. No app is launched.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runner = Join-Path $repo 'tests\OgmaLibrary.Tests.E2E\Invoke-GoldenJourneys.ps1'
$shell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$failures = [System.Collections.Generic.List[string]]::new()

function Invoke-Case([string]$Name, [string]$Status, [int]$ExpectedExit, [switch]$LiveNoMatch) {
    $runId = "runner-check-$Name-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    $runRoot = Join-Path $repo "artifacts\e2e\$runId"
    $log = Join-Path $runRoot 'redirected.log'
    New-Item -ItemType Directory -Force $runRoot | Out-Null
    try {
        $row = [ordered]@{
            journey = 'G2'; test = 'RunnerCheck'; size = '1280x800'; status = $Status
            message = $(if ($Status -eq 'FAIL') { 'Synthetic failure.' } else { $null })
            elapsedMs = 1000; evidence = 'n/a'
        }
        $expectedText = 'RunnerCheck'
        $arguments = "-ReportOnly -RunId '$runId'"
        if ($LiveNoMatch) {
            # Runs the real redirected `dotnet test` call with a filter that matches nothing; the
            # summary must still be written and report that no journey produced a result.
            $expectedText = 'No journey produced a result'
            $arguments = "-Journey RunnerCheckNoMatch -Sizes 1280x800 -NoBuild -Exe '$shell' -RunId '$runId'"
        }
        else {
            Set-Content -Path (Join-Path $runRoot 'results.jsonl') -Value ($row | ConvertTo-Json -Compress) -Encoding UTF8
        }

        $command = "& '$runner' $arguments 2>&1 | Out-File -FilePath '$log' -Encoding utf8; exit `$LASTEXITCODE"
        & $shell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $command | Out-Null
        $exit = $LASTEXITCODE

        $resultsMd = Join-Path $runRoot 'results.md'
        if (-not (Test-Path $resultsMd)) {
            $failures.Add("${Name}: results.md was not written under redirected output.")
        }
        elseif (-not (Select-String -Path $resultsMd -Pattern $expectedText -SimpleMatch -Quiet)) {
            $failures.Add("${Name}: results.md does not list the synthetic row.")
        }
        if ($exit -ne $ExpectedExit) {
            $failures.Add("${Name}: exit code $exit, expected $ExpectedExit.")
        }
        Write-Output "${Name}: exit $exit (expected $ExpectedExit), results.md $(if (Test-Path $resultsMd) { 'written' } else { 'MISSING' })"
    }
    finally {
        Remove-Item -Recurse -Force $runRoot -ErrorAction SilentlyContinue
    }
}

Invoke-Case -Name 'pass' -Status 'PASS' -ExpectedExit 0
Invoke-Case -Name 'fail' -Status 'FAIL' -ExpectedExit 1
$testDll = Join-Path $repo 'tests/OgmaLibrary.Tests.E2E/bin/Release/net10.0-windows/OgmaLibrary.Tests.E2E.dll'
if (Test-Path $testDll) {
    Invoke-Case -Name 'live' -Status 'n/a' -ExpectedExit 1 -LiveNoMatch
}
else {
    Write-Output 'live: NOT ASSESSED (build tests/OgmaLibrary.Tests.E2E in Release first).'
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}
Write-Output 'Golden journey runner check: PASS'
exit 0
