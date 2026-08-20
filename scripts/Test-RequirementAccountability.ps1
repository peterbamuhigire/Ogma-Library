[CmdletBinding()]
param(
    [string]$SrsPath = "docs/references/Ogma-Library_SRS_v2.1_2026-08-13.docx",
    [string]$RoadmapMatrixPath = "docs/plans/aug-39/appendices/01-requirement-phase-matrix.md"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RepositoryPath {
    param([Parameter(Mandatory)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
}

function Get-RequirementIds {
    param([Parameter(Mandatory)][string]$Text)

    $pattern = '(?<![A-Z0-9-])(?:FR-[A-Z]+-\d{3}|NFR-[A-Z]+-\d{3}|CTRL-\d{3})(?![A-Z0-9-])'
    return [System.Text.RegularExpressions.Regex]::Matches(
        $Text,
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
    ) | ForEach-Object { $_.Value.ToUpperInvariant() } | Sort-Object -Unique
}

$resolvedSrsPath = Resolve-RepositoryPath $SrsPath
$resolvedMatrixPath = Resolve-RepositoryPath $RoadmapMatrixPath

if (-not [System.IO.File]::Exists($resolvedSrsPath)) {
    throw "Canonical SRS not found: $resolvedSrsPath"
}

if (-not [System.IO.File]::Exists($resolvedMatrixPath)) {
    throw "Requirement-to-phase matrix not found: $resolvedMatrixPath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedSrsPath)
try {
    $documentEntry = $archive.GetEntry("word/document.xml")
    if ($null -eq $documentEntry) {
        throw "The canonical SRS does not contain word/document.xml."
    }

    $stream = $documentEntry.Open()
    $reader = [System.IO.StreamReader]::new($stream)
    try {
        $srsXml = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}
finally {
    $archive.Dispose()
}

$srsIds = @(Get-RequirementIds $srsXml)
$matrixIds = @(Get-RequirementIds ([System.IO.File]::ReadAllText($resolvedMatrixPath)))

$missingFromMatrix = @($srsIds | Where-Object { $_ -notin $matrixIds })
$unknownInMatrix = @($matrixIds | Where-Object { $_ -notin $srsIds })
$functionalCount = @($srsIds | Where-Object { $_.StartsWith("FR-", [System.StringComparison]::Ordinal) }).Count
$nonFunctionalCount = @($srsIds | Where-Object { $_.StartsWith("NFR-", [System.StringComparison]::Ordinal) }).Count
$controlCount = @($srsIds | Where-Object { $_.StartsWith("CTRL-", [System.StringComparison]::Ordinal) }).Count

$errors = [System.Collections.Generic.List[string]]::new()
if ($functionalCount -ne 101) {
    $errors.Add("Expected 101 functional requirements; found $functionalCount.")
}
if ($nonFunctionalCount -ne 29) {
    $errors.Add("Expected 29 non-functional requirements; found $nonFunctionalCount.")
}
if ($controlCount -ne 32) {
    $errors.Add("Expected 32 controls; found $controlCount.")
}
if ($missingFromMatrix.Count -gt 0) {
    $errors.Add("SRS IDs missing from the roadmap matrix: $($missingFromMatrix -join ', ').")
}
if ($unknownInMatrix.Count -gt 0) {
    $errors.Add("Roadmap matrix IDs absent from the SRS: $($unknownInMatrix -join ', ').")
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "Requirement accountability verified: 101 FRs, 29 NFRs, 32 controls; all 162 IDs are assigned in the roadmap matrix."
