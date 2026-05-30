#Requires -Version 5.1
<#
.SYNOPSIS
    Imports purchased premium Flaticon SVG icons into the Ogma Library asset tree.

.DESCRIPTION
    Copies <icon_key>.svg files from -SourceDir into
    src/OgmaLibrary.App/Assets/icons/<category>/<icon_key>.svg,
    deriving the category from the built-in icon manifest.

    After copying, prints a status table showing which of the 34 Phase 03 keys
    are now real (file > 1 KB) vs still placeholder.

    TODO (post-Phase 03): generate PNG @1x/@2x/@3x density variants from the SVG
    masters using an SVG rasterizer (e.g. Inkscape CLI or SkiaSharp). For now only
    SVG is wired; Avalonia.Svg.Skia renders them at any DPI.

.PARAMETER SourceDir
    Path to the folder containing purchased <icon_key>.svg files.
    Example: C:\Downloads\ogma-phase03-flaticon\

.EXAMPLE
    scripts/Import-Icons.ps1 -SourceDir C:\Downloads\ogma-phase03-flaticon\
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SourceDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Icon manifest: key -> category
$IconCategories = @{
    'ic_app_logo'           = 'app'
    'ic_settings'           = 'app'
    'ic_keyboard_shortcut'  = 'app'
    'ic_close'              = 'app'
    'ic_search'             = 'app'
    'ic_lib_scan'           = 'library'
    'ic_lib_folder_open'    = 'library'
    'ic_lib_health'         = 'library'
    'ic_lib_preferences'    = 'library'
    'ic_cat_view_grid'      = 'catalogue'
    'ic_cat_view_list'      = 'catalogue'
    'ic_cat_view_shelf3d'   = 'catalogue'
    'ic_cat_view_directory' = 'catalogue'
    'ic_cat_shelf'          = 'catalogue'
    'ic_cat_shelf_new'      = 'catalogue'
    'ic_cat_filter'         = 'catalogue'
    'ic_cat_sort'           = 'catalogue'
    'ic_read_open'          = 'reader'
    'ic_read_bookmark'      = 'reader'
    'ic_read_zoom_in'       = 'reader'
    'ic_read_zoom_out'      = 'reader'
    'ic_read_fullscreen'    = 'reader'
    'ic_search_metadata'    = 'search'
    'ic_search_fulltext'    = 'search'
    'ic_ai_advisor'         = 'ai'
    'ic_ai_privacy'         = 'ai'
    'ic_ai_disable'         = 'ai'
    'ic_set_appearance'     = 'settings'
    'ic_set_language'       = 'settings'
    'ic_set_updates'        = 'settings'
    'ic_set_about'          = 'settings'
    'ic_status_available'   = 'status'
    'ic_status_unavailable' = 'status'
    'ic_status_loading'     = 'status'
}

$ScriptDir  = $PSScriptRoot
$RepoRoot   = Split-Path $ScriptDir -Parent
# Join-Path with multiple children requires PS 6+; chain calls for PS 5.1 compatibility
$AssetsRoot = Join-Path (Join-Path (Join-Path (Join-Path $RepoRoot 'src') 'OgmaLibrary.App') 'Assets') 'icons'

if (-not (Test-Path $SourceDir)) {
    Write-Error "SourceDir not found: $SourceDir"
}

# Copy purchased SVGs
$imported = 0
$notFound = 0

foreach ($key in $IconCategories.Keys) {
    $srcFile = Join-Path $SourceDir "$key.svg"
    if (-not (Test-Path $srcFile)) {
        Write-Host "  MISS  $key  (not in SourceDir - skipped)" -ForegroundColor DarkYellow
        $notFound++
        continue
    }

    $category = $IconCategories[$key]
    $destDir  = Join-Path $AssetsRoot $category
    $destFile = Join-Path $destDir "$key.svg"

    if (-not (Test-Path $destDir)) {
        $null = New-Item -ItemType Directory -Path $destDir -Force
    }

    Copy-Item -Path $srcFile -Destination $destFile -Force
    Write-Host "  COPY  $category/$key.svg" -ForegroundColor Green
    $imported++
}

# Status report
Write-Host ''
Write-Host '--- Phase 03 icon status report ---' -ForegroundColor Cyan
$real        = 0
$placeholder = 0
$missing     = 0

foreach ($key in ($IconCategories.Keys | Sort-Object)) {
    $category = $IconCategories[$key]
    $file     = Join-Path (Join-Path $AssetsRoot $category) "$key.svg"

    if (-not (Test-Path $file)) {
        Write-Host "  MISSING      $category/$key.svg" -ForegroundColor Red
        $missing++
    }
    elseif ((Get-Item $file).Length -gt 1024) {
        Write-Host "  real         $category/$key.svg" -ForegroundColor Green
        $real++
    }
    else {
        Write-Host "  placeholder  $category/$key.svg" -ForegroundColor Yellow
        $placeholder++
    }
}

Write-Host ''
Write-Host "Summary: $real real  |  $placeholder placeholder  |  $missing missing" -ForegroundColor Cyan
Write-Host "Imported this run: $imported  |  Not in SourceDir: $notFound"
Write-Host ''
Write-Host 'TODO (post-Phase 03): generate PNG @1x/@2x/@3x variants from SVG masters.' -ForegroundColor DarkGray
Write-Host "Run 'dotnet build' after importing to pick up the new AvaloniaResource assets."
