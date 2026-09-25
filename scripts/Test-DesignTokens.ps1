#Requires -Version 5.1
<#
.SYNOPSIS
    Static design-token lint (Sept-23 Kaizen Phase 09, task 9.10).
.DESCRIPTION
    Fails when a view under src/OgmaLibrary.App/Views hard-codes presentation values that must
    come from Themes/Tokens.axaml:
      - a literal FontSize (FontSize="14", or a Setter Value for FontSize)
      - a literal colour on Foreground/Background/BorderBrush/Fill/Stroke/Color
        (hex such as #FFFFFF, or a named colour such as White/Black; Transparent is allowed)
      - a SolidColorBrush with a literal Color
    and when an icon file under Assets/icons is not registered in Icons/IconCatalog.cs or the
    icon set is missing its licence file.
    Exit code 0 = clean, 1 = violations (listed as path:line: message).
.EXAMPLE
    ./scripts/Test-DesignTokens.ps1
.EXAMPLE
    ./scripts/Test-DesignTokens.ps1 -Root C:\temp\seeded-copy
#>
[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }
$views = Join-Path $Root 'src\OgmaLibrary.App\Views'
$icons = Join-Path $Root 'src\OgmaLibrary.App\Assets\icons'
$catalog = Join-Path $Root 'src\OgmaLibrary.App\Icons\IconCatalog.cs'

$colourProps = 'Foreground|Background|BorderBrush|Fill|Stroke|Color|CaretBrush|SelectionBrush'
$rules = @(
    @{ Pattern = 'FontSize="\s*[0-9.]+\s*"'; Message = 'literal FontSize; use {DynamicResource Type.Size.*}' },
    @{ Pattern = "(?:$colourProps)=""\s*#[0-9A-Fa-f]{3,8}\s*"""; Message = 'literal hex colour; use a Brush.* token' },
    @{ Pattern = "(?:$colourProps)=""\s*(?!Transparent\b)[A-Z][a-zA-Z]+\s*"""; Message = 'named colour; use a Brush.* token' },
    @{ Pattern = 'Property="FontSize"\s+Value="\s*[0-9.]+'; Message = 'literal FontSize setter; use a Type.Size.* token' },
    @{ Pattern = "Property=""(?:$colourProps)""\s+Value=""\s*(?:#[0-9A-Fa-f]{3,8}|(?!Transparent\b)[A-Z][a-zA-Z]+)\s*"""; Message = 'literal colour setter; use a Brush.* token' }
)

$violations = New-Object System.Collections.Generic.List[string]
foreach ($file in Get-ChildItem -Path $views -Recurse -Filter '*.axaml') {
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadAllLines($file.FullName)) {
        $lineNumber++
        foreach ($rule in $rules) {
            if ($line -match $rule.Pattern) {
                $relative = $file.FullName.Substring($Root.Length).TrimStart('\', '/')
                $violations.Add("${relative}:${lineNumber}: $($rule.Message) -> $($Matches[0])")
            }
        }
    }
}

# Icons: every shipped SVG is registered in IconCatalog and the set carries its licence.
$registered = @{}
foreach ($m in [regex]::Matches([IO.File]::ReadAllText($catalog), '\["ic_[a-z0-9_]+"\]\s*=\s*"([^"]+)"')) {
    $registered[$m.Groups[1].Value.Replace('\', '/')] = $true
}
foreach ($svg in Get-ChildItem -Path $icons -Recurse -Filter '*.svg') {
    $relative = $svg.FullName.Substring($icons.Length).TrimStart('\', '/').Replace('\', '/')
    if (-not $registered.ContainsKey($relative)) {
        $violations.Add("src/OgmaLibrary.App/Assets/icons/${relative}: icon file is not registered in IconCatalog")
    }
}
if (-not (Test-Path (Join-Path $icons 'LICENSE-ICONS.txt'))) {
    $violations.Add('src/OgmaLibrary.App/Assets/icons/LICENSE-ICONS.txt: icon licence file is missing')
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Output $_ }
    Write-Output "Design-token lint: $($violations.Count) violation(s)."
    exit 1
}

Write-Output 'Design-token lint: clean.'
exit 0
