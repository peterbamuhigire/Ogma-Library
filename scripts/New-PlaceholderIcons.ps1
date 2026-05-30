#Requires -Version 5.1
<#
.SYNOPSIS
    Generates flat-color placeholder SVG icons for all 34 Phase 03 icon keys.

.DESCRIPTION
    For each icon_key in the Phase 03 manifest, emits a 48x48 SVG with:
      - A rounded square tinted by the key's color family (oak/ink/sage/clay/plum/slate)
      - 2-3 upper-case initials of the key name in white
    Output: src/OgmaLibrary.App/Assets/icons/<category>/<key>.svg

    Run this script once to populate placeholder icons. When the owner purchases
    premium Flaticon SVGs, run Import-Icons.ps1 to replace them.

.NOTES
    PLACEHOLDER ICONS — DO NOT SHIP. These are development placeholders only.
    Status after running: placeholder in use (see docs/plans/grand-plan/phase-03/icons.md)
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Color families
$FamilyColors = @{
    oak   = '#D4922A'
    ink   = '#2B4A7A'
    sage  = '#4A7A5A'
    clay  = '#C05A3A'
    plum  = '#6A3A7A'
    slate = '#5A6A7A'
}

# Icon manifest: key -> category, color family
# Matches docs/plans/grand-plan/_icons/PHASE-03-FLATICON-SHOPPING-LIST.md
$Icons = @(
    [PSCustomObject]@{ Key = 'ic_app_logo';            Category = 'app';       Family = 'oak'   }
    [PSCustomObject]@{ Key = 'ic_settings';            Category = 'app';       Family = 'slate' }
    [PSCustomObject]@{ Key = 'ic_keyboard_shortcut';   Category = 'app';       Family = 'slate' }
    [PSCustomObject]@{ Key = 'ic_close';               Category = 'app';       Family = 'slate' }
    [PSCustomObject]@{ Key = 'ic_search';              Category = 'app';       Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_lib_scan';            Category = 'library';   Family = 'oak'   }
    [PSCustomObject]@{ Key = 'ic_lib_folder_open';     Category = 'library';   Family = 'oak'   }
    [PSCustomObject]@{ Key = 'ic_lib_health';          Category = 'library';   Family = 'sage'  }
    [PSCustomObject]@{ Key = 'ic_lib_preferences';     Category = 'library';   Family = 'slate' }
    [PSCustomObject]@{ Key = 'ic_cat_view_grid';       Category = 'catalogue'; Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_cat_view_list';       Category = 'catalogue'; Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_cat_view_shelf3d';    Category = 'catalogue'; Family = 'oak'   }
    [PSCustomObject]@{ Key = 'ic_cat_view_directory';  Category = 'catalogue'; Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_cat_shelf';           Category = 'catalogue'; Family = 'oak'   }
    [PSCustomObject]@{ Key = 'ic_cat_shelf_new';       Category = 'catalogue'; Family = 'oak'   }
    [PSCustomObject]@{ Key = 'ic_cat_filter';          Category = 'catalogue'; Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_cat_sort';            Category = 'catalogue'; Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_read_open';           Category = 'reader';    Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_read_bookmark';       Category = 'reader';    Family = 'oak'   }
    [PSCustomObject]@{ Key = 'ic_read_zoom_in';        Category = 'reader';    Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_read_zoom_out';       Category = 'reader';    Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_read_fullscreen';     Category = 'reader';    Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_search_metadata';     Category = 'search';    Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_search_fulltext';     Category = 'search';    Family = 'ink'   }
    [PSCustomObject]@{ Key = 'ic_ai_advisor';          Category = 'ai';        Family = 'plum'  }
    [PSCustomObject]@{ Key = 'ic_ai_privacy';          Category = 'ai';        Family = 'plum'  }
    [PSCustomObject]@{ Key = 'ic_ai_disable';          Category = 'ai';        Family = 'plum'  }
    [PSCustomObject]@{ Key = 'ic_set_appearance';      Category = 'settings';  Family = 'slate' }
    [PSCustomObject]@{ Key = 'ic_set_language';        Category = 'settings';  Family = 'slate' }
    [PSCustomObject]@{ Key = 'ic_set_updates';         Category = 'settings';  Family = 'slate' }
    [PSCustomObject]@{ Key = 'ic_set_about';           Category = 'settings';  Family = 'slate' }
    [PSCustomObject]@{ Key = 'ic_status_available';    Category = 'status';    Family = 'sage'  }
    [PSCustomObject]@{ Key = 'ic_status_unavailable';  Category = 'status';    Family = 'clay'  }
    [PSCustomObject]@{ Key = 'ic_status_loading';      Category = 'status';    Family = 'ink'   }
)

$ScriptDir  = $PSScriptRoot
$RepoRoot   = Split-Path $ScriptDir -Parent
# Join-Path with multiple children requires PS 6+; chain calls for PS 5.1 compatibility
$AssetsRoot = Join-Path (Join-Path (Join-Path (Join-Path $RepoRoot 'src') 'OgmaLibrary.App') 'Assets') 'icons'

function Get-Initials {
    param([string]$Key)
    $parts = $Key -replace '^ic_', '' -split '_'
    $letters = $parts | ForEach-Object { $_.Substring(0, [Math]::Min(1, $_.Length)).ToUpper() }
    return ($letters | Select-Object -First 3) -join ''
}

function New-PlaceholderSvg {
    param(
        [string]$FillColor,
        [string]$Initials
    )
    # 48x48 canvas; rounded square at radius 10; centered text at 24,24
    $svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" width="48" height="48">' + [Environment]::NewLine
    $svg += '  <!-- PLACEHOLDER - replace with premium Flaticon SVG via scripts/Import-Icons.ps1 -->' + [Environment]::NewLine
    $svg += '  <rect width="48" height="48" rx="10" ry="10" fill="' + $FillColor + '"/>' + [Environment]::NewLine
    $svg += '  <text x="24" y="24" font-family="system-ui,sans-serif" font-size="16" font-weight="bold" fill="white" text-anchor="middle" dominant-baseline="central">' + $Initials + '</text>' + [Environment]::NewLine
    $svg += '</svg>' + [Environment]::NewLine
    return $svg
}

$generated = 0
$skipped   = 0

foreach ($icon in $Icons) {
    $color    = $FamilyColors[$icon.Family]
    $initials = Get-Initials -Key $icon.Key
    $outDir   = Join-Path $AssetsRoot $icon.Category
    $outFile  = Join-Path $outDir "$($icon.Key).svg"

    if (-not (Test-Path $outDir)) {
        $null = New-Item -ItemType Directory -Path $outDir -Force
    }

    # Skip if a non-placeholder SVG already exists (file > 1 KB is assumed real)
    if (Test-Path $outFile) {
        $size = (Get-Item $outFile).Length
        if ($size -gt 1024) {
            Write-Host "  SKIP  $($icon.Key) (existing file $size bytes - assumed real icon)" -ForegroundColor Yellow
            $skipped++
            continue
        }
    }

    $svg = New-PlaceholderSvg -FillColor $color -Initials $initials
    # Write as UTF-8 without BOM (Avalonia SVG loader expects UTF-8)
    [System.IO.File]::WriteAllText($outFile, $svg, [System.Text.UTF8Encoding]::new($false))

    Write-Host "  OK    $($icon.Category)/$($icon.Key).svg  [$($icon.Family) $color  $initials]" -ForegroundColor Green
    $generated++
}

Write-Host ''
Write-Host "Phase 03 placeholder icons: $generated generated, $skipped skipped (already real)." -ForegroundColor Cyan
Write-Host 'Status: placeholder in use - run scripts/Import-Icons.ps1 to replace with premium SVGs.'
