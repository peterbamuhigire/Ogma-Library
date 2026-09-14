param([int]$AuditProcessId, [string]$CaptureName = 'native-window', [string]$InvokeName, [int]$Width, [int]$Height)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class OgmaAuditWindow {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hgt,uint flags);
}
'@
$process = Get-Process -Id $AuditProcessId
$window = [Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
if ($InvokeName) {
 $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$InvokeName)
 $target = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,$condition)
 if (-not $target) { throw "Control not found: $InvokeName" }
 $target.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 700
}
if ($Width -and $Height) {
 [void][OgmaAuditWindow]::SetWindowPos($process.MainWindowHandle,[IntPtr]::Zero,0,0,$Width,$Height,6)
 Start-Sleep -Milliseconds 500
}
$all = $window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
$records = foreach($element in $all) {
 $c=$element.Current
 [pscustomobject]@{Name=$c.Name;Type=$c.ControlType.ProgrammaticName;Enabled=$c.IsEnabled;Offscreen=$c.IsOffscreen;Bounds=$c.BoundingRectangle.ToString()}
}
$records | ConvertTo-Json -Depth 3 | Out-File (Join-Path $PSScriptRoot "$CaptureName-uia.json") -Encoding utf8
$rect = [OgmaAuditWindow+Rect]::new()
[void][OgmaAuditWindow]::GetWindowRect($process.MainWindowHandle,[ref]$rect)
$bitmap = [Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$dc=$graphics.GetHdc()
try { $captured=[OgmaAuditWindow]::PrintWindow($process.MainWindowHandle,$dc,2) }
finally {$graphics.ReleaseHdc($dc)}
$bitmap.Save((Join-Path $PSScriptRoot "$CaptureName.png"))
$graphics.Dispose();$bitmap.Dispose()
[pscustomobject]@{Process=$AuditProcessId;Captured=$captured;Controls=$records.Count;Image="$CaptureName.png"}
$records | Where-Object {$_.Type -match 'Button|Edit|ComboBox'} | Format-Table -AutoSize
