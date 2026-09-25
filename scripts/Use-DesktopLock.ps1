#Requires -Version 5.1
<#
.SYNOPSIS
    Serialises real-window work (E2E journeys, UIA driving) across parallel lanes on one machine.
.DESCRIPTION
    Real-window runs need the foreground, the folder dialog and a topmost window at the screen's
    top-left, so two concurrent runs corrupt each other. Wrap every real-window run:

        ./scripts/Use-DesktopLock.ps1 -Owner 'phase-07' -ScriptBlock { ./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Journey G1 }

    The lock is a named system mutex, so it is released automatically if the holder dies.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Owner,
    [Parameter(Mandatory = $true)][scriptblock]$ScriptBlock,
    [int]$TimeoutMinutes = 90
)

$ErrorActionPreference = 'Stop'
$mutex = New-Object System.Threading.Mutex($false, 'Global\OgmaLibraryDesktopLock')
$acquired = $false
try {
    try {
        Write-Host "[$Owner] waiting for the desktop lock..."
        $acquired = $mutex.WaitOne([TimeSpan]::FromMinutes($TimeoutMinutes))
    }
    catch [System.Threading.AbandonedMutexException] {
        # The previous holder died; the lock is now ours.
        $acquired = $true
    }

    if (-not $acquired) {
        throw "[$Owner] timed out after $TimeoutMinutes minutes waiting for the desktop lock."
    }

    Write-Host "[$Owner] holds the desktop lock."
    & $ScriptBlock
}
finally {
    if ($acquired) {
        $mutex.ReleaseMutex()
        Write-Host "[$Owner] released the desktop lock."
    }

    $mutex.Dispose()
}
