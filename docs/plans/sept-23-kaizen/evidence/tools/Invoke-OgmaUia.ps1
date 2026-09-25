# Kaizen 2026-09-25 prototype: drives the real Ogma window through Windows UI Automation (no focus needed).
# Actions: launch|size|dump|shot|invoke|select|expand|set|patterns|pick|close|kill. Output goes to $env:OGMA_UIA_OUT (default %TEMP%\ogma-uia).
# Prototype only — superseded by tests/OgmaLibrary.Tests.E2E (Phase 01). Kills only processes started from -Exe.
param(
  [Parameter(Mandatory=$true)][string]$Action,
  [string]$Name, [string]$Value, [string]$Out, [string]$Type, [int]$Index = 0,
  [string]$DataDir, [string]$LibRoot, [switch]$EnableAll, [int]$Width, [int]$Height,
  [string]$Exe = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')) 'src\OgmaLibrary.App\bin\Release\net10.0\OgmaLibrary.App.exe')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing
if (-not ('OgmaWin2' -as [type])) {
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class OgmaWin2 {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hgt,uint flags);
 [DllImport("user32.dll")] public static extern IntPtr PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
 [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
'@ }
$S = if ($env:OGMA_UIA_OUT) { $env:OGMA_UIA_OUT } else { Join-Path $env:TEMP 'ogma-uia' }
$AE = [Windows.Automation.AutomationElement]
$TS = [Windows.Automation.TreeScope]

# Only ever target processes started from the exe under test (never another lane's or the owner's app).
$ExeDir = Split-Path -Parent ([IO.Path]::GetFullPath($Exe))
function Get-OwnProcs { Get-Process OgmaLibrary.App,OgmaLibrary.Workers -ErrorAction SilentlyContinue | Where-Object { $_.Path -and ((Split-Path -Parent $_.Path) -eq $ExeDir) } }
function Get-Proc { Get-OwnProcs | Where-Object { $_.ProcessName -eq 'OgmaLibrary.App' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1 }
function Get-Win { $p = Get-Proc; if (-not $p) { throw 'App window not found' }; $AE::FromHandle($p.MainWindowHandle) }
function Find-All($root) { $root.FindAll($TS::Descendants, [Windows.Automation.Condition]::TrueCondition) }
function Find-ByName([string]$n, [string]$t) {
  $w = Get-Win
  $hits = @(Find-All $w | Where-Object { ($_.Current.Name -eq $n -or $_.Current.AutomationId -eq $n) -and (-not $t -or $_.Current.ControlType.ProgrammaticName -match $t) })
  if ($hits.Count -le $Index) {
    # popups/menus are separate top-level windows in the same process
    $p = Get-Proc
    $tops = $AE::RootElement.FindAll($TS::Children, [Windows.Automation.PropertyCondition]::new($AE::ProcessIdProperty, $p.Id))
    foreach ($tw in $tops) { $hits += @(Find-All $tw | Where-Object { ($_.Current.Name -eq $n -or $_.Current.AutomationId -eq $n) -and (-not $t -or $_.Current.ControlType.ProgrammaticName -match $t) }) }
  }
  if ($hits.Count -le $Index) { throw "Control not found: '$n' [$t]" }
  $hits[$Index]
}
function Capture([string]$name) {
  $p = Get-Proc
  $r = [OgmaWin2+Rect]::new(); [void][OgmaWin2]::GetWindowRect($p.MainWindowHandle, [ref]$r)
  $bmp = [Drawing.Bitmap]::new($r.Right-$r.Left, $r.Bottom-$r.Top); $g = [Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
  try { [void][OgmaWin2]::PrintWindow($p.MainWindowHandle, $dc, 2) } finally { $g.ReleaseHdc($dc) }
  $path = Join-Path $S "shots\$name.png"; New-Item -ItemType Directory -Force (Join-Path $S 'shots') | Out-Null
  $bmp.Save($path); $g.Dispose(); $bmp.Dispose(); $path
}
function Dump([string]$name) {
  $p = Get-Proc
  $tops = $AE::RootElement.FindAll($TS::Children, [Windows.Automation.PropertyCondition]::new($AE::ProcessIdProperty, $p.Id))
  $rows = foreach ($tw in $tops) { foreach ($e in @($tw) + @(Find-All $tw)) { $c = $e.Current
    [pscustomobject]@{ Win=$tw.Current.Name; Type=$c.ControlType.ProgrammaticName.Replace('ControlType.',''); Name=$c.Name; Id=$c.AutomationId; En=$c.IsEnabled; Off=$c.IsOffscreen; Help=$c.HelpText; B=$c.BoundingRectangle.ToString() } } }
  New-Item -ItemType Directory -Force (Join-Path $S 'shots') | Out-Null
  $rows | ConvertTo-Json -Depth 3 | Out-File (Join-Path $S "shots\$name-uia.json") -Encoding utf8
  $rows | Where-Object { -not $_.Off -and ($_.Name -or $_.Id) } | ForEach-Object { '{0,-12} {1,-45} id={2} en={3}' -f $_.Type, ($_.Name -replace "`r?`n",' ' | ForEach-Object { if ($_.Length -gt 45) { $_.Substring(0,45) } else { $_ } }), $_.Id, $_.En }
}

switch ($Action) {
  'launch' {
    Get-OwnProcs | Stop-Process -Force
    $psi = [Diagnostics.ProcessStartInfo]::new($Exe); $psi.UseShellExecute = $false
    $psi.WorkingDirectory = Split-Path $Exe
    if ($DataDir) { $psi.Environment['OGMA_LIBRARY_DATA_DIR'] = $DataDir }
    if ($LibRoot) { $psi.Environment['OGMA_LIBRARY_ROOT'] = $LibRoot }
    if ($EnableAll) { 'OGMA_ENABLE_METADATA_PROVIDERS','OGMA_ENABLE_3D_SHELF','OGMA_ENABLE_CLASSROOM_HOST' | ForEach-Object { $psi.Environment[$_] = 'true' } }
    $psi.RedirectStandardError = $true; $psi.RedirectStandardOutput = $true
    $sw = [Diagnostics.Stopwatch]::StartNew(); $proc = [Diagnostics.Process]::Start($psi)
    while ($sw.Elapsed.TotalSeconds -lt 60) { $proc.Refresh(); if ($proc.MainWindowHandle -ne 0) { break }; if ($proc.HasExited) { break }; Start-Sleep -Milliseconds 200 }
    "pid=$($proc.Id) window_ms=$($sw.ElapsedMilliseconds) exited=$($proc.HasExited)"
    if ($proc.HasExited) { "exit=$($proc.ExitCode)"; $proc.StandardError.ReadToEnd(); $proc.StandardOutput.ReadToEnd() }
  }
  'size'    { $p = Get-Proc; [void][OgmaWin2]::SetWindowPos($p.MainWindowHandle,[IntPtr]::Zero,0,0,$Width,$Height,6); Start-Sleep -Milliseconds 600; 'ok' }
  'dump'    { Dump $Out }
  'shot'    { Capture $Out }
  'invoke'  { $e = Find-ByName $Name $Type
              try { $e.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
              catch { try { $e.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() } catch { $e.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() } }
              Start-Sleep -Milliseconds 900; "invoked $Name" }
  'select'  { $e = Find-ByName $Name $Type; $e.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 900; "selected $Name" }
  'expand'  { $e = Find-ByName $Name $Type; $e.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 700; "expanded $Name" }
  'set'     { $e = Find-ByName $Name $Type; $e.SetFocus(); $e.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Value); Start-Sleep -Milliseconds 900; "set $Name" }
  'patterns'{ $e = Find-ByName $Name $Type; $e.GetSupportedPatterns() | ForEach-Object { $_.ProgrammaticName } }
  'close'   { $p = Get-Proc; [void][OgmaWin2]::PostMessage($p.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero); Start-Sleep 3; $still = Get-OwnProcs; "remaining=" + (($still | ForEach-Object { $_.ProcessName }) -join ',') }
  'pick'    { # Drive the native folder dialog owned by the app. The Windows folder picker exposes no
              # UIA "Select Folder" button, so: focus the dialog (Alt-key foreground trick), Alt+D, type
              # the path, Enter to navigate, then click "Select Folder" at its standard bottom-right offset.
              Add-Type -AssemblyName System.Windows.Forms
              $w = Get-Win; $dlg = $null
              for ($i=0; $i -lt 30 -and -not $dlg; $i++) { $dlg = $w.FindFirst($TS::Children, [Windows.Automation.PropertyCondition]::new($AE::ClassNameProperty,'#32770')); if (-not $dlg) { Start-Sleep -Milliseconds 500 } }
              if (-not $dlg) { throw 'No dialog' }
              $h = [IntPtr]$dlg.Current.NativeWindowHandle
              [OgmaWin2]::keybd_event(0x12,0,0,[UIntPtr]::Zero); [void][OgmaWin2]::SetForegroundWindow($h); [OgmaWin2]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
              Start-Sleep -Milliseconds 500
              if ([OgmaWin2]::GetForegroundWindow() -ne $h) { throw 'Could not focus the folder dialog' }
              [System.Windows.Forms.SendKeys]::SendWait('%d'); Start-Sleep -Milliseconds 400
              [System.Windows.Forms.SendKeys]::SendWait(($Value -replace '([+^%~(){}\[\]])','{$1}')); [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
              Start-Sleep -Milliseconds 1500
              $r = [OgmaWin2+Rect]::new(); [void][OgmaWin2]::GetWindowRect($h, [ref]$r)
              [void][OgmaWin2]::SetForegroundWindow($h)
              [void][OgmaWin2]::SetCursorPos($r.Right - 179, $r.Bottom - 39)
              [OgmaWin2]::mouse_event(2,0,0,0,[UIntPtr]::Zero); [OgmaWin2]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
              Start-Sleep 2
              $still = $w.FindFirst($TS::Children, [Windows.Automation.PropertyCondition]::new($AE::ClassNameProperty,'#32770'))
              if ($still) { throw 'Folder dialog is still open' }
              'picked' }
  'kill'    { Get-OwnProcs | Stop-Process -Force; 'killed' }
}
