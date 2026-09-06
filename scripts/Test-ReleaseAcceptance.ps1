[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $RecordPath
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $RecordPath -PathType Leaf)) { throw 'Acceptance record does not exist.' }
$record = Get-Content -LiteralPath $RecordPath -Raw | ConvertFrom-Json

function Assert-True([object] $value, [string] $message) {
    if ($value -ne $true) { throw $message }
}

function Assert-AllowedProperties([object] $value, [string[]] $allowed, [string] $scope) {
    if ($null -eq $value) { throw "$scope is required." }
    foreach ($property in @($value.PSObject.Properties.Name)) {
        if ($property -notin $allowed) { throw "$scope contains unsupported property '$property'." }
    }
}

if ($record.schema -ne 'ogma-release-acceptance-v1') { throw 'Unsupported acceptance record schema.' }
Assert-AllowedProperties $record @('schema', 'releaseId', 'commitSha', 'artifacts', 'hardware', 'schemaFreeze', 'migration', 'approval') 'Acceptance record'
if ([string]::IsNullOrWhiteSpace($record.releaseId) -or $record.releaseId.Length -gt 128 -or $record.releaseId -notmatch '^[A-Za-z0-9._-]+$') { throw 'Acceptance releaseId is missing or unsafe.' }
if ($record.commitSha -notmatch '^[0-9a-fA-F]{40}$') { throw 'Acceptance record must bind to a full commit SHA.' }
$artifacts = @($record.artifacts)
if ($artifacts.Count -ne 2) { throw 'Acceptance requires exactly one Windows and one macOS artifact.' }
if (@($artifacts.platform) -notcontains 'windows' -or @($artifacts.platform) -notcontains 'macos') { throw 'Acceptance requires both platform records.' }

foreach ($artifact in $artifacts) {
    Assert-AllowedProperties $artifact @('platform', 'runtimeIdentifier', 'artifactName', 'sha256', 'descriptorSignatureVerified', 'platformSigned', 'cleanInstall', 'criticalFlows', 'performanceBudgets', 'authenticodeOrMsix', 'developerIdAndNotarized') 'Acceptance artifact'
    if ($artifact.platform -notin @('windows', 'macos')) { throw "Unsupported artifact platform '$($artifact.platform)'." }
    if ($artifact.platform -eq 'windows' -and $artifact.runtimeIdentifier -notmatch '^win-(x64|arm64)$') { throw 'Windows artifact runtime identifier is invalid.' }
    if ($artifact.platform -eq 'macos' -and $artifact.runtimeIdentifier -notmatch '^osx-(x64|arm64)$') { throw 'macOS artifact runtime identifier is invalid.' }
    if ([string]::IsNullOrWhiteSpace($artifact.artifactName) -or $artifact.artifactName.Length -gt 255 -or $artifact.artifactName -match '[\\/:]' -or $artifact.artifactName -match '\.\.') { throw "Unsafe artifact name for $($artifact.platform)." }
    if ($artifact.sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw "Invalid artifact digest for $($artifact.platform)." }
    Assert-True $artifact.descriptorSignatureVerified "$($artifact.platform) descriptor signature is not verified."
    Assert-True $artifact.platformSigned "$($artifact.platform) platform signature is not verified."
    Assert-True $artifact.cleanInstall "$($artifact.platform) clean install is not verified."
    Assert-True $artifact.criticalFlows "$($artifact.platform) critical-flow acceptance is not verified."
    Assert-True $artifact.performanceBudgets "$($artifact.platform) performance evidence is not verified."
    if ($artifact.platform -eq 'windows') { Assert-True $artifact.authenticodeOrMsix 'Windows Authenticode/MSIX evidence is required.' }
    if ($artifact.platform -eq 'macos') { Assert-True $artifact.developerIdAndNotarized 'macOS Developer ID/notarization evidence is required.' }
}

if (@($record.hardware).Count -ne 2) { throw 'Acceptance requires exactly both reference machine records.' }
foreach ($machineId in @('W-REF-01', 'M-REF-01')) {
    $machine = @($record.hardware | Where-Object machineId -eq $machineId)
    if ($machine.Count -ne 1) { throw "Exactly one $machineId hardware record is required." }
    Assert-AllowedProperties $machine[0] @('machineId', 'installedBuild', 'performanceEvidence', 'accessibilityEvidence') "$machineId hardware record"
    Assert-True $machine[0].installedBuild "$machineId installed-build evidence is required."
    Assert-True $machine[0].performanceEvidence "$machineId performance evidence is required."
    Assert-True $machine[0].accessibilityEvidence "$machineId accessibility evidence is required."
}

if ($null -eq $record.schemaFreeze) { throw 'Acceptance schema-freeze evidence is required.' }
Assert-AllowedProperties $record.schemaFreeze @('version', 'migrationCount', 'latestMigration', 'sequenceSha256', 'verified') 'Acceptance schema freeze'
if ($record.schemaFreeze.version -ne 'beta-schema-v1') { throw 'Acceptance schema-freeze version is invalid.' }
if ($record.schemaFreeze.migrationCount -ne 41) { throw 'Acceptance migration count does not match the frozen baseline.' }
if ($record.schemaFreeze.latestMigration -ne '20260906060000_Phase17PausedJobStatus') { throw 'Acceptance latest migration does not match the frozen baseline.' }
if ($record.schemaFreeze.sequenceSha256 -ne '8135fad43778f705b48c9d667d8e56d36b8d4445b8be3a5d2b985b1e42637dd5') { throw 'Acceptance migration sequence digest does not match the frozen baseline.' }
Assert-True $record.schemaFreeze.verified 'Acceptance schema-freeze verification is required.'

if ($null -eq $record.migration) { throw 'Acceptance migration evidence is required.' }
Assert-AllowedProperties $record.migration @('upgrade', 'interruptedUpgradeRecovery', 'rollback', 'backupRestore') 'Acceptance migration evidence'
foreach ($gate in @('upgrade', 'interruptedUpgradeRecovery', 'rollback', 'backupRestore')) {
    Assert-True $record.migration.$gate "Migration gate '$gate' is not verified."
}
if ($null -eq $record.approval) { throw 'Acceptance approval evidence is required.' }
Assert-AllowedProperties $record.approval @('owner', 'approvedAtUtc', 'residualRisksAccepted') 'Acceptance approval'
Assert-True $record.approval.residualRisksAccepted 'Owner residual-risk acceptance is required.'
if ([string]::IsNullOrWhiteSpace($record.approval.owner)) { throw 'Acceptance owner is required.' }
$approvedAt = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse($record.approval.approvedAtUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$approvedAt)) { throw 'approvedAtUtc must be an ISO-8601 timestamp.' }

Write-Output "Release acceptance passed for $($record.releaseId) at commit $($record.commitSha)."
