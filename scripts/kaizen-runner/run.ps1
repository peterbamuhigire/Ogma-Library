# Launcher for the Ogma Kaizen runner. Windows PowerShell 5.1 compatible.
#   ./run.ps1                  start the run detached (survives closing this window)
#   ./run.ps1 -Action Status   phase table, token summary and last events
#   ./run.ps1 -Action Watch    follow the driver log
#   ./run.ps1 -Action Stop     finish the current session, then stop
#   ./run.ps1 -Action Report   rebuild the summaries and mirror them to the paper folder
#   ./run.ps1 -Action SelfTest prove the driver with a fake agent (no model calls)
param([ValidateSet('Start', 'Status', 'Watch', 'Stop', 'Report', 'SelfTest')][string]$Action = 'Start')
$ErrorActionPreference = 'Stop'
$driver = Join-Path $PSScriptRoot 'kaizen_runner.py'
$config = Get-Content (Join-Path $PSScriptRoot 'config.json') -Raw | ConvertFrom-Json
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$root = [IO.Path]::GetFullPath((Join-Path $repo $config.work_root))
$log = Join-Path $root 'driver.log'
switch ($Action) {
    'Start' {
        New-Item -ItemType Directory -Force $root | Out-Null
        $cmd = "python -u `"$driver`" run >> `"$log`" 2>&1"
        Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', $cmd -WindowStyle Hidden -WorkingDirectory $repo
        Start-Sleep -Seconds 4
        Write-Output "Started. Log: $log"
        Get-Content $log -Tail 5
    }
    'Status' { python $driver status }
    'Watch' { Get-Content $log -Tail 30 -Wait }
    'Stop' {
        New-Item -ItemType File -Force (Join-Path $root 'STOP') | Out-Null
        Write-Output 'Stop requested: the driver stops after the current session or gate.'
    }
    'Report' { python $driver report }
    'SelfTest' { python $driver selftest }
}
