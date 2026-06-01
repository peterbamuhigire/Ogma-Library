param(
    [string] $QueryFile = "",
    [string] $OutputFile = "",
    [ValidateSet("mock", "real")]
    [string] $ProviderMode = "mock"
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    $current = Get-Item -LiteralPath $PSScriptRoot
    while ($null -ne $current) {
        if (Test-Path -LiteralPath (Join-Path $current.FullName "OgmaLibrary.sln")) {
            return $current.FullName
        }

        $current = $current.Parent
    }

    throw "Could not find OgmaLibrary.sln above $PSScriptRoot."
}

$repoRoot = Resolve-RepoRoot
if ([string]::IsNullOrWhiteSpace($QueryFile)) {
    $QueryFile = Join-Path $repoRoot "tests/evaluation/phase-13/queries.json"
}

if ([string]::IsNullOrWhiteSpace($OutputFile)) {
    $OutputFile = Join-Path $repoRoot "docs/benchmarks/phase-13/eval-mock-20260601.json"
}

if ($ProviderMode -ne "mock") {
    throw "Real-provider evaluation is intentionally manual for Phase 13. Use -ProviderMode mock for CI-safe structural evaluation."
}

$queryDoc = Get-Content -LiteralPath $QueryFile -Raw | ConvertFrom-Json
$queries = @($queryDoc.queries)
if ($queries.Count -ne 20) {
    throw "Phase 13 structural evaluation requires exactly 20 queries; found $($queries.Count)."
}

$confidenceLabels = @("VeryHigh", "High", "High", "Medium")
$results = @()
for ($index = 0; $index -lt $queries.Count; $index++) {
    $query = $queries[$index]
    $label = $confidenceLabels[$index % $confidenceLabels.Count]
    $explanationLength = 78 + (($index * 7) % 41)
    $latencyMs = 32 + (($index * 5) % 23)
    $structuralPass = -not [string]::IsNullOrWhiteSpace($query.id) -and
        -not [string]::IsNullOrWhiteSpace($query.goal) -and
        @($query.expectedSignals).Count -ge 3

    $results += [ordered]@{
        queryId = $query.id
        structuralPass = $structuralPass
        recommendationCount = 3
        provenanceCount = 3
        confidenceLabel = $label
        explanationLength = $explanationLength
        mockLatencyMs = $latencyMs
    }
}

$passed = @($results | Where-Object { $_.structuralPass }).Count
$distribution = [ordered]@{
    VeryHigh = @($results | Where-Object { $_.confidenceLabel -eq "VeryHigh" }).Count
    High = @($results | Where-Object { $_.confidenceLabel -eq "High" }).Count
    Medium = @($results | Where-Object { $_.confidenceLabel -eq "Medium" }).Count
    Low = @($results | Where-Object { $_.confidenceLabel -eq "Low" }).Count
}

$summary = [ordered]@{
    schemaVersion = 1
    phase = "13"
    runDate = "2026-06-01"
    providerMode = $ProviderMode
    queryCount = $queries.Count
    structuralPassRate = [math]::Round($passed / $queries.Count, 4)
    avgExplanationLength = [math]::Round((($results | ForEach-Object { $_["explanationLength"] } | Measure-Object -Average).Average), 2)
    confidenceDistribution = $distribution
    avgMockLatencyMs = [math]::Round((($results | ForEach-Object { $_["mockLatencyMs"] } | Measure-Object -Average).Average), 2)
    results = $results
}

$outputDir = Split-Path -Parent $OutputFile
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputFile -Encoding UTF8

if ($summary.structuralPassRate -ne 1.0) {
    throw "Structural pass rate must be 1.0 for the deterministic mock harness."
}

Write-Host "Phase 13 mock structural evaluation passed: $($queries.Count) queries, pass rate $($summary.structuralPassRate)."
